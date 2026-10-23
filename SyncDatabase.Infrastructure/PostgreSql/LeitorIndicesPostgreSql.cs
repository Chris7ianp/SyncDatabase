using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorIndicesPostgreSql : ILeitorIndicesBanco
{
    public async Task<IReadOnlyCollection<IndiceBanco>> ListarIndicesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ns.nspname AS "Schema",
                tabela.relname AS "Tabela",
                indice.relname AS "Nome",
                pg_catalog.pg_get_indexdef(ix.indexrelid, posicao.numero, true) AS "DefinicaoColuna",
                (posicao.numero <= ix.indnkeyatts) AS "EhChave",
                ix.indisunique AS "Unico",
                metodo.amname AS "MetodoAcesso",
                pg_catalog.pg_get_expr(ix.indpred, ix.indrelid, true) AS "Predicado",
                ix.indnullsnotdistinct AS "NulosNaoDistintos",
                ix.indisvalid AS "Valido",
                ix.indisready AS "Pronto"
            FROM pg_catalog.pg_index ix
            INNER JOIN pg_catalog.pg_class tabela ON tabela.oid = ix.indrelid
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = tabela.relnamespace
            INNER JOIN pg_catalog.pg_class indice ON indice.oid = ix.indexrelid
            INNER JOIN pg_catalog.pg_am metodo ON metodo.oid = indice.relam
            CROSS JOIN LATERAL generate_series(1, ix.indnatts) AS posicao(numero)
            WHERE ns.nspname NOT IN ('pg_catalog', 'information_schema')
              AND NOT EXISTS (
                  SELECT 1
                  FROM pg_catalog.pg_constraint constraint_index
                  WHERE constraint_index.conindid = ix.indexrelid
                    AND constraint_index.contype IN ('p', 'u')
              )
            ORDER BY ns.nspname, tabela.relname, indice.relname, posicao.numero;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);
        var registros = await conexao.QueryAsync<RegistroIndice>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return registros
            .GroupBy(r => new
            {
                r.Schema, r.Tabela, r.Nome, r.Unico, r.MetodoAcesso,
                r.Predicado, r.NulosNaoDistintos, r.Valido, r.Pronto
            })
            .Select(grupo => new IndiceBanco
            {
                Schema = grupo.Key.Schema,
                Tabela = grupo.Key.Tabela,
                Nome = grupo.Key.Nome,
                Chaves = grupo.Where(r => r.EhChave).Select(r => r.DefinicaoColuna).ToList(),
                ColunasIncluidas = grupo.Where(r => !r.EhChave).Select(r => r.DefinicaoColuna).ToList(),
                Unico = grupo.Key.Unico,
                MetodoAcesso = grupo.Key.MetodoAcesso,
                Predicado = grupo.Key.Predicado,
                NulosNaoDistintos = grupo.Key.NulosNaoDistintos,
                Valido = grupo.Key.Valido,
                Pronto = grupo.Key.Pronto
            })
            .ToList();
    }

    private sealed class RegistroIndice
    {
        public string Schema { get; init; } = string.Empty;
        public string Tabela { get; init; } = string.Empty;
        public string Nome { get; init; } = string.Empty;
        public string DefinicaoColuna { get; init; } = string.Empty;
        public bool EhChave { get; init; }
        public bool Unico { get; init; }
        public string MetodoAcesso { get; init; } = string.Empty;
        public string? Predicado { get; init; }
        public bool NulosNaoDistintos { get; init; }
        public bool Valido { get; init; }
        public bool Pronto { get; init; }
    }
}
