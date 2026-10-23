
namespace SyncDatabase.Application.DTOs.Comparacao;

public sealed class DiferencaChavePrimaria
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string TipoDiferenca { get; init; } = string.Empty;

    public string? NomeConstraintProducao { get; init; }

    public string? NomeConstraintHomologacao { get; init; }

    public List<string> ColunasProducao { get; init; } = [];

    public List<string> ColunasHomologacao { get; init; } = [];
}
