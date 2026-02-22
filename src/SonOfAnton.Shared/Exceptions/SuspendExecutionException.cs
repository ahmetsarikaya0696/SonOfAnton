namespace SonOfAnton.Shared.Exceptions;

/// <summary>
/// Workflow execution'ın beklemeye alındığını (suspend) belirten özel hata sınıfı.
/// Bu fırlatıldığında motor işlemi durdurur ve state'i kaydeder.
/// </summary>
public class SuspendExecutionException : System.Exception
{
    public Guid NodeId { get; }

    public SuspendExecutionException(Guid nodeId, string message) : base(message)
    {
        NodeId = nodeId;
    }
}
