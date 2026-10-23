
using Microsoft.Extensions.Configuration;
using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.Interfaces.Comparacao;
using SyncDatabase.Application.Interfaces.Estrutura;

namespace SyncDatabase.Infrastructure.PostgreSql.Comparacao;

public sealed class ComparadorEstruturaPostgreSql : IComparadorEstrutura
{
    private readonly ILeitorEstruturaBanco _leitorEstrutura;
    private readonly ILeitorColunasBanco _leitorColunas;
    private readonly IComparadorColunas _comparadorColunas;
    private readonly IConfiguration _configuration;
    private readonly ILeitorChavesPrimariasBanco _leitorChaves;
    private readonly IComparadorChavesPrimarias _comparadorChaves;
    private readonly ILeitorChavesEstrangeirasBanco _leitorChavesEstrangeiras;
    private readonly IComparadorChavesEstrangeiras _comparadorChavesEstrangeiras;
    private readonly ILeitorIndicesBanco _leitorIndices;
    private readonly IComparadorIndices _comparadorIndices;
    private readonly ILeitorConstraintsBanco _leitorConstraints;
    private readonly IComparadorConstraints _comparadorConstraints;
    private readonly ILeitorObjetosAdicionaisBanco _leitorObjetosAdicionais;
    private readonly IComparadorObjetosAdicionais _comparadorObjetosAdicionais;

    public ComparadorEstruturaPostgreSql(
        ILeitorEstruturaBanco leitorEstrutura,
        ILeitorColunasBanco leitorColunas,
        IComparadorColunas comparadorColunas,
        IConfiguration configuration,
        ILeitorChavesPrimariasBanco leitorChaves,
        IComparadorChavesPrimarias comparadorChaves,
        ILeitorChavesEstrangeirasBanco leitorChavesEstrangeiras,
        IComparadorChavesEstrangeiras comparadorChavesEstrangeiras,
        ILeitorIndicesBanco leitorIndices,
        IComparadorIndices comparadorIndices,
        ILeitorConstraintsBanco leitorConstraints,
        IComparadorConstraints comparadorConstraints,
        ILeitorObjetosAdicionaisBanco leitorObjetosAdicionais,
        IComparadorObjetosAdicionais comparadorObjetosAdicionais)
    {
        _leitorEstrutura = leitorEstrutura;
        _leitorColunas = leitorColunas;
        _comparadorColunas = comparadorColunas;
        _configuration = configuration;
        _leitorChaves = leitorChaves;
        _comparadorChaves = comparadorChaves;
        _leitorChavesEstrangeiras = leitorChavesEstrangeiras;
        _comparadorChavesEstrangeiras = comparadorChavesEstrangeiras;
        _leitorIndices = leitorIndices;
        _comparadorIndices = comparadorIndices;
        _leitorConstraints = leitorConstraints;
        _comparadorConstraints = comparadorConstraints;
        _leitorObjetosAdicionais = leitorObjetosAdicionais;
        _comparadorObjetosAdicionais = comparadorObjetosAdicionais;
    }





