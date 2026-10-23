using SyncDatabase.Application.DTOs.Estrutura;
using SyncDatabase.Application.Servicos.Comparacao;
using Xunit;

namespace SyncDatabase.Tests;

public sealed class ComparadoresIndicesConstraintsTests
{
    [Fact]
    public void CompararColunas_TipoDiferente_RetornaValoresDeAmbosAmbientes()
    {
        var comparador = new ComparadorColunas();
        var producao = new ColunaBanco
        {
            Schema = "public", Tabela = "pedidos", Coluna = "numero",
            TipoDados = "character varying", ComprimentoMaximo = 100
        };
        var homologacao = new ColunaBanco
        {
            Schema = "public", Tabela = "pedidos", Coluna = "numero",
            TipoDados = "character varying", ComprimentoMaximo = 200
        };

        var diferenca = Assert.Single(comparador.Comparar([producao], [homologacao]));

        Assert.Equal("ComprimentoMaximo", diferenca.TipoDiferenca);
        Assert.Equal("100", diferenca.ValorProducao);
        Assert.Equal("200", diferenca.ValorHomologacao);
    }

    [Fact]
    public void CompararChavesPrimarias_OrdemDiferente_EIdentificada()
    {
        var comparador = new ComparadorChavesPrimarias();
        var producao = new ChavePrimariaBanco
        {
            Schema = "public", Tabela = "pedidos", NomeConstraint = "pk_a",
            Colunas = ["cliente_id", "numero"]
        };
        var homologacao = new ChavePrimariaBanco
        {
            Schema = "public", Tabela = "pedidos", NomeConstraint = "pk_b",
            Colunas = ["numero", "cliente_id"]
        };

        var diferenca = Assert.Single(comparador.Comparar([producao], [homologacao]));

        Assert.Equal("ColunasChavePrimariaDiferentes", diferenca.TipoDiferenca);
    }

    [Fact]
    public void CompararIndices_NomesDiferentesComMesmaEstrutura_SaoEquivalentes()
    {
        var comparador = new ComparadorIndices();
        var producao = CriarIndice("ix_a");
        var homologacao = CriarIndice("idx_rebatizado");

        var diferencas = comparador.Comparar(
            [producao], [homologacao],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal));

        Assert.Empty(diferencas);
    }

    [Fact]
    public void CompararIndices_IndiceParcialDiferente_IdentificaPredicado()
    {
        var comparador = new ComparadorIndices();
        var producao = CriarIndice("ix_a");
        var homologacao = new IndiceBanco
        {
            Schema = "public", Tabela = "pedidos", Nome = "ix_b",
            Chaves = ["cliente_id"], MetodoAcesso = "btree",
            Predicado = "ativo = false", Valido = true, Pronto = true
        };

        var diferencas = comparador.Comparar(
            [producao], [homologacao],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal));

        Assert.Contains(diferencas, d => d.TipoDiferenca == "Predicado");
    }

    [Fact]
    public void CompararConstraints_ConstraintUnicaAusente_IdentificaCategoriaEValor()
    {
        var comparador = new ComparadorConstraints();
        var constraint = new ConstraintBanco
        {
            Schema = "public", Tabela = "pedidos", Nome = "uq_numero",
            Tipo = "UNIQUE", Colunas = ["numero"], Definicao = "UNIQUE (numero)"
        };

        var diferenca = Assert.Single(comparador.Comparar(
            [constraint], [],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal)));

        Assert.Equal("UNIQUE", diferenca.Categoria);
        Assert.Equal("AusenteNaHomologacao", diferenca.TipoDiferenca);
        Assert.Equal("UNIQUE (numero)", diferenca.ValorProducao);
    }

    [Fact]
    public void CompararConstraints_NomesDiferentesMasDefinicaoIgual_SaoEquivalentes()
    {
        var comparador = new ComparadorConstraints();
        var producao = new ConstraintBanco
        {
            Schema = "public", Tabela = "pedidos", Nome = "uq_numero",
            Tipo = "UNIQUE", Colunas = ["numero"], Definicao = "UNIQUE (numero)"
        };
        var homologacao = new ConstraintBanco
        {
            Schema = "public", Tabela = "pedidos", Nome = "unq_pedidos_num",
            Tipo = "UNIQUE", Colunas = ["numero"], Definicao = "UNIQUE (numero)"
        };

        Assert.Empty(comparador.Comparar(
            [producao], [homologacao],
            new HashSet<string>(["public.pedidos"], StringComparer.Ordinal)));
    }

    [Fact]
    public void CompararObjetosAdicionais_DefinicaoComEspacosDiferentes_SaoEquivalentes()
    {
        var comparador = new ComparadorObjetosAdicionais();
        var producao = new ObjetoBanco
        {
            Categoria = "View", Schema = "public", Nome = "vw_pedidos",
            Definicao = "SELECT id, numero FROM pedidos"
        };
        var homologacao = new ObjetoBanco
        {
            Categoria = "View", Schema = "public", Nome = "vw_pedidos",
            Definicao = "SELECT  id,\n numero FROM pedidos"
        };

        Assert.Empty(comparador.Comparar([producao], [homologacao]));
    }

    private static IndiceBanco CriarIndice(string nome) => new()
    {
        Schema = "public", Tabela = "pedidos", Nome = nome,
        Chaves = ["cliente_id"], Unico = false, MetodoAcesso = "btree",
        Predicado = "ativo = true", Valido = true, Pronto = true
    };
}
