
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Comparacao;

/// <summary>
/// Compara uma tabela por chave primária e lotes, usando transações somente leitura.
/// </summary>
public sealed class ComparadorDadosPostgreSql : IComparadorDados
{
    private const int MaxAmostras = 10;
    private const int MaxRegistrosDivergentes = 50;

    private static readonly byte[] ChaveHashProcesso =
        RandomNumberGenerator.GetBytes(32);

    private readonly IConfiguration _configuration;
    private readonly ILeitorColunasBanco _leitorColunas;
    private readonly ILeitorChavesPrimariasBanco _leitorChaves;
    private readonly ILeitorEstruturaBanco _leitorEstrutura;

    public ComparadorDadosPostgreSql(
        IConfiguration configuration,
        ILeitorColunasBanco leitorColunas,
        ILeitorChavesPrimariasBanco leitorChaves,
        ILeitorEstruturaBanco leitorEstrutura)
    {
        _configuration = configuration;
        _leitorColunas = leitorColunas;
        _leitorChaves = leitorChaves;
        _leitorEstrutura = leitorEstrutura;
    }

    public async Task<ResultadoComparacaoDados> CompararTabelaAsync(
        string schema,
        string tabela,
        int tamanhoLote = 200,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema) ||
            string.IsNullOrWhiteSpace(tabela))
        {
            throw new ArgumentException(
                "Schema e tabela são obrigatórios.");
        }

        if (tamanhoLote is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tamanhoLote),
                "O lote deve estar entre 1 e 500.");
        }

        var producao =
            _configuration.GetConnectionString("Producao");

        var homologacao =
            _configuration.GetConnectionString("Homologacao");

        if (string.IsNullOrWhiteSpace(producao) ||
            string.IsNullOrWhiteSpace(homologacao))
        {
            throw new InvalidOperationException(
                "As conexões dos dois ambientes devem estar configuradas.");
        }

        var tabelasProducao =
            await _leitorEstrutura.ListarTabelasAsync(
                producao, cancellationToken);

        var tabelasHomologacao =
            await _leitorEstrutura.ListarTabelasAsync(
                homologacao, cancellationToken);

        var nomeCompleto = $"{schema}.{tabela}";

        if (!tabelasProducao.Contains(
                nomeCompleto, StringComparer.Ordinal) ||
            !tabelasHomologacao.Contains(
                nomeCompleto, StringComparer.Ordinal))
        {
            return NaoComparavel(
                schema,
                tabela,
                tamanhoLote,
                "A tabela precisa existir nos dois ambientes.");
        }

        var colunasProducao =
            (await _leitorColunas.ListarColunasAsync(
                producao, cancellationToken))
            .Where(c => c.Schema == schema && c.Tabela == tabela)
            .ToDictionary(c => c.Coluna, StringComparer.Ordinal);

        var colunasHomologacao =
            (await _leitorColunas.ListarColunasAsync(
                homologacao, cancellationToken))
            .Where(c => c.Schema == schema && c.Tabela == tabela)
            .ToDictionary(c => c.Coluna, StringComparer.Ordinal);

        var chaveProducao =
            (await _leitorChaves.ListarChavesPrimariasAsync(
                producao, cancellationToken))
            .SingleOrDefault(k =>
                k.Schema == schema && k.Tabela == tabela);

        var chaveHomologacao =
            (await _leitorChaves.ListarChavesPrimariasAsync(
                homologacao, cancellationToken))
            .SingleOrDefault(k =>
                k.Schema == schema && k.Tabela == tabela);

        if (chaveProducao is null || chaveHomologacao is null)
        {
            return NaoComparavel(
                schema,
                tabela,
                tamanhoLote,
                "A tabela precisa ter chave primária nos dois ambientes; " +
                "sem chave estável, a comparação não é segura.");
        }

        if (!chaveProducao.Colunas.SequenceEqual(
                chaveHomologacao.Colunas,
                StringComparer.Ordinal))
        {
            return NaoComparavel(
                schema,
                tabela,
                tamanhoLote,
                "As colunas ou a ordem da chave primária diferem entre os ambientes.");
        }

        var chaves = chaveProducao.Colunas;

        if (chaves.Any(c =>
                !colunasProducao.ContainsKey(c) ||
                !colunasHomologacao.ContainsKey(c) ||
                colunasProducao[c].TipoDados !=
                colunasHomologacao[c].TipoDados))
        {
            return NaoComparavel(
                schema,
                tabela,
                tamanhoLote,
                "O tipo de uma coluna da chave primária difere; " +
                "a comparação segura por chave não é possível.");
        }

        var colunasCompartilhadas = colunasProducao.Keys
            .Intersect(
                colunasHomologacao.Keys,
                StringComparer.Ordinal)
            .OrderBy(c => colunasProducao[c].Posicao)
            .ToList();

        var ignoradas = colunasCompartilhadas
            .Where(c =>
                colunasProducao[c].TipoDados !=
                colunasHomologacao[c].TipoDados)
            .ToList();

        var colunasComparadas = colunasCompartilhadas
            .Except(ignoradas, StringComparer.Ordinal)
            .ToList();

        await using var conexaoProducao =
            new NpgsqlConnection(producao);

        await using var conexaoHomologacao =
            new NpgsqlConnection(homologacao);

        await conexaoProducao.OpenAsync(cancellationToken);
        await conexaoHomologacao.OpenAsync(cancellationToken);

        await using var transacaoProducao =
            await conexaoProducao.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);

        await using var transacaoHomologacao =
            await conexaoHomologacao.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);

        await TornarSomenteLeituraAsync(
            conexaoProducao,
            transacaoProducao,
            cancellationToken);

        await TornarSomenteLeituraAsync(
            conexaoHomologacao,
            transacaoHomologacao,
            cancellationToken);

        await DefinirFormatosEstaveisAsync(
            conexaoProducao,
            transacaoProducao,
            cancellationToken);

        await DefinirFormatosEstaveisAsync(
            conexaoHomologacao,
            transacaoHomologacao,
            cancellationToken);

        var qualificada =
            $"{Identificador(schema)}.{Identificador(tabela)}";

        var nomeChaves = chaves
            .Select(Identificador)
            .ToArray();

        var expressoesColunas = colunasComparadas
            .Select(c =>
                colunasProducao[c].TipoDados == "money"
                    ? $"{Identificador(c)}::numeric"
                    : Identificador(c))
            .ToArray();

        var diferencasPorColuna =
            colunasComparadas.ToDictionary(
                c => c,
                _ => 0L,
                StringComparer.Ordinal);

        var amostrasProducao = new List<string>();
        var amostrasHomologacao = new List<string>();

        var registrosDivergentesDetalhados =
            new List<RegistroDivergenteDados>();

        long totalProducao = 0;
        long totalHomologacao = 0;
        long somenteProducao = 0;
        long somenteHomologacao = 0;
        long registrosDivergentes = 0;

        object?[]? cursor = null;

        // Primeira passagem: percorre Produção e compara os registros
        // correspondentes em Homologação.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lote = await LerLoteAsync(
                conexaoProducao,
                transacaoProducao,
                qualificada,
                nomeChaves,
                expressoesColunas,
                cursor,
                tamanhoLote,
                cancellationToken);

            if (lote.Count == 0)
                break;

            totalProducao += lote.Count;
            cursor = lote[^1].Chaves;

            var encontrados = await LerPorChavesAsync(
                conexaoHomologacao,
                transacaoHomologacao,
                qualificada,
                nomeChaves,
                expressoesColunas,
                lote,
                cancellationToken);

            var porChave = encontrados.ToDictionary(
                r => r.ChaveCanonica,
                StringComparer.Ordinal);

            foreach (var registro in lote)
            {
                if (!porChave.TryGetValue(
                        registro.ChaveCanonica,
                        out var correspondente))
                {
                    somenteProducao++;

                    AdicionarAmostra(
                        amostrasProducao,
                        registro.ChaveCanonica);

                    continue;
                }

                var colunasDivergentes = new List<string>();

                for (var i = 0; i < colunasComparadas.Count; i++)
                {
                    if (ValoresIguais(
                            registro.Valores[i],
                            correspondente.Valores[i]))
                    {
                        continue;
                    }

                    diferencasPorColuna[colunasComparadas[i]]++;
                    colunasDivergentes.Add(colunasComparadas[i]);
                }

                // Conta o registro apenas uma vez, mesmo que várias
                // colunas apresentem divergências.
                if (colunasDivergentes.Count == 0)
                    continue;

                registrosDivergentes++;

                // Retorna detalhes de, no máximo, 50 registros,
                // preservando a contagem total da tabela.
                if (registrosDivergentesDetalhados.Count >=
                    MaxRegistrosDivergentes)
                {
                    continue;
                }

                var chavePrimaria =
                    new Dictionary<string, object?>(
                        StringComparer.Ordinal);

                for (var i = 0; i < chaves.Count; i++)
                {
                    chavePrimaria[chaves[i]] =
                        registro.Chaves[i];
                }

                registrosDivergentesDetalhados.Add(
                    new RegistroDivergenteDados
                    {
                        ChavePrimaria = chavePrimaria,
                        ColunasDivergentes = colunasDivergentes
                    });
            }
        }

        // Segunda passagem: conta os registros de Homologação
        // que não existem em Produção.
        cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lote = await LerLoteAsync(
                conexaoHomologacao,
                transacaoHomologacao,
                qualificada,
                nomeChaves,
                [],
                cursor,
                tamanhoLote,
                cancellationToken);

            if (lote.Count == 0)
                break;

            totalHomologacao += lote.Count;
            cursor = lote[^1].Chaves;

            var encontrados = await LerPorChavesAsync(
                conexaoProducao,
                transacaoProducao,
                qualificada,
                nomeChaves,
                [],
                lote,
                cancellationToken);

            var chavesProducaoExistentes = encontrados
                .Select(r => r.ChaveCanonica)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var registro in lote)
            {
                if (chavesProducaoExistentes.Contains(
                        registro.ChaveCanonica))
                {
                    continue;
                }

                somenteHomologacao++;

                AdicionarAmostra(
                    amostrasHomologacao,
                    registro.ChaveCanonica);
            }
        }

        return new ResultadoComparacaoDados
        {
            Schema = schema,
            Tabela = tabela,
            Comparavel = true,
            TamanhoLote = tamanhoLote,

            RegistrosProducao = totalProducao,
            RegistrosHomologacao = totalHomologacao,

            RegistrosApenasNaProducao = somenteProducao,
            RegistrosApenasNaHomologacao = somenteHomologacao,

            RegistrosComDiferencas = registrosDivergentes,

            DiferencasPorColuna = diferencasPorColuna
                .Where(p => p.Value > 0)
                .ToDictionary(
                    p => p.Key,
                    p => p.Value,
                    StringComparer.Ordinal),

            // NOVO: devolve as chaves primárias e as colunas
            // divergentes para a API e o Angular.
            RegistrosDivergentes = registrosDivergentesDetalhados,

            ColunasIgnoradasPorTipo = ignoradas,

            AmostrasChaveAnonimizadasApenasNaProducao =
                amostrasProducao,

            AmostrasChaveAnonimizadasApenasNaHomologacao =
                amostrasHomologacao
        };
    }

    private static ResultadoComparacaoDados NaoComparavel(
        string schema,
        string tabela,
        int tamanhoLote,
        string motivo) => new()
        {
            Schema = schema,
            Tabela = tabela,
            Comparavel = false,
            MotivoNaoComparavel = motivo,
            TamanhoLote = tamanhoLote
        };

    private static async Task TornarSomenteLeituraAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        CancellationToken cancellationToken)
    {
        await using var comando = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY",
            conexao,
            transacao);

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DefinirFormatosEstaveisAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        CancellationToken cancellationToken)
    {
        const string sql =
            "SELECT " +
            "set_config('TimeZone', 'UTC', true), " +
            "set_config('DateStyle', 'ISO, YMD', true), " +
            "set_config('IntervalStyle', 'iso_8601', true), " +
            "set_config('bytea_output', 'hex', true)";

        await using var comando = new NpgsqlCommand(
            sql,
            conexao,
            transacao);

        await comando.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<List<Registro>> LerLoteAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string tabela,
        string[] chaves,
        string[] colunas,
        object?[]? cursor,
        int tamanhoLote,
        CancellationToken cancellationToken)
    {
        var campos = new List<string>();

        campos.AddRange(
            chaves.Select((c, i) =>
                $"{c} AS {Identificador($"__k{i}")}"));

        campos.Add(
            $"jsonb_build_array({string.Join(", ", chaves)})::text " +
            $"AS {Identificador("__key")}");

        campos.AddRange(
            colunas.Select((c, i) =>
                $"to_jsonb({c})::text AS {Identificador($"__v{i}")}"));

        var where = cursor is null
            ? string.Empty
            : $"WHERE ROW({string.Join(", ", chaves)}) > " +
              $"ROW({string.Join(", ", chaves.Select(
                  (_, i) => $"@cursor{i}"))})";

        var sql =
            $"SELECT {string.Join(", ", campos)} " +
            $"FROM {tabela} {where} " +
            $"ORDER BY {string.Join(", ", chaves)} " +
            "LIMIT @limite";

        await using var comando = new NpgsqlCommand(
            sql,
            conexao,
            transacao)
        {
            CommandTimeout = 90
        };

        if (cursor is not null)
        {
            for (var i = 0; i < cursor.Length; i++)
            {
                comando.Parameters.AddWithValue(
                    $"cursor{i}",
                    cursor[i]!);
            }
        }

        comando.Parameters.AddWithValue(
            "limite",
            tamanhoLote);

        return await LerRegistrosAsync(
            comando,
            chaves.Length,
            colunas.Length,
            cancellationToken);
    }

    private static async Task<List<Registro>> LerPorChavesAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string tabela,
        string[] chaves,
        string[] colunas,
        IReadOnlyList<Registro> lote,
        CancellationToken cancellationToken)
    {
        if (lote.Count == 0)
            return [];

        var campos = new List<string>();

        campos.AddRange(
            chaves.Select((c, i) =>
                $"{c} AS {Identificador($"__k{i}")}"));

        campos.Add(
            $"jsonb_build_array({string.Join(", ", chaves)})::text " +
            $"AS {Identificador("__key")}");

        campos.AddRange(
            colunas.Select((c, i) =>
                $"to_jsonb({c})::text AS {Identificador($"__v{i}")}"));

        var termos = new List<string>();

        await using var comando = new NpgsqlCommand
        {
            Connection = conexao,
            Transaction = transacao,
            CommandTimeout = 90
        };

        for (var r = 0; r < lote.Count; r++)
        {
            var partes = new List<string>();

            for (var k = 0; k < chaves.Length; k++)
            {
                var nome = $"k{r}_{k}";

                partes.Add($"{chaves[k]} = @{nome}");

                comando.Parameters.AddWithValue(
                    nome,
                    lote[r].Chaves[k]!);
            }

            termos.Add($"({string.Join(" AND ", partes)})");
        }

        comando.CommandText =
            $"SELECT {string.Join(", ", campos)} " +
            $"FROM {tabela} " +
            $"WHERE {string.Join(" OR ", termos)}";

        return await LerRegistrosAsync(
            comando,
            chaves.Length,
            colunas.Length,
            cancellationToken);
    }

    private static async Task<List<Registro>> LerRegistrosAsync(
        NpgsqlCommand comando,
        int quantidadeChaves,
        int quantidadeValores,
        CancellationToken cancellationToken)
    {
        var registros = new List<Registro>();

        await using var reader =
            await comando.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var chaves = new object?[quantidadeChaves];

            for (var i = 0; i < quantidadeChaves; i++)
            {
                chaves[i] = reader.GetValue(i);
            }

            var chaveCanonica =
                CanonizarTextoJson(
                    reader.GetString(quantidadeChaves));

            var valores = new string?[quantidadeValores];

            for (var i = 0; i < quantidadeValores; i++)
            {
                var ordinal = quantidadeChaves + 1 + i;

                valores[i] = reader.IsDBNull(ordinal)
                    ? "null"
                    : CanonizarTextoJson(
                        reader.GetString(ordinal));
            }

            registros.Add(
                new Registro(
                    chaves,
                    chaveCanonica,
                    valores));
        }

        return registros;
    }

    private static bool ValoresIguais(
        string? esquerda,
        string? direita) =>
        string.Equals(
            esquerda,
            direita,
            StringComparison.Ordinal);

    private static string Canonizar(JsonElement valor) =>
        valor.ValueKind switch
        {
            JsonValueKind.Object =>
                "{" + string.Join(
                    ",",
                    valor.EnumerateObject()
                        .OrderBy(
                            p => p.Name,
                            StringComparer.Ordinal)
                        .Select(
                            p => JsonSerializer.Serialize(p.Name) +
                                 ":" + Canonizar(p.Value))) + "}",

            JsonValueKind.Array =>
                "[" + string.Join(
                    ",",
                    valor.EnumerateArray().Select(Canonizar)) + "]",

            JsonValueKind.String =>
                JsonSerializer.Serialize(valor.GetString()),

            JsonValueKind.Number =>
                CanonizarNumero(valor.GetRawText()),

            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "null"
        };

    private static string CanonizarTextoJson(string json)
    {
        using var documento = JsonDocument.Parse(json);

        return Canonizar(documento.RootElement);
    }

    private static string CanonizarNumero(string texto)
    {
        var partes = texto.ToLowerInvariant().Split('e', 2);
        var mantissa = partes[0];

        var expoente = partes.Length == 2
            ? int.Parse(
                partes[1],
                CultureInfo.InvariantCulture)
            : 0;

        var negativo = mantissa.StartsWith('-');

        if (negativo || mantissa.StartsWith('+'))
        {
            mantissa = mantissa[1..];
        }

        var ponto = mantissa.IndexOf('.');

        if (ponto >= 0)
        {
            expoente -= mantissa.Length - ponto - 1;
            mantissa = mantissa.Remove(ponto, 1);
        }

        mantissa = mantissa.TrimStart('0');

        if (mantissa.Length == 0)
            return "0";

        while (mantissa.EndsWith('0'))
        {
            mantissa = mantissa[..^1];
            expoente++;
        }

        return $"{(negativo ? "-" : string.Empty)}{mantissa}e{expoente}";
    }

    private static void AdicionarAmostra(
        List<string> amostras,
        string chaveCanonica)
    {
        if (amostras.Count >= MaxAmostras)
            return;

        amostras.Add(
            Convert.ToHexString(
                HMACSHA256.HashData(
                    ChaveHashProcesso,
                    Encoding.UTF8.GetBytes(chaveCanonica))));
    }

    private static string Identificador(string valor) =>
        $"\"{valor.Replace("\"", "\"\"")}\"";

    private sealed record Registro(
        object?[] Chaves,
        string ChaveCanonica,
        string?[] Valores);
}
