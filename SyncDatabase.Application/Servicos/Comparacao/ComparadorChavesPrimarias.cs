
using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorChavesPrimarias
    : IComparadorChavesPrimarias
{
    public List<DiferencaChavePrimaria> Comparar(
        IReadOnlyCollection<ChavePrimariaBanco> chavesProducao,
        IReadOnlyCollection<ChavePrimariaBanco> chavesHomologacao)
    {
        var diferencas = new List<DiferencaChavePrimaria>();

        // Verifica cada chave existente em Produção.
        foreach (var chaveProducao in chavesProducao)
        {
            var chaveHomologacao = chavesHomologacao.FirstOrDefault(
                h => MesmaTabela(h, chaveProducao));

            if (chaveHomologacao is null)
            {
                diferencas.Add(new DiferencaChavePrimaria
                {
                    Schema = chaveProducao.Schema,
                    Tabela = chaveProducao.Tabela,
                    TipoDiferenca =
                        "ChavePrimariaAusenteNaHomologacao",
                    NomeConstraintProducao =
                        chaveProducao.NomeConstraint,
                    NomeConstraintHomologacao = null,
                    ColunasProducao = [.. chaveProducao.Colunas],
                    ColunasHomologacao = []
                });

                continue;
            }

            // Compara as colunas e sua ordem.
            var colunasIguais = chaveProducao.Colunas.SequenceEqual(
                chaveHomologacao.Colunas,
                StringComparer.Ordinal);

            if (!colunasIguais)
            {
                diferencas.Add(new DiferencaChavePrimaria
                {
                    Schema = chaveProducao.Schema,
                    Tabela = chaveProducao.Tabela,
                    TipoDiferenca =
                        "ColunasChavePrimariaDiferentes",
                    NomeConstraintProducao =
                        chaveProducao.NomeConstraint,
                    NomeConstraintHomologacao =
                        chaveHomologacao.NomeConstraint,
                    ColunasProducao = [.. chaveProducao.Colunas],
                    ColunasHomologacao = [.. chaveHomologacao.Colunas]
                });
            }
        }

        // Verifica as chaves que existem somente na Homologação.
        foreach (var chaveHomologacao in chavesHomologacao)
        {
            var existeNaProducao = chavesProducao.Any(
                p => MesmaTabela(p, chaveHomologacao));

            if (!existeNaProducao)
            {
                diferencas.Add(new DiferencaChavePrimaria
                {
                    Schema = chaveHomologacao.Schema,
                    Tabela = chaveHomologacao.Tabela,
                    TipoDiferenca =
                        "ChavePrimariaAusenteNaProducao",
                    NomeConstraintProducao = null,
                    NomeConstraintHomologacao =
                        chaveHomologacao.NomeConstraint,
                    ColunasProducao = [],
                    ColunasHomologacao =
                        [.. chaveHomologacao.Colunas]
                });
            }
        }

        return diferencas;
    }

    private static bool MesmaTabela(
        ChavePrimariaBanco primeira,
        ChavePrimariaBanco segunda)
    {
        return string.Equals(
                   primeira.Schema,
                   segunda.Schema,
                   StringComparison.Ordinal)
               &&
               string.Equals(
                   primeira.Tabela,
                   segunda.Tabela,
                   StringComparison.Ordinal);
    }
}
