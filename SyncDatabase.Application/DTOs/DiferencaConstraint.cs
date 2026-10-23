namespace SyncDatabase.Application.DTOs.Comparacao;

public sealed class DiferencaConstraint
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string TipoDiferenca { get; init; } = string.Empty;
    public string? NomeProducao { get; init; }
    public string? NomeHomologacao { get; init; }
    public string? ValorProducao { get; init; }
    public string? ValorHomologacao { get; init; }
}
