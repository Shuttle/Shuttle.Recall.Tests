using System;
using System.Data.SqlClient;
using NUnit.Framework;

namespace Shuttle.Recall.Tests
{
	public class RecallFixture
	{
	    public static void ExcerciseKeyStore(IKeyStore store)
	    {
            var id = Guid.NewGuid();

            var value = string.Concat("value=", id.ToString());
            var anotherValue = string.Concat("anotherValue=", id.ToString());

            store.Add(id, value);

            Assert.Throws<SqlException>(() => store.Add(id, value), string.Format("Should not be able to add duplicate key / id = {0} / key = '{1}'", id, value));

            var idGet = store.Get(value);

            Assert.IsNotNull(idGet, string.Format("Should be able to retrieve the id of the associated key / id = {0} / key = '{1}'", id, value));
            Assert.AreEqual(id, idGet, string.Format("Should be able to retrieve the correct id of the associated key / id = {0} / key = '{1}' / id retrieved = {2}", id, value, idGet));

            idGet = store.Get(anotherValue);

            Assert.IsNull(idGet, string.Format("Should not be able to get id of non-existent / id = {0} / key = '{1}'", id, anotherValue));

            store.Remove(id);

            idGet = store.Get(value);

            Assert.IsNull(idGet, string.Format("Should be able to remove association using id (was not removed) / id = {0} / key = '{1}'", id, value));

            store.Add(id, value);
            store.Remove(value);

            idGet = store.Get(value);

            Assert.IsNull(idGet, string.Format("Should be able to remove association using key (was not removed) / id = {0} / key = '{1}'", id, value));
        }

        public static void ExerciseEventStore(IEventStore store)
		{
			var orderId = Guid.NewGuid();
			var orderProcessId = Guid.NewGuid();

			var order = new Order(orderId);
			var orderProcess = new OrderProcess(orderProcessId);

			var orderStream = new EventStream(orderId);
			var orderProcessStream = new EventStream(orderProcessId);

			orderStream.AddEvent(order.AddItem("t-shirt", 5, 125));
			orderStream.AddEvent(order.AddItem("baked beans", 2, 4.55));
			orderStream.AddEvent(order.AddItem("20L white glossy enamel paint", 1, 700));

			var orderTotal = order.Total();

			store.Save(orderStream);

			orderProcessStream.AddEvent(orderProcess.StartPicking());
			store.Save(orderProcessStream);

			order = new Order(orderId);
			orderStream = store.Get(orderId);

			orderStream.Apply(order);

			Assert.AreEqual(orderTotal, order.Total(),
				"The total of the first re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

            orderStream.AttemptSnapshot(1);
            store.Save(orderStream);

            orderProcess = new OrderProcess(orderProcessId);
			orderProcessStream = store.Get(orderProcessId);
			orderProcessStream.Apply(orderProcess);

			Assert.IsTrue(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled),
				"Should be able to change status to 'Fulfilled'");

			orderStream.AddEvent(order.AddItem("4kg bag of potatoes", 5, 15.35));

			orderTotal = order.Total();

			store.Save(orderStream);

			orderProcessStream.AddEvent(orderProcess.Fulfill());

			store.Save(orderProcessStream);

			order = new Order(orderId);
			orderStream = store.Get(orderId);
			orderStream.Apply(order);

			Assert.AreEqual(orderTotal, order.Total(),
				"The total of the second re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

			orderProcess = new OrderProcess(orderProcessId);
			orderProcessStream = store.Get(orderProcessId);
			orderProcessStream.Apply(orderProcess);

			Assert.IsFalse(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled),
				"Should not be able to change status to 'Fulfilled'");

			store.Remove(orderId);
			store.Remove(orderProcessStream.Id);

			orderStream = store.Get(orderId);
			orderProcessStream = store.Get(orderProcessId);

			Assert.IsTrue(orderStream.IsEmpty);
			Assert.IsTrue(orderProcessStream.IsEmpty);
		}
	}
}