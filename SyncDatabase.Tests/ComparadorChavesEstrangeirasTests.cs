using Xunit;
using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Servicos.Comparacao;

namespace SyncDatabase.Tests;

public sealed class ComparadorChavesEstrangeirasTests
{
    private readonly ComparadorChavesEstrangeiras _comparador = new();

    [Fact]
    public void Comparar_RelacionamentoAusenteNaHomologacao_IdentificaApenasOrigemExistente()
    {
        var producao = new[] { CriarChave("fk_pedido_cliente", "cliente_id") };

        var diferencas = _comparador.Comparar(
            producao,
            [],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal));

        var diferenca = Assert.Single(diferencas);
        Assert.Equal("ChaveEstrangeiraAusenteNaHomologacao", diferenca.TipoDiferenca);
        Assert.Equal("fk_pedido_cliente", diferenca.NomeConstraintProducao);
        Assert.Null(diferenca.NomeConstraintHomologacao);
    }

    [Fact]
    public void Comparar_RegrasDiferentes_EmiteDiferencaComValoresDosDoisAmbientes()
    {
        var producao = CriarChave("fk_pedido_cliente", "cliente_id");
        var homologacao = new ChaveEstrangeiraBanco
        {
            Schema = "public",
            Tabela = "pedidos",
            NomeConstraint = "outro_nome",
            Colunas = ["cliente_id"],
            SchemaReferenciado = "public",
            TabelaReferenciada = "clientes",
            ColunasReferenciadas = ["id"],
            RegraOnDelete = "NO ACTION",
            RegraOnUpdate = "NO ACTION"
        };

        var diferencas = _comparador.Comparar(
            [producao],
            [homologacao],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal));

        var diferenca = Assert.Single(diferencas);
        Assert.Equal("RegraOnDelete", diferenca.TipoDiferenca);
        Assert.Equal("CASCADE", diferenca.ValorProducao);
        Assert.Equal("NO ACTION", diferenca.ValorHomologacao);
    }

    [Fact]
    public void Comparar_TabelaDeOrigemExclusiva_NaoDuplicaDiferencaDeTabela()
    {
        var diferencas = _comparador.Comparar(
            [CriarChave("fk_exclusiva", "cliente_id")],
            [],
            new HashSet<string>(["public.outra_tabela"], StringComparer.Ordinal));

        Assert.Empty(diferencas);
    }

    private static ChaveEstrangeiraBanco CriarChave(string nome, string coluna) => new()
    {
        Schema = "public",
        Tabela = "pedidos",
        NomeConstraint = nome,
        Colunas = [coluna],
        SchemaReferenciado = "public",
        TabelaReferenciada = "clientes",
        ColunasReferenciadas = ["id"],
        RegraOnDelete = "CASCADE",
        RegraOnUpdate = "NO ACTION"
    };
}
