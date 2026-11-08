namespace SeOne.Application.Interfaces;

/// <summary>
/// Idempotently creates/updates the durable class group and its active memberships.
/// Call only after an enrollment/payment has reached the backend's confirmed state.
/// </summary>
public interface IClassGroupProvisioningService
{
    Task<Guid?> EnsureForClassAsync(Guid classId, CancellationToken cancellationToken = default);
}
