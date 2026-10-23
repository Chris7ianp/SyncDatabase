
namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class ColunaBanco
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string Coluna { get; init; } = string.Empty;

    public string TipoDados { get; init; } = string.Empty;

    public bool AceitaNulo { get; init; }

    public string? ValorPadrao { get; init; }

    public int Posicao { get; init; }

    public int? ComprimentoMaximo { get; init; }

    public int? PrecisaoNumerica { get; init; }

    public int? EscalaNumerica { get; init; }
}
