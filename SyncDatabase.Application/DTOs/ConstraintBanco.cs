namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class ConstraintBanco
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public List<string> Colunas { get; init; } = [];
    public string Definicao { get; init; } = string.Empty;
    public bool Diferida { get; init; }
    public bool InicialmenteDiferida { get; init; }
    public bool Validada { get; init; }
    public bool NulosNaoDistintos { get; init; }
}
