using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorObjetosAdicionaisBanco
{
    Task<IReadOnlyCollection<ObjetoBanco>> ListarObjetosAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
