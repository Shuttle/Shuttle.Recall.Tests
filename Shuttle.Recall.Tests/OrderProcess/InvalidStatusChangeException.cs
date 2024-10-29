using System;

namespace Shuttle.Recall.Tests;

public class InvalidStatusChangeException : Exception
{
    public InvalidStatusChangeException(string message) : base(message)
    {
    }

    public InvalidStatusChangeException()
    {
    }
}