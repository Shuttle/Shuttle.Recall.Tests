﻿using System;
using System.Collections.Generic;
using System.Linq;
using Shuttle.Core.Infrastructure;

namespace Shuttle.Recall.Tests
{
	public class Order : ICanSnapshot
	{
		public Guid Id { get; private set; }

		private List<OrderItem> _items = new List<OrderItem>();

		public Order(Guid id)
		{
			Id = id;
		}

		public ItemAdded AddItem(string product, double quantity, double cost)
		{
			var result = new ItemAdded
			{
				Product = product,
				Quantity = quantity,
				Cost = cost
			};

			On(result);

			return result;
		}

		public void On(ItemAdded itemAdded)
		{
			Guard.AgainstNull(itemAdded, "itemAdded");

			_items.Add(new OrderItem
			{
				Product = itemAdded.Product,
				Quantity = itemAdded.Quantity,
				Cost = itemAdded.Cost
			});
		}

		public void On(OrderSnapshot snapshot)
		{
			Guard.AgainstNull(snapshot, "snapshot");

			_items = new List<OrderItem>(snapshot.Items);
		}

		public double Total()
		{
			return _items.Sum(item => item.Total());
		}

	    public object GetSnapshotEvent()
	    {
            return new OrderSnapshot
            {
                Items = new List<OrderItem>(_items)
            };
        }
    }
}