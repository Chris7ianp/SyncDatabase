
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorColunas : IComparadorColunas
{
    public List<DiferencaColuna> Comparar(
     IReadOnlyCollection<ColunaBanco> colunasProducao,
     IReadOnlyCollection<ColunaBanco> colunasHomologacao)
    {
        var producao = colunasProducao.ToDictionary(
            c => (c.Schema, c.Tabela, c.Coluna));

        var homologacao = colunasHomologacao.ToDictionary(
            c => (c.Schema, c.Tabela, c.Coluna));

        var diferencas = new List<DiferencaColuna>();

        foreach (var (chave, colunaProducao) in producao)
        {
            if (!homologacao.TryGetValue(chave, out var colunaHomologacao))
            {
                diferencas.Add(CriarDiferenca(
                    colunaProducao,
                    "ColunaAusenteNaHomologacao",
                    colunaProducao.TipoDados,
                    null));

                continue;
            }

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "TipoDados", colunaProducao.TipoDados,
                colunaHomologacao.TipoDados);

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "AceitaNulo", colunaProducao.AceitaNulo.ToString(),
                colunaHomologacao.AceitaNulo.ToString());

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "ValorPadrao", colunaProducao.ValorPadrao,
                colunaHomologacao.ValorPadrao);

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "ComprimentoMaximo",
                colunaProducao.ComprimentoMaximo?.ToString(),
                colunaHomologacao.ComprimentoMaximo?.ToString());

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "PrecisaoNumerica",
                colunaProducao.PrecisaoNumerica?.ToString(),
                colunaHomologacao.PrecisaoNumerica?.ToString());

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "EscalaNumerica",
                colunaProducao.EscalaNumerica?.ToString(),
                colunaHomologacao.EscalaNumerica?.ToString());

            CompararPropriedade(
                diferencas, colunaProducao, colunaHomologacao,
                "Posicao", colunaProducao.Posicao.ToString(),
                colunaHomologacao.Posicao.ToString());
        }

        foreach (var (chave, colunaHomologacao) in homologacao)
        {
            if (!producao.ContainsKey(chave))
            {
                diferencas.Add(CriarDiferenca(
                    colunaHomologacao,
                    "ColunaAusenteNaProducao",
                    null,
                    colunaHomologacao.TipoDados));
            }
        }

        return diferencas;
    }

    private static void CompararPropriedade(
        List<DiferencaColuna> diferencas,
        ColunaBanco producao,
        ColunaBanco homologacao,
        string propriedade,
        string? valorProducao,
        string? valorHomologacao)
    {
        if (valorProducao == valorHomologacao)
            return;

        diferencas.Add(CriarDiferenca(
            producao,
            propriedade,
            valorProducao,
            valorHomologacao));
    }

    private static DiferencaColuna CriarDiferenca(
        ColunaBanco coluna,
        string tipoDiferenca,
        string? valorProducao,
        string? valorHomologacao)
    {
        return new DiferencaColuna
        {
            Schema = coluna.Schema,
            Tabela = coluna.Tabela,
            Coluna = coluna.Coluna,
            TipoDiferenca = tipoDiferenca,
            ValorProducao = valorProducao,
            ValorHomologacao = valorHomologacao
        };
    }
}
