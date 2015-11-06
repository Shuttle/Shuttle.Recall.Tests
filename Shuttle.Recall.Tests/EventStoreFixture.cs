using System;
using NUnit.Framework;
using Shuttle.Recall.Core;

namespace Shuttle.Recall.Tests
{
	[TestFixture]
	public class EventStoreFixture
	{
		public void ExerciseEventStore(IEventStore store)
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

			orderStream.AddSnapshot(order.Snapshot());

			var orderTotal = order.Total();

			store.SaveEventStream(orderStream);

			orderProcessStream.AddEvent(orderProcess.StartPicking());
			store.SaveEventStream(orderProcessStream);

			order = new Order(orderId);
			orderStream = store.Get(orderId);
			orderStream.Apply(order);

			Assert.AreEqual(orderTotal, order.Total(), "The total of the first re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

			orderProcess = new OrderProcess(orderProcessId);
			orderProcessStream = store.Get(orderProcessId);
			orderProcessStream.Apply(orderProcess);

			Assert.IsTrue(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled));

			orderStream.AddEvent(order.AddItem("4kg bag of potatoes", 5, 15.35));

			orderTotal = order.Total();

			store.SaveEventStream(orderStream);

			orderProcessStream.AddEvent(orderProcess.Fulfill());

			store.SaveEventStream(orderProcessStream);

			order = new Order(orderId);
			orderStream = store.Get(orderId);
			orderStream.Apply(order);

			Assert.AreEqual(orderTotal, order.Total(), "The total of the second re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

			orderProcess = new OrderProcess(orderProcessId);
			orderProcessStream = store.Get(orderProcessId);
			orderProcessStream.Apply(orderProcess);

			Assert.IsFalse(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled));

            store.Remove(orderId);
            store.Remove(orderProcessId);
            
            orderStream = store.Get(orderId);
            orderProcessStream = store.Get(orderProcessId);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
		}
	}
}