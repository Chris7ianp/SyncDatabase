using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorChavesEstrangeirasBanco
{
    Task<IReadOnlyCollection<ChaveEstrangeiraBanco>> ListarChavesEstrangeirasAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
