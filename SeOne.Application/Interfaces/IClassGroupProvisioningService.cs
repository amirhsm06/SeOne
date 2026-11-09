namespace SeOne.Application.Interfaces;

public interface IClassGroupProvisioningService
{
    Task EnsureForClassAsync(Guid classId, CancellationToken cancellationToken = default);
}
