
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SyncDatabase.Api.Controllers;

[ApiController]
[Route("api/conexoes")]
public class ConexoesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConexoesController> _logger;

    public ConexoesController(
        IConfiguration configuration,
        ILogger<ConexoesController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Testa a conexão configurada para Produção sem retornar dados de conexão.</summary>
    [HttpGet("testar-producao")]
    public Task<IActionResult> TestarProducao(
        CancellationToken cancellationToken)
    {
        return TestarConexao(
            "Producao",
            "Produção",
            cancellationToken);
    }

    /// <summary>Testa a conexão configurada para Homologação sem retornar dados de conexão.</summary>
    [HttpGet("testar-homologacao")]
    public Task<IActionResult> TestarHomologacao(
        CancellationToken cancellationToken)
    {
        return TestarConexao(
            "Homologacao",
            "Homologação",
            cancellationToken);
    }

    private async Task<IActionResult> TestarConexao(
        string nomeConexao,
        string ambiente,
        CancellationToken cancellationToken)
    {
        var connectionString =
            _configuration.GetConnectionString(nomeConexao);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, new
            {
                conectado = false,
                ambiente,
                mensagem = "Conexão não configurada."
            });
        }

        try
        {
            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            return Ok(new
            {
                conectado = true,
                ambiente,
                mensagem = "Conexão estabelecida com sucesso."
            });
        }
        catch (NpgsqlException ex)
        {
            _logger.LogWarning(
                "Falha ao conectar ao PostgreSQL de {Ambiente}. Estado: {SqlState}.",
                ambiente,
                ex.SqlState ?? "indisponível");

            return StatusCode(503, new
            {
                conectado = false,
                ambiente,
                mensagem = "Não foi possível conectar ao banco."
            });
        }
    }
}
