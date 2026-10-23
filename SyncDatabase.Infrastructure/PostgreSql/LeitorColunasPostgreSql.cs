
using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorColunasPostgreSql : ILeitorColunasBanco
{
    public async Task<IReadOnlyCollection<ColunaBanco>> ListarColunasAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ns.nspname AS "Schema",
                tabela.relname AS "Tabela",
                atributo.attname AS "Coluna",
                pg_catalog.format_type(atributo.atttypid, NULL) AS "TipoDados",
                (NOT atributo.attnotnull) AS "AceitaNulo",
                pg_catalog.pg_get_expr(padrao.adbin, padrao.adrelid) AS "ValorPadrao",
                atributo.attnum AS "Posicao",
                information_schema._pg_char_max_length(
                    atributo.atttypid, atributo.atttypmod) AS "ComprimentoMaximo",
                information_schema._pg_numeric_precision(
                    atributo.atttypid, atributo.atttypmod) AS "PrecisaoNumerica",
                information_schema._pg_numeric_scale(
                    atributo.atttypid, atributo.atttypmod) AS "EscalaNumerica"
            FROM pg_catalog.pg_attribute atributo
            INNER JOIN pg_catalog.pg_class tabela
                ON tabela.oid = atributo.attrelid
            INNER JOIN pg_catalog.pg_namespace ns
                ON ns.oid = tabela.relnamespace
            LEFT JOIN pg_catalog.pg_attrdef padrao
                ON padrao.adrelid = tabela.oid
               AND padrao.adnum = atributo.attnum
            WHERE atributo.attnum > 0
              AND NOT atributo.attisdropped
              AND tabela.relkind IN ('r', 'p')
              AND ns.nspname NOT IN (
                  'pg_catalog',
                  'information_schema'
              )
            ORDER BY
                ns.nspname,
                tabela.relname,
                atributo.attnum;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);

        var colunas = await conexao.QueryAsync<ColunaBanco>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return colunas.ToList();
    }
}
