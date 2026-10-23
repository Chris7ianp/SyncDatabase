using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorObjetosAdicionais : IComparadorObjetosAdicionais
{
    public List<DiferencaObjetoBanco> Comparar(
        IReadOnlyCollection<ObjetoBanco> objetosProducao,
        IReadOnlyCollection<ObjetoBanco> objetosHomologacao)
    {
        var homologacao = objetosHomologacao.ToDictionary(Chave);
        var diferencas = new List<DiferencaObjetoBanco>();

        foreach (var objeto in objetosProducao)
        {
            var chave = Chave(objeto);
            if (!homologacao.Remove(chave, out var correspondente))
            {
                diferencas.Add(Criar(objeto, null, "AusenteNaHomologacao"));
                continue;
            }

            if (!string.Equals(Normalizar(objeto.Definicao),
                    Normalizar(correspondente.Definicao), StringComparison.Ordinal))
            {
                diferencas.Add(Criar(objeto, correspondente, "DefinicaoDiferente"));
            }
        }

        diferencas.AddRange(homologacao.Values.Select(
            objeto => Criar(null, objeto, "AusenteNaProducao")));
        return diferencas;
    }

    private static (string Categoria, string Schema, string Nome, string? Pai, string Identificador) Chave(
        ObjetoBanco objeto) =>
        (objeto.Categoria, objeto.Schema, objeto.Nome, objeto.ObjetoPai, objeto.Identificador);

    private static DiferencaObjetoBanco Criar(
        ObjetoBanco? producao,
        ObjetoBanco? homologacao,
        string tipo)
    {
        var objeto = producao ?? homologacao!;
        return new DiferencaObjetoBanco
        {
            Categoria = objeto.Categoria,
            Schema = objeto.Schema,
            Nome = objeto.Nome,
            ObjetoPai = objeto.ObjetoPai,
            TipoDiferenca = tipo,
            DefinicaoProducao = producao?.Definicao,
            DefinicaoHomologacao = homologacao?.Definicao
        };
    }

    private static string Normalizar(string definicao) =>
        string.Join(" ", definicao.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
