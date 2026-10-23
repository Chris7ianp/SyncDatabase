using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorIndices
{
    List<DiferencaIndice> Comparar(
        IReadOnlyCollection<IndiceBanco> indicesProducao,
        IReadOnlyCollection<IndiceBanco> indicesHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas);
}
