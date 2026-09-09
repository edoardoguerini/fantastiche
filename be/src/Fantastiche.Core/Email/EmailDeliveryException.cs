namespace Fantastiche.Core.Email;

public sealed class EmailDeliveryException(bool transient) : Exception("Invio email non riuscito.")
{
    public bool Transient { get; } = transient;
}
