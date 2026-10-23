using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorIndices : IComparadorIndices
{
    public List<DiferencaIndice> Comparar(
        IReadOnlyCollection<IndiceBanco> indicesProducao,
        IReadOnlyCollection<IndiceBanco> indicesHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas)
    {
        var producao = indicesProducao.Where(i => tabelasCompartilhadas.Contains(NomeTabela(i))).ToList();
        var homologacao = indicesHomologacao.Where(i => tabelasCompartilhadas.Contains(NomeTabela(i))).ToList();
        var homologacaoUsada = new HashSet<int>();
        var diferencas = new List<DiferencaIndice>();

        foreach (var indice in producao)
        {
            var correspondente = -1;
            var melhorPontuacao = -1;
            for (var i = 0; i < homologacao.Count; i++)
            {
                if (!homologacaoUsada.Contains(i) &&
                    MesmaTabela(indice, homologacao[i]) &&
                    MesmoFormatoChaves(indice, homologacao[i]))
                {
                    var pontuacao = PontuarCorrespondencia(indice, homologacao[i]);
                    if (pontuacao > melhorPontuacao)
                    {
                        correspondente = i;
                        melhorPontuacao = pontuacao;
                    }
                }
            }

            if (correspondente < 0)
            {
                diferencas.Add(Criar(indice, null, "IndiceAusenteNaHomologacao", Descrever(indice), null));
                continue;
            }

            homologacaoUsada.Add(correspondente);
            var outro = homologacao[correspondente];
            CompararValor(diferencas, indice, outro, "ColunasIncluidas", Lista(indice.ColunasIncluidas), Lista(outro.ColunasIncluidas));
            CompararValor(diferencas, indice, outro, "Unico", indice.Unico.ToString(), outro.Unico.ToString());
            CompararValor(diferencas, indice, outro, "MetodoAcesso", indice.MetodoAcesso, outro.MetodoAcesso);
            CompararValor(diferencas, indice, outro, "Predicado", indice.Predicado, outro.Predicado);
            CompararValor(diferencas, indice, outro, "NulosNaoDistintos", indice.NulosNaoDistintos.ToString(), outro.NulosNaoDistintos.ToString());
            CompararValor(diferencas, indice, outro, "Valido", indice.Valido.ToString(), outro.Valido.ToString());
            CompararValor(diferencas, indice, outro, "Pronto", indice.Pronto.ToString(), outro.Pronto.ToString());
        }

        for (var i = 0; i < homologacao.Count; i++)
        {
            if (!homologacaoUsada.Contains(i))
                diferencas.Add(Criar(null, homologacao[i], "IndiceAusenteNaProducao", null, Descrever(homologacao[i])));
        }

        return diferencas;
    }

    private static void CompararValor(List<DiferencaIndice> diferencas, IndiceBanco producao,
        IndiceBanco homologacao, string tipo, string? valorProducao, string? valorHomologacao)
    {
        if (!string.Equals(valorProducao, valorHomologacao, StringComparison.Ordinal))
            diferencas.Add(Criar(producao, homologacao, tipo, valorProducao, valorHomologacao));
    }

    private static DiferencaIndice Criar(IndiceBanco? producao, IndiceBanco? homologacao,
        string tipo, string? valorProducao, string? valorHomologacao)
    {
        var indice = producao ?? homologacao!;
        return new DiferencaIndice
        {
            Schema = indice.Schema,
            Tabela = indice.Tabela,
            TipoDiferenca = tipo,
            NomeIndiceProducao = producao?.Nome,
            NomeIndiceHomologacao = homologacao?.Nome,
            ValorProducao = valorProducao,
            ValorHomologacao = valorHomologacao
        };
    }

    private static bool MesmaTabela(IndiceBanco a, IndiceBanco b) =>
        string.Equals(a.Schema, b.Schema, StringComparison.Ordinal) &&
        string.Equals(a.Tabela, b.Tabela, StringComparison.Ordinal);

    private static bool MesmoFormatoChaves(IndiceBanco a, IndiceBanco b) =>
        a.Chaves.SequenceEqual(b.Chaves, StringComparer.Ordinal);

    private static int PontuarCorrespondencia(IndiceBanco a, IndiceBanco b)
    {
        var pontuacao = 0;
        if (a.ColunasIncluidas.SequenceEqual(b.ColunasIncluidas, StringComparer.Ordinal)) pontuacao++;
        if (a.Unico == b.Unico) pontuacao++;
        if (string.Equals(a.MetodoAcesso, b.MetodoAcesso, StringComparison.Ordinal)) pontuacao++;
        if (string.Equals(a.Predicado, b.Predicado, StringComparison.Ordinal)) pontuacao++;
        if (a.NulosNaoDistintos == b.NulosNaoDistintos) pontuacao++;
        if (a.Valido == b.Valido) pontuacao++;
        if (a.Pronto == b.Pronto) pontuacao++;
        return pontuacao;
    }

    private static string NomeTabela(IndiceBanco indice) => $"{indice.Schema}.{indice.Tabela}";
    private static string Lista(IEnumerable<string> valores) => string.Join(", ", valores);
    private static string Descrever(IndiceBanco i) =>
        $"({Lista(i.Chaves)}), método {i.MetodoAcesso}, único={i.Unico}, " +
        $"incluídas=({Lista(i.ColunasIncluidas)}), predicado={i.Predicado ?? "<nenhum>"}";
}
