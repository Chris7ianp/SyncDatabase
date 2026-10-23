using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SyncDatabase.Application.DTOs;
using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Comparacao;

/// <summary>Prepara e aplica uma atualização pontual de Produção para Homologação.</summary>
public sealed class SincronizadorRegistroPostgreSql : ISincronizadorRegistro
{
    private static readonly TimeSpan ValidadePrevia = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, Previa> Previas = new(StringComparer.Ordinal);
    private readonly IConfiguration _configuration;
    private readonly ILeitorEstruturaBanco _leitorEstrutura;
    private readonly ILeitorColunasBanco _leitorColunas;
    private readonly ILeitorChavesPrimariasBanco _leitorChaves;

    public SincronizadorRegistroPostgreSql(
        IConfiguration configuration,
        ILeitorEstruturaBanco leitorEstrutura,
        ILeitorColunasBanco leitorColunas,
        ILeitorChavesPrimariasBanco leitorChaves)
    {
        _configuration = configuration;
        _leitorEstrutura = leitorEstrutura;
        _leitorColunas = leitorColunas;
        _leitorChaves = leitorChaves;
    }

    public async Task<ResultadoPreviaAtualizacaoRegistro> CriarPreviaAsync(
        string schema,
        string tabela,
        IReadOnlyDictionary<string, JsonElement> chavePrimaria,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tabela))
            throw new ArgumentException("Schema e tabela são obrigatórios.");
        if (chavePrimaria is null || chavePrimaria.Count == 0)
            throw new ArgumentException("Informe os valores da chave primária.");

        var (producao, homologacao) = ObterConexoes();
        var nomeCompleto = $"{schema}.{tabela}";
        var tabelasProducao = await _leitorEstrutura.ListarTabelasAsync(producao, cancellationToken);
        var tabelasHomologacao = await _leitorEstrutura.ListarTabelasAsync(homologacao, cancellationToken);
        if (!tabelasProducao.Contains(nomeCompleto, StringComparer.Ordinal) ||
            !tabelasHomologacao.Contains(nomeCompleto, StringComparer.Ordinal))
            throw new ArgumentException("A tabela deve existir nos dois ambientes.");

        var colunasProducao = (await _leitorColunas.ListarColunasAsync(producao, cancellationToken))
            .Where(c => c.Schema == schema && c.Tabela == tabela).ToDictionary(c => c.Coluna, StringComparer.Ordinal);
        var colunasHomologacao = (await _leitorColunas.ListarColunasAsync(homologacao, cancellationToken))
            .Where(c => c.Schema == schema && c.Tabela == tabela).ToDictionary(c => c.Coluna, StringComparer.Ordinal);
        var pkProducao = (await _leitorChaves.ListarChavesPrimariasAsync(producao, cancellationToken))
            .SingleOrDefault(k => k.Schema == schema && k.Tabela == tabela);
        var pkHomologacao = (await _leitorChaves.ListarChavesPrimariasAsync(homologacao, cancellationToken))
            .SingleOrDefault(k => k.Schema == schema && k.Tabela == tabela);
        if (pkProducao is null || pkHomologacao is null ||
            !pkProducao.Colunas.SequenceEqual(pkHomologacao.Colunas, StringComparer.Ordinal))
            throw new ArgumentException("A tabela precisa ter a mesma chave primária nos dois ambientes.");
        if (chavePrimaria.Count != pkProducao.Colunas.Count ||
            pkProducao.Colunas.Any(c => !chavePrimaria.ContainsKey(c) ||
                !colunasProducao.TryGetValue(c, out var cp) || !colunasHomologacao.TryGetValue(c, out var ch) || cp.TipoDados != ch.TipoDados))
            throw new ArgumentException("Informe exatamente as colunas da chave primária, com tipos compatíveis.");

        var compartilhadas = colunasProducao.Keys.Intersect(colunasHomologacao.Keys, StringComparer.Ordinal)
            .Where(c => colunasProducao[c].TipoDados == colunasHomologacao[c].TipoDados)
            .OrderBy(c => colunasProducao[c].Posicao).ToArray();
        var ignoradas = colunasProducao.Keys.Intersect(colunasHomologacao.Keys, StringComparer.Ordinal)
            .Where(c => colunasProducao[c].TipoDados != colunasHomologacao[c].TipoDados).ToList();
        var chaveJson = JsonSerializer.Serialize(chavePrimaria.ToDictionary(p => p.Key, p => p.Value));
        var tabelaSql = $"{Q(schema)}.{Q(tabela)}";

        await using var connP = new NpgsqlConnection(producao);
        await using var connH = new NpgsqlConnection(homologacao);
        await connP.OpenAsync(cancellationToken);
        await connH.OpenAsync(cancellationToken);
        RegistroLido? registroP;
        RegistroLido? registroH;
        try
        {
            registroP = await LerRegistroAsync(connP, tabelaSql, pkProducao.Colunas, compartilhadas, chaveJson, cancellationToken);
            registroH = await LerRegistroAsync(connH, tabelaSql, pkHomologacao.Colunas, compartilhadas, chaveJson, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "22P02")
        {
            var tipos = string.Join(", ", pkProducao.Colunas.Select(c => $"{c} ({colunasProducao[c].TipoDados})"));
            throw new ArgumentException($"O valor da chave primária tem formato inválido. Use o tipo cadastrado no banco: {tipos}.");
        }
        if (registroP is null || registroH is null)
        {
            return new ResultadoPreviaAtualizacaoRegistro
            {
                Schema = schema, Tabela = tabela,
                RegistroEncontradoNaProducao = registroP is not null,
                RegistroEncontradoNaHomologacao = registroH is not null,
                Mensagem = registroP is null
                    ? "O registro não existe em Produção."
                    : "O registro não existe em Homologação. Esta operação atualiza apenas registros existentes; não insere linhas."
            };
        }

        var gravaveis = await ListarColunasGravaveisAsync(connH, schema, tabela, cancellationToken);
        var chaves = pkProducao.Colunas.ToHashSet(StringComparer.Ordinal);
        var diferencas = new List<CampoDivergenteRegistro>();
        var colunasAtualizaveis = new List<string>();
        foreach (var coluna in compartilhadas)
        {
            var valorP = registroP.Valores[coluna];
            var valorH = registroH.Valores[coluna];
            if (string.Equals(valorP, valorH, StringComparison.Ordinal)) continue;
            diferencas.Add(new CampoDivergenteRegistro
            {
                Coluna = coluna,
                TipoDados = colunasProducao[coluna].TipoDados,
                ValorProducaoJson = valorP,
                ValorHomologacaoJson = valorH
            });
            if (!chaves.Contains(coluna) && gravaveis.Contains(coluna)) colunasAtualizaveis.Add(coluna);
        }

        if (diferencas.Count == 0)
            return new ResultadoPreviaAtualizacaoRegistro
            {
                Schema = schema, Tabela = tabela,
                RegistroEncontradoNaProducao = true, RegistroEncontradoNaHomologacao = true,
                Mensagem = "Este registro já está igual nos dois ambientes.",
                ColunasIgnoradasPorTipo = ignoradas
            };
        if (colunasAtualizaveis.Count == 0)
            return new ResultadoPreviaAtualizacaoRegistro
            {
                Schema = schema, Tabela = tabela,
                RegistroEncontradoNaProducao = true, RegistroEncontradoNaHomologacao = true,
                Diferencas = diferencas, ColunasIgnoradasPorTipo = ignoradas,
                Mensagem = "Há diferenças, mas nenhuma coluna pode ser atualizada com segurança."
            };

        var id = Guid.NewGuid().ToString("N");
        var agora = DateTimeOffset.UtcNow;
        foreach (var item in Previas)
            if (item.Value.ExpiraEm <= agora) Previas.TryRemove(item.Key, out _);
        Previas[id] = new Previa(schema, tabela, chaveJson, registroP.Json, registroH.Xmin,
            colunasAtualizaveis, agora.Add(ValidadePrevia));
        return new ResultadoPreviaAtualizacaoRegistro
        {
            Schema = schema, Tabela = tabela,
            RegistroEncontradoNaProducao = true, RegistroEncontradoNaHomologacao = true,
            PodeAplicar = true, PreviewId = id,
            ColunasIgnoradasPorTipo = ignoradas, Diferencas = diferencas,
            Mensagem = "Confira os valores. A prévia expira em cinco minutos e só pode ser aplicada uma vez."
        };
    }

    public async Task<ResultadoAplicacaoAtualizacaoRegistro> AplicarPreviaAsync(
        string previewId,
        bool confirmado,
        CancellationToken cancellationToken = default)
    {
        if (!confirmado) throw new ArgumentException("A confirmação explícita é obrigatória.");
        if (string.IsNullOrWhiteSpace(previewId) || !Previas.TryRemove(previewId, out var previa) ||
            previa.ExpiraEm <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A prévia expirou ou já foi usada. Faça uma nova consulta.");
        var (_, homologacao) = ObterConexoes();
        var tabelaSql = $"{Q(previa.Schema)}.{Q(previa.Tabela)}";
        var colunas = string.Join(", ", previa.Colunas.Select(c => $"{Q(c)} = origem.{Q(c)}"));
        var pk = (await _leitorChaves.ListarChavesPrimariasAsync(homologacao, cancellationToken))
            .SingleOrDefault(k => k.Schema == previa.Schema && k.Tabela == previa.Tabela)
            ?? throw new ArgumentException("A tabela não possui mais a chave primária esperada.");
        var predicado = string.Join(" AND ", pk.Colunas.Select(c => $"destino.{Q(c)} IS NOT DISTINCT FROM chave.{Q(c)}"));
        var sql = $"UPDATE {tabelaSql} AS destino SET {colunas} " +
                  $"FROM jsonb_populate_record(NULL::{tabelaSql}, @dados::jsonb) AS origem, " +
                  $"jsonb_populate_record(NULL::{tabelaSql}, @chave::jsonb) AS chave " +
                  $"WHERE {predicado} AND destino.xmin::text = @xmin";
        await using var conn = new NpgsqlConnection(homologacao);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("dados", NpgsqlTypes.NpgsqlDbType.Jsonb, previa.DadosProducaoJson);
        cmd.Parameters.AddWithValue("chave", NpgsqlTypes.NpgsqlDbType.Jsonb, previa.ChaveJson);
        cmd.Parameters.AddWithValue("xmin", previa.Xmin);
        var afetados = await cmd.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new ResultadoAplicacaoAtualizacaoRegistro
        {
            Aplicado = afetados == 1,
            Schema = previa.Schema,
            Tabela = previa.Tabela,
            Mensagem = afetados == 1
                ? "O registro foi atualizado em Homologação."
                : "O registro mudou desde a prévia. Consulte novamente antes de aplicar."
        };
    }

    private async Task<HashSet<string>> ListarColunasGravaveisAsync(
        NpgsqlConnection conn, string schema, string tabela, CancellationToken cancellationToken)
    {
        const string sql = "SELECT a.attname FROM pg_attribute a " +
            "JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE n.nspname = @schema AND c.relname = @tabela AND a.attnum > 0 AND NOT a.attisdropped " +
            "AND a.attidentity = '' AND a.attgenerated = ''";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("tabela", tabela);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<RegistroLido?> LerRegistroAsync(
        NpgsqlConnection conn, string tabelaSql, IReadOnlyList<string> pk,
        IReadOnlyList<string> colunas, string chaveJson, CancellationToken cancellationToken)
    {
        var fields = string.Join(", ", colunas.Select(c => $"'{c.Replace("'", "''")}', to_jsonb(r.{Q(c)})"));
        var predicado = string.Join(" AND ", pk.Select(c => $"r.{Q(c)} IS NOT DISTINCT FROM chave.{Q(c)}"));
        var sql = $"SELECT jsonb_build_object({fields})::text, r.xmin::text FROM {tabelaSql} AS r " +
            $"CROSS JOIN LATERAL jsonb_populate_record(NULL::{tabelaSql}, @chave::jsonb) AS chave " +
            $"WHERE {predicado} LIMIT 2";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("chave", NpgsqlTypes.NpgsqlDbType.Jsonb, chaveJson);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var json = reader.GetString(0);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var doc = JsonDocument.Parse(json))
            foreach (var property in doc.RootElement.EnumerateObject()) values[property.Name] = property.Value.GetRawText();
        return new RegistroLido(json, reader.GetString(1), values);
    }

    private (string Producao, string Homologacao) ObterConexoes()
    {
        var producao = _configuration.GetConnectionString("Producao");
        var homologacao = _configuration.GetConnectionString("Homologacao");
        if (string.IsNullOrWhiteSpace(producao) || string.IsNullOrWhiteSpace(homologacao))
            throw new InvalidOperationException("As conexões dos dois ambientes devem estar configuradas.");
        return (producao, homologacao);
    }

    private static string Q(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private sealed record RegistroLido(string Json, string Xmin, Dictionary<string, string> Valores);
    private sealed record Previa(string Schema, string Tabela, string ChaveJson, string DadosProducaoJson, string Xmin, List<string> Colunas, DateTimeOffset ExpiraEm);
}
