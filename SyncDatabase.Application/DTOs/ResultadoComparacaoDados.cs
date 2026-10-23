
namespace SyncDatabase.Application.DTOs.Comparacao;

/// <summary>
/// Resumo de uma comparação de dados, sem retornar os valores completos das linhas.
/// </summary>
public sealed class ResultadoComparacaoDados
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public bool Comparavel { get; init; }
    public string? MotivoNaoComparavel { get; init; }
    public DateTime DataComparacao { get; init; } = DateTime.UtcNow;
    public int TamanhoLote { get; init; }

    public long RegistrosProducao { get; init; }
    public long RegistrosHomologacao { get; init; }
    public long RegistrosApenasNaProducao { get; init; }
    public long RegistrosApenasNaHomologacao { get; init; }
    public long RegistrosComDiferencas { get; init; }

    public Dictionary<string, long> DiferencasPorColuna { get; init; } = [];

    public List<string> ColunasIgnoradasPorTipo { get; init; } = [];

    public List<string> AmostrasChaveAnonimizadasApenasNaProducao { get; init; } = [];

    public List<string> AmostrasChaveAnonimizadasApenasNaHomologacao { get; init; } = [];

    /// <summary>
    /// Lista limitada de registros com divergências de valores.
    /// </summary>
    public List<RegistroDivergenteDados> RegistrosDivergentes { get; init; } = [];
}
