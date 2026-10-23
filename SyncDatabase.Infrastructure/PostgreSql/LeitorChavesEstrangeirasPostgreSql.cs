using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorChavesEstrangeirasPostgreSql : ILeitorChavesEstrangeirasBanco
{
    public async Task<IReadOnlyCollection<ChaveEstrangeiraBanco>> ListarChavesEstrangeirasAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                origem_ns.nspname AS "Schema",
                origem.relname AS "Tabela",
                fk.conname AS "NomeConstraint",
                origem_col.attname AS "Coluna",
                destino_ns.nspname AS "SchemaReferenciado",
                destino.relname AS "TabelaReferenciada",
                destino_col.attname AS "ColunaReferenciada",
                CASE fk.confdeltype
                    WHEN 'a' THEN 'NO ACTION'
                    WHEN 'r' THEN 'RESTRICT'
                    WHEN 'c' THEN 'CASCADE'
                    WHEN 'n' THEN 'SET NULL'
                    WHEN 'd' THEN 'SET DEFAULT'
                END AS "RegraOnDelete",
                CASE fk.confupdtype
                    WHEN 'a' THEN 'NO ACTION'
                    WHEN 'r' THEN 'RESTRICT'
                    WHEN 'c' THEN 'CASCADE'
                    WHEN 'n' THEN 'SET NULL'
                    WHEN 'd' THEN 'SET DEFAULT'
                END AS "RegraOnUpdate",
                origem_chave.ordinalidade AS "OrdemColuna"
            FROM pg_catalog.pg_constraint fk
            INNER JOIN pg_catalog.pg_class origem
                ON origem.oid = fk.conrelid
            INNER JOIN pg_catalog.pg_namespace origem_ns
                ON origem_ns.oid = origem.relnamespace
            INNER JOIN pg_catalog.pg_class destino
                ON destino.oid = fk.confrelid
            INNER JOIN pg_catalog.pg_namespace destino_ns
                ON destino_ns.oid = destino.relnamespace
            CROSS JOIN LATERAL unnest(fk.conkey)
                WITH ORDINALITY AS origem_chave(attnum, ordinalidade)
            INNER JOIN LATERAL unnest(fk.confkey)
                WITH ORDINALITY AS destino_chave(attnum, ordinalidade)
                ON destino_chave.ordinalidade = origem_chave.ordinalidade
            INNER JOIN pg_catalog.pg_attribute origem_col
                ON origem_col.attrelid = origem.oid
               AND origem_col.attnum = origem_chave.attnum
            INNER JOIN pg_catalog.pg_attribute destino_col
                ON destino_col.attrelid = destino.oid
               AND destino_col.attnum = destino_chave.attnum
            WHERE fk.contype = 'f'
              AND origem_ns.nspname NOT IN ('pg_catalog', 'information_schema')
              AND fk.conparentid = 0
            ORDER BY origem_ns.nspname, origem.relname, fk.conname,
                     origem_chave.ordinalidade;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);
        var registros = await conexao.QueryAsync<RegistroChaveEstrangeira>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return registros
            .GroupBy(r => new
            {
                r.Schema, r.Tabela, r.NomeConstraint, r.SchemaReferenciado,
                r.TabelaReferenciada, r.RegraOnDelete, r.RegraOnUpdate
            })
            .Select(grupo => new ChaveEstrangeiraBanco
            {
                Schema = grupo.Key.Schema,
                Tabela = grupo.Key.Tabela,
                NomeConstraint = grupo.Key.NomeConstraint,
                Colunas = grupo.OrderBy(r => r.OrdemColuna).Select(r => r.Coluna).ToList(),
                SchemaReferenciado = grupo.Key.SchemaReferenciado,
                TabelaReferenciada = grupo.Key.TabelaReferenciada,
                ColunasReferenciadas = grupo.OrderBy(r => r.OrdemColuna)
                    .Select(r => r.ColunaReferenciada).ToList(),
                RegraOnDelete = grupo.Key.RegraOnDelete,
                RegraOnUpdate = grupo.Key.RegraOnUpdate
            })
            .ToList();
    }

    private sealed class RegistroChaveEstrangeira
    {
        public string Schema { get; init; } = string.Empty;
        public string Tabela { get; init; } = string.Empty;
        public string NomeConstraint { get; init; } = string.Empty;
        public string Coluna { get; init; } = string.Empty;
        public string SchemaReferenciado { get; init; } = string.Empty;
        public string TabelaReferenciada { get; init; } = string.Empty;
        public string ColunaReferenciada { get; init; } = string.Empty;
        public string RegraOnDelete { get; init; } = string.Empty;
        public string RegraOnUpdate { get; init; } = string.Empty;
        public long OrdemColuna { get; init; }
    }
}
