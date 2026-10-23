namespace SyncDatabase.Application.DTOs.Comparacao;

public sealed class DiferencaIndice
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string TipoDiferenca { get; init; } = string.Empty;
    public string? NomeIndiceProducao { get; init; }
    public string? NomeIndiceHomologacao { get; init; }
    public string? ValorProducao { get; init; }
    public string? ValorHomologacao { get; init; }
}
