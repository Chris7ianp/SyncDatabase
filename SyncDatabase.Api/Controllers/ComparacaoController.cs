using Microsoft.AspNetCore.Mvc;
using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Servicos.Comparacao;

namespace SyncDatabase.Api.Controllers;

[ApiController]
[Route("api/comparacao")]
public class ComparacaoController : ControllerBase
{
    private readonly IComparadorEstrutura _comparador;
    private readonly IComparadorDados _comparadorDados;

    public ComparacaoController(
        IComparadorEstrutura comparador,
        IComparadorDados comparadorDados)
    {
        _comparador = comparador;
        _comparadorDados = comparadorDados;
    }

    /// <summary>Compara a estrutura PostgreSQL dos dois ambientes em modo somente leitura.</summary>
    /// <remarks>O resultado informa explicitamente as categorias estruturais ainda não comparadas.</remarks>
    [HttpGet("estrutura")]
    public async Task<IActionResult> CompararEstrutura(
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _comparador.CompararAsync(
                cancellationToken);

            return Ok(resultado);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return Problem(
                title: "Configuração inválida",
                detail: "Verifique se as conexões dos dois bancos estão configuradas.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Npgsql.NpgsqlException)
        {
            return Problem(
                title: "Falha na consulta",
                detail: "Não foi possível consultar um dos bancos PostgreSQL.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Compara dados de uma tabela por chave primária, em lotes e somente leitura.</summary>
    /// <remarks>Retorna contagens e amostras de chaves anonimizadas, sem expor valores das linhas.</remarks>
    [HttpGet("dados")]
    public async Task<IActionResult> CompararDados(
        [FromQuery] string schema,
        [FromQuery] string tabela,
        [FromQuery] int tamanhoLote = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resultado = await _comparadorDados.CompararTabelaAsync(
                schema, tabela, tamanhoLote, cancellationToken);
            return Ok(resultado);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parâmetros inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException)
        {
            return Problem(
                title: "Configuração inválida",
                detail: "Verifique se as conexões dos dois bancos estão configuradas.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Npgsql.NpgsqlException)
        {
            return Problem(
                title: "Falha na consulta",
                detail: "Não foi possível comparar os dados da tabela nos bancos PostgreSQL.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
