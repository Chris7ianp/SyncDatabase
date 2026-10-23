namespace SyncDatabase.Application.DTOs.Comparacao;

/// <summary>
/// Identifica um registro com divergências entre Produção e Homologação,
/// sem retornar os valores completos das colunas.
/// </summary>
public sealed class RegistroDivergenteDados
{
    public Dictionary<string, object?> ChavePrimaria { get; init; } = [];

    public List<string> ColunasDivergentes { get; init; } = [];
}