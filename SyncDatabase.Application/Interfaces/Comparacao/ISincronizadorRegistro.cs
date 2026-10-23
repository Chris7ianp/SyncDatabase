using System.Text.Json;
using SyncDatabase.Application.DTOs;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface ISincronizadorRegistro
{
    Task<ResultadoPreviaAtualizacaoRegistro> CriarPreviaAsync(
        string schema,
        string tabela,
        IReadOnlyDictionary<string, JsonElement> chavePrimaria,
        CancellationToken cancellationToken = default);

    Task<ResultadoAplicacaoAtualizacaoRegistro> AplicarPreviaAsync(
        string previewId,
        bool confirmado,
        CancellationToken cancellationToken = default);
}
