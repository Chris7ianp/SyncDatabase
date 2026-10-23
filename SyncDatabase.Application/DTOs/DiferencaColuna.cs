
namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class DiferencaColuna
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string Coluna { get; init; } = string.Empty;

    public string TipoDiferenca { get; init; } = string.Empty;

    public string? ValorProducao { get; init; }

    public string? ValorHomologacao { get; init; }
}
