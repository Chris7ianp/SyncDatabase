
namespace SyncDatabase.Application.DTOs.Estrutura;

public sealed class ChavePrimariaBanco
{
    public string Schema { get; init; } = string.Empty;

    public string Tabela { get; init; } = string.Empty;

    public string NomeConstraint { get; init; } = string.Empty;

    public List<string> Colunas { get; init; } = [];
}
