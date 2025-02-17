﻿using System;
using System.Collections.Generic;
using System.Linq;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class Order
{
    private List<OrderItem> _items = new();

    public Order(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

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

    private void On(ItemAdded itemAdded)
    {
        Guard.AgainstNull(itemAdded);

        _items.Add(new()
        {
            Product = itemAdded.Product,
            Quantity = itemAdded.Quantity,
            Cost = itemAdded.Cost
        });
    }

    public double Total()
    {
        return _items.Sum(item => item.Total());
    }
}