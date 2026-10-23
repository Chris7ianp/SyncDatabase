namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class IndiceBanco
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public List<string> Chaves { get; init; } = [];
    public List<string> ColunasIncluidas { get; init; } = [];
    public bool Unico { get; init; }
    public string MetodoAcesso { get; init; } = string.Empty;
    public string? Predicado { get; init; }
    public bool NulosNaoDistintos { get; init; }
    public bool Valido { get; init; }
    public bool Pronto { get; init; }
}
