using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorConstraintsBanco
{
    Task<IReadOnlyCollection<ConstraintBanco>> ListarConstraintsAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
