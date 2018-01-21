﻿using System;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests
{
    public enum OrderProcessStatus
    {
        Open = 0,
        Picking = 1,
        Fulfilled = 2,
        Cancelled = 3
    }

    public class OrderProcess
    {
        public OrderProcess(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }

        public OrderProcessStatus Status { get; private set; }

        public StatusChanged Cancel()
        {
            var result = new StatusChanged
            {
                Status = OrderProcessStatus.Cancelled
            };

            On(result);

            return result;
        }

        public StatusChanged StartPicking()
        {
            var result = new StatusChanged
            {
                Status = OrderProcessStatus.Picking
            };

            On(result);

            return result;
        }

        public StatusChanged Fulfill()
        {
            var result = new StatusChanged
            {
                Status = OrderProcessStatus.Fulfilled
            };

            On(result);

            return result;
        }

        private void On(StatusChanged statusChanged)
        {
            Guard.AgainstNull(statusChanged, nameof(statusChanged));

            InvariantStatus(statusChanged.Status);

            Status = statusChanged.Status;
        }

        private void InvariantStatus(OrderProcessStatus status)
        {
            if (CanChangeStatusTo(status))
            {
                return;
            }

            throw new InvalidStatusChangeException(status == OrderProcessStatus.Open
                ? "An order can never be placed into the 'Open' status from any other status."
                : $"The status cannot be changed to '{status}' from '{Status}'");
        }

        public bool CanChangeStatusTo(OrderProcessStatus status)
        {
            switch (status)
            {
                case OrderProcessStatus.Open:
                {
                    return false;
                }
                case OrderProcessStatus.Picking:
                {
                    return Status == OrderProcessStatus.Open;
                }
                case OrderProcessStatus.Fulfilled:
                {
                    return Status == OrderProcessStatus.Picking;
                }
                case OrderProcessStatus.Cancelled:
                {
                    return Status == OrderProcessStatus.Open;
                }
            }

            return true;
        }
    }
}