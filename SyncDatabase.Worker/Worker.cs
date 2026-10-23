
using System.Text.Json;
using Npgsql;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Homologacao")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Homologacao não configurada.");

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("SyncDatabase Worker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tarefa = await BuscarEReservarTarefaAsync(stoppingToken);

                if (tarefa is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                await ProcessarTarefaAsync(tarefa, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no ciclo de processamento do Worker.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("SyncDatabase Worker encerrado.");
    }

    private async Task<TarefaPendente?> BuscarEReservarTarefaAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH proxima AS (
                SELECT id
                FROM syncdatabase.tarefas
                WHERE status = 'Pendente'
                  AND tipo = 'CompararDados'
                ORDER BY criada_em
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE syncdatabase.tarefas AS t
            SET status = 'EmProcessamento',
                iniciada_em = NOW(),
                tentativas = tentativas + 1
            FROM proxima
            WHERE t.id = proxima.id
            RETURNING t.id, t.parametros;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        TarefaPendente? tarefa = null;

        await using (var reader =
            await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                tarefa = new TarefaPendente(
                    reader.GetGuid(0),
                    reader.GetString(1));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return tarefa;
    }

    private async Task ProcessarTarefaAsync(
        TarefaPendente tarefa,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processando tarefa {TarefaId}.", tarefa.Id);

        try
        {
            using var document = JsonDocument.Parse(tarefa.Parametros);
            var root = document.RootElement;

            var schema = root.GetProperty("schema").GetString();
            var tabela = root.GetProperty("tabela").GetString();

            var tamanhoLote = root.TryGetProperty(
                "tamanhoLote", out var tamanho)
                ? tamanho.GetInt32()
                : 200;

            if (string.IsNullOrWhiteSpace(schema) ||
                string.IsNullOrWhiteSpace(tabela))
            {
                throw new ArgumentException(
                    "Schema e tabela são obrigatórios.");
            }

            using var scope = scopeFactory.CreateScope();

            var comparador = scope.ServiceProvider
                .GetRequiredService<IComparadorDados>();

            var resultado = await comparador.CompararTabelaAsync(
                schema, tabela, tamanhoLote, cancellationToken);

            await FinalizarTarefaAsync(
                tarefa.Id,
                "Concluida",
                JsonSerializer.Serialize(resultado),
                null,
                cancellationToken);

            logger.LogInformation(
                "Tarefa {TarefaId} concluída.", tarefa.Id);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Falha ao processar tarefa {TarefaId}.", tarefa.Id);

            await FinalizarTarefaAsync(
                tarefa.Id,
                "Falhou",
                null,
                ex.Message,
                cancellationToken);
        }
    }

    private async Task FinalizarTarefaAsync(
        Guid id,
        string status,
        string? resultado,
        string? erro,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE syncdatabase.tarefas
            SET status = @status,
                resultado = @resultado::jsonb,
                erro = @erro,
                finalizada_em = NOW()
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue(
            "resultado", (object?)resultado ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "erro", (object?)erro ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record TarefaPendente(Guid Id, string Parametros);
}
