
using Dapper;
using Npgsql;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorEstruturaPostgreSql : ILeitorEstruturaBanco
{
    public async Task<IReadOnlyCollection<string>> ListarTabelasAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                schemaname AS Schema,
                tablename AS Tabela
            FROM pg_catalog.pg_tables
            WHERE schemaname NOT IN (
                'pg_catalog',
                'information_schema'
            )
            ORDER BY schemaname, tablename;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);

        var tabelas = await conexao.QueryAsync<TabelaBanco>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return tabelas
            .Select(t => $"{t.Schema}.{t.Tabela}")
            .ToList();
    }

    private sealed class TabelaBanco
    {
        public string Schema { get; init; } = string.Empty;

        public string Tabela { get; init; } = string.Empty;
    }
}
