using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorColunasBanco
{
    Task<IReadOnlyCollection<ColunaBanco>> ListarColunasAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
