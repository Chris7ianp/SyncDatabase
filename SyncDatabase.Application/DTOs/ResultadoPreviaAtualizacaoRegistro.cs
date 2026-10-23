namespace SyncDatabase.Application.DTOs;

public sealed class ResultadoPreviaAtualizacaoRegistro
{
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public bool RegistroEncontradoNaProducao { get; init; }
    public bool RegistroEncontradoNaHomologacao { get; init; }
    public bool PodeAplicar { get; init; }
    public string? Mensagem { get; init; }
    public string? PreviewId { get; init; }
    public List<string> ColunasIgnoradasPorTipo { get; init; } = [];
    public List<CampoDivergenteRegistro> Diferencas { get; init; } = [];
}

public sealed class CampoDivergenteRegistro
{
    public string Coluna { get; init; } = string.Empty;
    public string TipoDados { get; init; } = string.Empty;
    public string ValorProducaoJson { get; init; } = "null";
    public string ValorHomologacaoJson { get; init; } = "null";
}
