using SyncDatabase.Application.DTOs.Comparacao;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorEstrutura
{
    Task<ResultadoComparacaoEstrutura> CompararAsync(CancellationToken cancellationToken = default);
}
