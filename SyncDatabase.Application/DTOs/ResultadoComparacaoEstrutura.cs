using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.DTOs.Comparacao;

public class ResultadoComparacaoEstrutura
{
    public bool ComparacaoEstruturalCompleta { get; init; } = false;

    public List<string> CategoriasNaoComparadas { get; init; } =
    [
        "DadosDasTabelas"
    ];

    public DateTime DataComparacao { get; set; } = DateTime.UtcNow;

    public List<string> TabelasApenasNaProducao { get; set; } = [];

    public List<string> TabelasApenasNaHomologacao { get; set; } = [];

    public List<string> TabelasEmAmbosAmbientes { get; set; } = [];
    public List<DiferencaColuna> DiferencasColunas { get; set; } = [];
    public List<DiferencaChavePrimaria> DiferencasChavesPrimarias
    { get; init; } = [];

    public List<DiferencaChaveEstrangeira> DiferencasChavesEstrangeiras
    { get; init; } = [];

    public List<DiferencaIndice> DiferencasIndices { get; init; } = [];

    public List<DiferencaConstraint> DiferencasConstraints { get; init; } = [];

    public List<DiferencaObjetoBanco> DiferencasOutrosObjetos { get; init; } = [];
}
