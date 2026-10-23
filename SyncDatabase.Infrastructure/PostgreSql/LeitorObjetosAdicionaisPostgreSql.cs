using Dapper;
using Npgsql;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Estrutura;

public sealed class LeitorObjetosAdicionaisPostgreSql : ILeitorObjetosAdicionaisBanco
{
    public async Task<IReadOnlyCollection<ObjetoBanco>> ListarObjetosAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 'View'::text AS "Categoria", ns.nspname AS "Schema",
                   rel.relname AS "Nome", NULL::text AS "ObjetoPai",
                   ''::text AS "Identificador",
                   pg_catalog.pg_get_viewdef(rel.oid, true) AS "Definicao"
            FROM pg_catalog.pg_class rel
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = rel.relnamespace
            WHERE rel.relkind = 'v'
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')

            UNION ALL

            SELECT 'MaterializedView'::text, ns.nspname, rel.relname,
                   NULL::text, ''::text,
                   pg_catalog.pg_get_viewdef(rel.oid, true)
            FROM pg_catalog.pg_class rel
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = rel.relnamespace
            WHERE rel.relkind = 'm'
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')

            UNION ALL

            SELECT 'Sequence'::text, ns.nspname, rel.relname,
                   NULL::text, ''::text,
                   format('type=%s;start=%s;increment=%s;min=%s;max=%s;cache=%s;cycle=%s',
                       pg_catalog.format_type(seq.seqtypid, NULL), seq.seqstart,
                       seq.seqincrement, seq.seqmin, seq.seqmax, seq.seqcache, seq.seqcycle)
            FROM pg_catalog.pg_class rel
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = rel.relnamespace
            INNER JOIN pg_catalog.pg_sequence seq ON seq.seqrelid = rel.oid
            WHERE rel.relkind = 'S'
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')

            UNION ALL

            SELECT CASE rotina.prokind WHEN 'p' THEN 'Procedure' ELSE 'Function' END,
                   ns.nspname, rotina.proname, NULL::text,
                   pg_catalog.pg_get_function_identity_arguments(rotina.oid),
                   pg_catalog.pg_get_functiondef(rotina.oid)
            FROM pg_catalog.pg_proc rotina
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = rotina.pronamespace
            WHERE rotina.prokind IN ('f', 'p')
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')

            UNION ALL

            SELECT 'Trigger'::text, ns.nspname, gatilho.tgname, tabela.relname,
                   format('type=%s;enabled=%s;deferrable=%s;initially_deferred=%s',
                       gatilho.tgtype, gatilho.tgenabled,
                       gatilho.tgdeferrable, gatilho.tginitdeferred),
                   pg_catalog.pg_get_triggerdef(gatilho.oid, true)
            FROM pg_catalog.pg_trigger gatilho
            INNER JOIN pg_catalog.pg_class tabela ON tabela.oid = gatilho.tgrelid
            INNER JOIN pg_catalog.pg_namespace ns ON ns.oid = tabela.relnamespace
            WHERE NOT gatilho.tgisinternal
              AND ns.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1, 2, 3, 4, 5;
            """;

        await using var conexao = new NpgsqlConnection(connectionString);
        var objetos = await conexao.QueryAsync<ObjetoBanco>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return objetos.ToList();
    }
}
