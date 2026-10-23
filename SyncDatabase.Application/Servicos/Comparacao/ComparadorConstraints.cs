using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Interfaces.Comparacao;

namespace SyncDatabase.Application.Servicos.Comparacao;

public sealed class ComparadorConstraints : IComparadorConstraints
{
    public List<DiferencaConstraint> Comparar(
        IReadOnlyCollection<ConstraintBanco> constraintsProducao,
        IReadOnlyCollection<ConstraintBanco> constraintsHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas)
    {
        var producao = Filtrar(constraintsProducao, tabelasCompartilhadas);
        var homologacao = Filtrar(constraintsHomologacao, tabelasCompartilhadas);
        var usadas = new HashSet<int>();
        var diferencas = new List<DiferencaConstraint>();

        foreach (var constraint in producao)
        {
            var indice = -1;
            for (var i = 0; i < homologacao.Count; i++)
            {
                if (!usadas.Contains(i) && MesmoObjeto(constraint, homologacao[i]))
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
            {
                diferencas.Add(Criar(constraint, null, "AusenteNaHomologacao", Definicao(constraint), null));
                continue;
            }

            usadas.Add(indice);
            var destino = homologacao[indice];
            Comparar(diferencas, constraint, destino, "Definicao", constraint.Definicao, destino.Definicao);
            Comparar(diferencas, constraint, destino, "Diferida", constraint.Diferida.ToString(), destino.Diferida.ToString());
            Comparar(diferencas, constraint, destino, "InicialmenteDiferida", constraint.InicialmenteDiferida.ToString(), destino.InicialmenteDiferida.ToString());
            Comparar(diferencas, constraint, destino, "Validada", constraint.Validada.ToString(), destino.Validada.ToString());
            Comparar(diferencas, constraint, destino, "NulosNaoDistintos", constraint.NulosNaoDistintos.ToString(), destino.NulosNaoDistintos.ToString());
        }

        for (var i = 0; i < homologacao.Count; i++)
        {
            if (!usadas.Contains(i))
                diferencas.Add(Criar(null, homologacao[i], "AusenteNaProducao", null, Definicao(homologacao[i])));
        }

        return diferencas;
    }

    private static List<ConstraintBanco> Filtrar(
        IReadOnlyCollection<ConstraintBanco> constraints,
        IReadOnlySet<string> tabelas) => constraints
        .Where(c => tabelas.Contains($"{c.Schema}.{c.Tabela}"))
        .ToList();

    private static bool MesmoObjeto(ConstraintBanco a, ConstraintBanco b) =>
        string.Equals(a.Schema, b.Schema, StringComparison.Ordinal) &&
        string.Equals(a.Tabela, b.Tabela, StringComparison.Ordinal) &&
        string.Equals(a.Tipo, b.Tipo, StringComparison.Ordinal) &&
        (a.Tipo == "UNIQUE"
            ? a.Colunas.SequenceEqual(b.Colunas, StringComparer.Ordinal)
            : string.Equals(Normalizar(a.Definicao), Normalizar(b.Definicao), StringComparison.Ordinal));

    private static void Comparar(List<DiferencaConstraint> saida,
        ConstraintBanco producao, ConstraintBanco homologacao,
        string tipo, string valorProducao, string valorHomologacao)
    {
        if (!string.Equals(Normalizar(valorProducao), Normalizar(valorHomologacao), StringComparison.Ordinal))
            saida.Add(Criar(producao, homologacao, tipo, valorProducao, valorHomologacao));
    }

    private static DiferencaConstraint Criar(ConstraintBanco? producao,
        ConstraintBanco? homologacao, string tipo, string? valorProducao, string? valorHomologacao)
    {
        var constraint = producao ?? homologacao!;
        return new DiferencaConstraint
        {
            Schema = constraint.Schema,
            Tabela = constraint.Tabela,
            Categoria = constraint.Tipo,
            TipoDiferenca = tipo,
            NomeProducao = producao?.Nome,
            NomeHomologacao = homologacao?.Nome,
            ValorProducao = valorProducao,
            ValorHomologacao = valorHomologacao
        };
    }

    private static string Definicao(ConstraintBanco c) =>
        c.Tipo == "UNIQUE" ? $"UNIQUE ({string.Join(", ", c.Colunas)})" : c.Definicao;

    private static string Normalizar(string? valor) =>
        string.Join(" ", (valor ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
