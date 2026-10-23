
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace SyncDatabase.Api.Controllers;

[ApiController]
[Route("api/tarefas")]
public sealed class TarefasController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<TarefasController> _logger;

    public TarefasController(
        IConfiguration configuration,
        ILogger<TarefasController> logger)
    {
        _connectionString =
            configuration.GetConnectionString("Homologacao")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Homologacao não configurada.");

        _logger = logger;
    }

    /// <summary>Solicita uma comparação de dados em segundo plano.</summary>
    [HttpPost("comparacao-dados")]
    public async Task<IActionResult> CriarComparacaoDados(
        [FromBody] CriarTarefaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Schema) ||
            string.IsNullOrWhiteSpace(request.Tabela))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parâmetros inválidos",
                Detail = "Schema e tabela são obrigatórios.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.TamanhoLote is < 1 or > 5000)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Tamanho de lote inválido",
                Detail = "Informe um tamanho de lote entre 1 e 5000.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var parametros = JsonSerializer.Serialize(new
        {
            schema = request.Schema,
            tabela = request.Tabela,
            tamanhoLote = request.TamanhoLote
        });

        var id = Guid.NewGuid();

        const string sql = """
            INSERT INTO syncdatabase.tarefas
                (id, tipo, parametros, status, criada_em)
            VALUES
                (@id, @tipo, @parametros, 'Pendente', NOW());
            """;

        try
        {
            await using var connection =
                new NpgsqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("tipo", "CompararDados");
            command.Parameters.AddWithValue(
                "parametros", NpgsqlDbType.Jsonb, parametros);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Tarefa de comparação {TarefaId} criada para {Schema}.{Tabela}.",
                id, request.Schema, request.Tabela);

            return AcceptedAtAction(
                nameof(ConsultarTarefa),
                new { id },
                new
                {
                    id,
                    status = "Pendente",
                    mensagem = "Tarefa criada e aguardando processamento."
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(
                ex, "Não foi possível registrar a tarefa de comparação.");

            return Problem(
                title: "Falha ao registrar tarefa",
                detail: "Não foi possível registrar a tarefa no banco de controle.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }



    /// <summary>Lista as tarefas recentes para o histórico de execuções.</summary>
    [HttpGet]
    public async Task<IActionResult> ListarTarefas(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        if (limite is < 1 or > 200)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Limite inválido",
                Detail = "O limite deve estar entre 1 e 200.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        const string sql = """
        SELECT id, tipo, parametros, status, resultado, erro,
               criada_em, iniciada_em, finalizada_em, tentativas
        FROM syncdatabase.tarefas
        ORDER BY criada_em DESC
        LIMIT @limite;
        """;

        try
        {
            await using var connection =
                new NpgsqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("limite", limite);

            var tarefas = new List<object>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                JsonElement parametros;
                using (var doc = JsonDocument.Parse(reader.GetString(2)))
                    parametros = doc.RootElement.Clone();

                JsonElement? resultado = null;

                if (!reader.IsDBNull(4))
                {
                    using var doc = JsonDocument.Parse(reader.GetString(4));
                    resultado = doc.RootElement.Clone();
                }

                tarefas.Add(new
                {
                    id = reader.GetGuid(0),
                    tipo = reader.GetString(1),
                    parametros,
                    status = reader.GetString(3),
                    resultado,
                    erro = reader.IsDBNull(5) ? null : reader.GetString(5),
                    criadaEm = reader.GetDateTime(6),
                    iniciadaEm = reader.IsDBNull(7)
                        ? (DateTime?)null : reader.GetDateTime(7),
                    finalizadaEm = reader.IsDBNull(8)
                        ? (DateTime?)null : reader.GetDateTime(8),
                    tentativas = reader.GetInt32(9)
                });
            }

            return Ok(tarefas);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(ex, "Falha ao listar o histórico de tarefas.");

            return Problem(
                title: "Falha ao consultar histórico",
                detail: "Não foi possível consultar o histórico de execuções.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }


    /// <summary>Consulta o estado e o resultado de uma tarefa.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ConsultarTarefa(
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, tipo, parametros, status, resultado, erro,
                   criada_em, iniciada_em, finalizada_em, tentativas
            FROM syncdatabase.tarefas
            WHERE id = @id;
            """;

        try
        {
            await using var connection =
                new NpgsqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return NotFound();

            var tarefa = new
            {
                id = reader.GetGuid(0),
                tipo = reader.GetString(1),
                parametros = JsonDocument.Parse(reader.GetString(2)).RootElement.Clone(),
                status = reader.GetString(3),
                resultado = reader.IsDBNull(4)
                    ? (JsonElement?)null
                    : JsonDocument.Parse(reader.GetString(4)).RootElement.Clone(),
                erro = reader.IsDBNull(5) ? null : reader.GetString(5),
                criadaEm = reader.GetDateTime(6),
                iniciadaEm = reader.IsDBNull(7)
                    ? (DateTime?)null : reader.GetDateTime(7),
                finalizadaEm = reader.IsDBNull(8)
                    ? (DateTime?)null : reader.GetDateTime(8),
                tentativas = reader.GetInt32(9)
            };

            return Ok(tarefa);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(
                ex, "Erro ao consultar a tarefa {TarefaId}.", id);

            return Problem(
                title: "Falha ao consultar tarefa",
                detail: "Não foi possível consultar o estado da tarefa.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public sealed record CriarTarefaRequest(
        string Schema,
        string Tabela,
        int TamanhoLote = 200);
}
