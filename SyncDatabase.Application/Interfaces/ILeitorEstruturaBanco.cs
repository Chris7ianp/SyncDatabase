
namespace SyncDatabase.Application.Interfaces.Estrutura;

public interface ILeitorEstruturaBanco
{
    Task<IReadOnlyCollection<string>> ListarTabelasAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
