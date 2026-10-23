using SyncDatabase.Application.DTOs.Comparacao;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorDados
{
    Task<ResultadoComparacaoDados> CompararTabelaAsync(
        string schema,
        string tabela,
        int tamanhoLote = 200,
        CancellationToken cancellationToken = default);
}
