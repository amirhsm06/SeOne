namespace SeOne.Application.Exceptions;

public sealed class DeliveryNotConfiguredException : Exception
{
    public DeliveryNotConfiguredException(string message)
        : base(message)
    {
    }
}
