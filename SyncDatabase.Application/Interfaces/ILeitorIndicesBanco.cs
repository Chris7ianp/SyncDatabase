using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorIndicesBanco
{
    Task<IReadOnlyCollection<IndiceBanco>> ListarIndicesAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
