
using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorChavesPrimariasPostgreSql
    : ILeitorChavesPrimariasBanco
{
    public async Task<IReadOnlyCollection<ChavePrimariaBanco>>
        ListarChavesPrimariasAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ns.nspname AS "Schema",
                tabela.relname AS "Tabela",
                constraint_pk.conname AS "NomeConstraint",
                coluna.attname AS "Coluna",
                chave.ordinalidade AS "OrdemColuna"
            FROM pg_constraint constraint_pk
            INNER JOIN pg_class tabela
                ON tabela.oid = constraint_pk.conrelid
            INNER JOIN pg_namespace ns
                ON ns.oid = tabela.relnamespace
            INNER JOIN LATERAL
                unnest(constraint_pk.conkey)
                WITH ORDINALITY AS chave(attnum, ordinalidade)
                ON TRUE
            INNER JOIN pg_attribute coluna
                ON coluna.attrelid = tabela.oid
               AND coluna.attnum = chave.attnum
            WHERE constraint_pk.contype = 'p'
              AND ns.nspname NOT IN (
                  'pg_catalog',
                  'information_schema'
              )
            ORDER BY
                ns.nspname,
                tabela.relname,
                constraint_pk.conname,
                chave.ordinalidade;
            """;

        await using var conexao =
            new NpgsqlConnection(connectionString);

        var registros = await conexao.QueryAsync<RegistroChavePrimaria>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return registros
            .GroupBy(r => new
            {
                r.Schema,
                r.Tabela,
                r.NomeConstraint
            })
            .Select(grupo => new ChavePrimariaBanco
            {
                Schema = grupo.Key.Schema,
                Tabela = grupo.Key.Tabela,
                NomeConstraint = grupo.Key.NomeConstraint,
                Colunas = grupo
                    .OrderBy(r => r.OrdemColuna)
                    .Select(r => r.Coluna)
                    .ToList()
            })
            .ToList();
    }

    private sealed class RegistroChavePrimaria
    {
        public string Schema { get; init; } = string.Empty;
        public string Tabela { get; init; } = string.Empty;
        public string NomeConstraint { get; init; } = string.Empty;
        public string Coluna { get; init; } = string.Empty;
        public long OrdemColuna { get; init; }
    }
}
