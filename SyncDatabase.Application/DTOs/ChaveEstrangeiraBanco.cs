namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class ChaveEstrangeiraBanco
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string NomeConstraint { get; init; } = string.Empty;

    public List<string> Colunas { get; init; } = [];

    public string SchemaReferenciado { get; init; } = string.Empty;

    public string TabelaReferenciada { get; init; } = string.Empty;

    public List<string> ColunasReferenciadas { get; init; } = [];

    public string RegraOnDelete { get; init; } = string.Empty;

    public string RegraOnUpdate { get; init; } = string.Empty;
}
