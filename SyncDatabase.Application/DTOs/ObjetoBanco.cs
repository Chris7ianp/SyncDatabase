namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class ObjetoBanco
{
    public string Categoria { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string? ObjetoPai { get; init; }
    public string Identificador { get; init; } = string.Empty;
    public string Definicao { get; init; } = string.Empty;
}
