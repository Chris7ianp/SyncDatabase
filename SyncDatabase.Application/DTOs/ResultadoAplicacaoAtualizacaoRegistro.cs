namespace SyncDatabase.Application.DTOs;

public sealed class ResultadoAplicacaoAtualizacaoRegistro
{
    public bool Aplicado { get; init; }
    public string Schema { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
}