    public async Task<ResultadoComparacaoEstrutura> CompararAsync(
        CancellationToken cancellationToken = default)
    {
        var producao = _configuration.GetConnectionString("Producao");
        var homologacao = _configuration.GetConnectionString("Homologacao");

        if (string.IsNullOrWhiteSpace(producao) ||
            string.IsNullOrWhiteSpace(homologacao))
        {
            throw new InvalidOperationException(
                "As conexões de produção e homologação devem estar configuradas.");
        }

        // Metadados são consultados somente para leitura em ambos os ambientes.
        var tabelasProducao = await _leitorEstrutura.ListarTabelasAsync(
            producao, cancellationToken);

        var tabelasHomologacao = await _leitorEstrutura.ListarTabelasAsync(
            homologacao, cancellationToken);

        var conjuntoProducao = tabelasProducao.ToHashSet(
            StringComparer.Ordinal);

        var conjuntoHomologacao = tabelasHomologacao.ToHashSet(
            StringComparer.Ordinal);

        // 3. Identificar tabelas compartilhadas
        var tabelasCompartilhadas = conjuntoProducao
            .Intersect(conjuntoHomologacao)
            .ToHashSet(StringComparer.Ordinal);

        // 4. Listar colunas dos dois ambientes
        var colunasProducao = await _leitorColunas.ListarColunasAsync(
            producao, cancellationToken);

        var colunasHomologacao = await _leitorColunas.ListarColunasAsync(
            homologacao, cancellationToken);

        // 5. Filtrar colunas das tabelas compartilhadas
        var colunasProducaoCompartilhadas = colunasProducao
            .Where(c => tabelasCompartilhadas.Contains(
                $"{c.Schema}.{c.Tabela}"))
            .ToList();

        var colunasHomologacaoCompartilhadas = colunasHomologacao
            .Where(c => tabelasCompartilhadas.Contains(
                $"{c.Schema}.{c.Tabela}"))
            .ToList();

        // 6. Comparar colunas
        var diferencasColunas = _comparadorColunas.Comparar(
            colunasProducaoCompartilhadas,
            colunasHomologacaoCompartilhadas);

        // 7. Listar chaves primárias
        var chavesProducao =
            await _leitorChaves.ListarChavesPrimariasAsync(
                producao, cancellationToken);

        var chavesHomologacao =
            await _leitorChaves.ListarChavesPrimariasAsync(
                homologacao, cancellationToken);

        Console.WriteLine(
            $"[SyncDatabase] Chaves encontradas em Produção: " +
            $"{chavesProducao.Count}");

        Console.WriteLine(
            $"[SyncDatabase] Chaves encontradas em Homologação: " +
            $"{chavesHomologacao.Count}");

        // 8. Filtrar chaves primárias das tabelas compartilhadas
        var chavesProducaoCompartilhadas = chavesProducao
            .Where(c => tabelasCompartilhadas.Contains(
                $"{c.Schema}.{c.Tabela}"))
            .ToList();

        var chavesHomologacaoCompartilhadas = chavesHomologacao
            .Where(c => tabelasCompartilhadas.Contains(
                $"{c.Schema}.{c.Tabela}"))
            .ToList();

        // 9. Comparar chaves primárias
        var diferencasChaves = _comparadorChaves.Comparar(
            chavesProducaoCompartilhadas,
            chavesHomologacaoCompartilhadas);

        var chavesEstrangeirasProducao = await _leitorChavesEstrangeiras
            .ListarChavesEstrangeirasAsync(producao, cancellationToken);
        var chavesEstrangeirasHomologacao = await _leitorChavesEstrangeiras
            .ListarChavesEstrangeirasAsync(homologacao, cancellationToken);
        var diferencasChavesEstrangeiras = _comparadorChavesEstrangeiras.Comparar(
            chavesEstrangeirasProducao,
            chavesEstrangeirasHomologacao,
            tabelasCompartilhadas);

        var indicesProducao = await _leitorIndices.ListarIndicesAsync(producao, cancellationToken);
        var indicesHomologacao = await _leitorIndices.ListarIndicesAsync(homologacao, cancellationToken);
        var diferencasIndices = _comparadorIndices.Comparar(
            indicesProducao,
            indicesHomologacao,
            tabelasCompartilhadas);

        var constraintsProducao = await _leitorConstraints.ListarConstraintsAsync(producao, cancellationToken);
        var constraintsHomologacao = await _leitorConstraints.ListarConstraintsAsync(homologacao, cancellationToken);
        var diferencasConstraints = _comparadorConstraints.Comparar(
            constraintsProducao,
            constraintsHomologacao,
            tabelasCompartilhadas);

        var objetosProducao = await _leitorObjetosAdicionais.ListarObjetosAsync(producao, cancellationToken);
        var objetosHomologacao = await _leitorObjetosAdicionais.ListarObjetosAsync(homologacao, cancellationToken);
        var diferencasOutrosObjetos = _comparadorObjetosAdicionais.Comparar(
            objetosProducao,
            objetosHomologacao);

        Console.WriteLine(
            $"[SyncDatabase] Diferenças de chaves primárias: " +
            $"{diferencasChaves.Count}");

        // 10. Montar resultado
        return new ResultadoComparacaoEstrutura
        {
            DataComparacao = DateTime.UtcNow,

            TabelasApenasNaProducao = conjuntoProducao
                .Except(conjuntoHomologacao)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList(),

            TabelasApenasNaHomologacao = conjuntoHomologacao
                .Except(conjuntoProducao)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList(),

            TabelasEmAmbosAmbientes = tabelasCompartilhadas
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList(),

            DiferencasColunas = diferencasColunas,

            DiferencasChavesPrimarias = diferencasChaves,

            DiferencasChavesEstrangeiras = diferencasChavesEstrangeiras,

            DiferencasIndices = diferencasIndices,

            DiferencasConstraints = diferencasConstraints,

            DiferencasOutrosObjetos = diferencasOutrosObjetos
        };
    }


}
