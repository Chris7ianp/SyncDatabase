namespace SyncDatabase.Application.DTOs.Comparacao;

public sealed class DiferencaObjetoBanco
{
    public string Categoria { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string? ObjetoPai { get; init; }
    public string TipoDiferenca { get; init; } = string.Empty;
    public string? DefinicaoProducao { get; init; }
    public string? DefinicaoHomologacao { get; init; }
}
