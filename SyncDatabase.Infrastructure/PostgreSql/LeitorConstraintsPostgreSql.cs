using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorConstraintsPostgreSql : ILeitorConstraintsBanco
{
    public async Task<IReadOnlyCollection<ConstraintBanco>> ListarConstraintsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ns.nspname AS "Schema",
                tabela.relname AS "Tabela",
                c.conname AS "Nome",
                CASE c.contype WHEN 'u' THEN 'UNIQUE' WHEN 'c' THEN 'CHECK' END AS "Tipo",
                ARRAY(
                    SELECT atributo.attname::text
                    FROM unnest(c.conkey) WITH ORDINALITY AS chave(attnum, ordem)
                    INNER JOIN pg_catalog.pg_attribute atributo
                        ON atributo.attrelid = tabela.oid
                       AND atributo.attnum = chave.attnum
                    ORDER BY chave.ordem
                ) AS "Colunas",
                pg_catalog.pg_get_constraintdef(c.oid, true) AS "Definicao",
                c.condeferrable AS "Diferida",
                c.condeferred AS "InicialmenteDiferida",
                c.convalidated AS "Validada",
                COALESCE(indice.indnullsnotdistinct, false) AS "NulosNaoDistintos"
            FROM pg_catalog.pg_constraint c
            INNER JOIN pg_catalog.pg_class tabela ON tabela.oid = c.conrelid
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = tabela.relnamespace
            LEFT JOIN pg_catalog.pg_index indice ON indice.indexrelid = c.conindid
            WHERE c.contype IN ('u', 'c')
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY ns.nspname, tabela.relname, c.contype, c.conname;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);
        var registros = await conexao.QueryAsync<RegistroConstraint>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return registros.Select(r => new ConstraintBanco
        {
            Schema = r.Schema,
            Tabela = r.Tabela,
            Nome = r.Nome,
            Tipo = r.Tipo,
            Colunas = r.Colunas.ToList(),
            Definicao = r.Definicao,
            Diferida = r.Diferida,
            InicialmenteDiferida = r.InicialmenteDiferida,
            Validada = r.Validada,
            NulosNaoDistintos = r.NulosNaoDistintos
        }).ToList();
    }

    private sealed class RegistroConstraint
    {
        public string Schema { get; init; } = string.Empty;
        public string Tabela { get; init; } = string.Empty;
        public string Nome { get; init; } = string.Empty;
        public string Tipo { get; init; } = string.Empty;
        public string[] Colunas { get; init; } = [];
        public string Definicao { get; init; } = string.Empty;
        public bool Diferida { get; init; }
        public bool InicialmenteDiferida { get; init; }
        public bool Validada { get; init; }
        public bool NulosNaoDistintos { get; init; }
    }
}
