
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorChavesPrimariasBanco
{
    Task<IReadOnlyCollection<ChavePrimariaBanco>>
        ListarChavesPrimariasAsync(
            string connectionString,
            CancellationToken cancellationToken = default);
}
