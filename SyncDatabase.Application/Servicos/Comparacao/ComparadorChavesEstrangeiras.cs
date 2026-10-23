using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorChavesEstrangeiras : IComparadorChavesEstrangeiras
{
    public List<DiferencaChaveEstrangeira> Comparar(
        IReadOnlyCollection<ChaveEstrangeiraBanco> chavesProducao,
        IReadOnlyCollection<ChaveEstrangeiraBanco> chavesHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas)
    {
        var producao = chavesProducao
            .Where(chave => tabelasCompartilhadas.Contains(
                NomeTabela(chave.Schema, chave.Tabela)))
            .ToList();
        var homologacao = chavesHomologacao
            .Where(chave => tabelasCompartilhadas.Contains(
                NomeTabela(chave.Schema, chave.Tabela)))
            .ToList();

        var usadasNaHomologacao = new HashSet<int>();
        var diferencas = new List<DiferencaChaveEstrangeira>();

        foreach (var chaveProducao in producao)
        {
            var indiceCorrespondente = -1;
            for (var i = 0; i < homologacao.Count; i++)
            {
                var candidata = homologacao[i];
                if (!usadasNaHomologacao.Contains(i) &&
                    MesmaTabela(candidata, chaveProducao) &&
                    MesmasColunas(candidata.Colunas, chaveProducao.Colunas))
                {
                    indiceCorrespondente = i;
                    break;
                }
            }

            if (indiceCorrespondente < 0)
            {
                diferencas.Add(CriarDiferenca(
                    chaveProducao,
                    null,
                    "ChaveEstrangeiraAusenteNaHomologacao",
                    Descrever(chaveProducao),
                    null));
                continue;
            }

            usadasNaHomologacao.Add(indiceCorrespondente);
            var chaveHomologacao = homologacao[indiceCorrespondente];
            CompararPropriedade(diferencas, chaveProducao, chaveHomologacao,
                "TabelaReferenciada", Referencia(chaveProducao), Referencia(chaveHomologacao));
            CompararPropriedade(diferencas, chaveProducao, chaveHomologacao,
                "ColunasReferenciadas", Lista(chaveProducao.ColunasReferenciadas), Lista(chaveHomologacao.ColunasReferenciadas));
            CompararPropriedade(diferencas, chaveProducao, chaveHomologacao,
                "RegraOnDelete", chaveProducao.RegraOnDelete, chaveHomologacao.RegraOnDelete);
            CompararPropriedade(diferencas, chaveProducao, chaveHomologacao,
                "RegraOnUpdate", chaveProducao.RegraOnUpdate, chaveHomologacao.RegraOnUpdate);
        }

        for (var i = 0; i < homologacao.Count; i++)
        {
            if (!usadasNaHomologacao.Contains(i))
            {
                diferencas.Add(CriarDiferenca(
                    null,
                    homologacao[i],
                    "ChaveEstrangeiraAusenteNaProducao",
                    null,
                    Descrever(homologacao[i])));
            }
        }

        return diferencas;
    }

    private static void CompararPropriedade(
        List<DiferencaChaveEstrangeira> diferencas,
        ChaveEstrangeiraBanco producao,
        ChaveEstrangeiraBanco homologacao,
        string tipo,
        string valorProducao,
        string valorHomologacao)
    {
        if (!string.Equals(valorProducao, valorHomologacao, StringComparison.Ordinal))
        {
            diferencas.Add(CriarDiferenca(
                producao,
                homologacao,
                tipo,
                valorProducao,
                valorHomologacao));
        }
    }

    private static DiferencaChaveEstrangeira CriarDiferenca(
        ChaveEstrangeiraBanco? producao,
        ChaveEstrangeiraBanco? homologacao,
        string tipo,
        string? valorProducao,
        string? valorHomologacao)
    {
        var referencia = producao ?? homologacao!;
        return new DiferencaChaveEstrangeira
        {
            Schema = referencia.Schema,
            Tabela = referencia.Tabela,
            TipoDiferenca = tipo,
            NomeConstraintProducao = producao?.NomeConstraint,
            NomeConstraintHomologacao = homologacao?.NomeConstraint,
            ValorProducao = valorProducao,
            ValorHomologacao = valorHomologacao
        };
    }

    private static bool MesmasColunas(
        IReadOnlyList<string> primeira,
        IReadOnlyList<string> segunda) =>
        primeira.SequenceEqual(segunda, StringComparer.OrdinalIgnoreCase);

    private static string Referencia(ChaveEstrangeiraBanco chave) =>
        NomeTabela(chave.SchemaReferenciado, chave.TabelaReferenciada);

    private static string NomeTabela(string schema, string tabela) => $"{schema}.{tabela}";

    private static bool MesmaTabela(
        ChaveEstrangeiraBanco primeira,
        ChaveEstrangeiraBanco segunda) =>
        string.Equals(primeira.Schema, segunda.Schema, StringComparison.Ordinal) &&
        string.Equals(primeira.Tabela, segunda.Tabela, StringComparison.Ordinal);

    private static string Lista(IEnumerable<string> valores) => string.Join(", ", valores);

    private static string Descrever(ChaveEstrangeiraBanco chave) =>
        $"({Lista(chave.Colunas)}) -> {Referencia(chave)}({Lista(chave.ColunasReferenciadas)}), " +
        $"ON DELETE {chave.RegraOnDelete}, ON UPDATE {chave.RegraOnUpdate}";
}
