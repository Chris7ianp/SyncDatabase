namespace SyncDatabase.Application.DTOs.Comparacao;

public sealed class DiferencaChaveEstrangeira
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string TipoDiferenca { get; init; } = string.Empty;

    public string? NomeConstraintProducao { get; init; }

    public string? NomeConstraintHomologacao { get; init; }

    public string? ValorProducao { get; init; }

    public string? ValorHomologacao { get; init; }
}
