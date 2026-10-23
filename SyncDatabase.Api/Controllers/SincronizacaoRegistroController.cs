using Microsoft.AspNetCore.Mvc;
using SyncDatabase.Application.Interfaces.Comparacao;
using System.Text.Json;

namespace SyncDatabase.Api.Controllers;

[ApiController]
[Route("api/sincronizacao/registro")]
public sealed class SincronizacaoRegistroController : ControllerBase
{
    private readonly ISincronizadorRegistro _sincronizador;
    private readonly ILogger<SincronizacaoRegistroController> _logger;

    public SincronizacaoRegistroController(
        ISincronizadorRegistro sincronizador,
        ILogger<SincronizacaoRegistroController> logger)
    {
        _sincronizador = sincronizador;
        _logger = logger;
    }

    [HttpPost("previa")]
    public async Task<IActionResult> CriarPrevia(
        [FromBody] PreviaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _sincronizador.CriarPreviaAsync(
                request.Schema, request.Tabela, request.ChavePrimaria, cancellationToken);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Parâmetros inválidos", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException)
        {
            return Problem(title: "Configuração inválida", detail: "Verifique a configuração das conexões.", statusCode: 500);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.LogWarning("Falha PostgreSQL ao criar prévia de sincronização. SQLSTATE: {SqlState}", ex.SqlState);
            return Problem(title: "Falha na consulta", detail: "Não foi possível consultar um dos bancos PostgreSQL.", statusCode: 503);
        }
    }

    [HttpPost("aplicar")]
    public async Task<IActionResult> Aplicar(
        [FromBody] AplicarRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _sincronizador.AplicarPreviaAsync(
                request.PreviewId, request.Confirmado, cancellationToken);
            return resultado.Aplicado ? Ok(resultado) : Conflict(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Prévia inválida", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException)
        {
            return Problem(title: "Configuração inválida", detail: "Verifique a configuração das conexões.", statusCode: 500);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.LogWarning("Falha PostgreSQL ao aplicar prévia de sincronização. SQLSTATE: {SqlState}", ex.SqlState);
            return Problem(title: "Falha na atualização", detail: "Não foi possível atualizar o registro em Homologação.", statusCode: 503);
        }
    }

    public sealed record PreviaRequest(string Schema, string Tabela, Dictionary<string, JsonElement> ChavePrimaria);
    public sealed record AplicarRequest(string PreviewId, bool Confirmado);
}
