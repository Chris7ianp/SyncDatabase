
using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorChavesPrimarias
{
    List<DiferencaChavePrimaria> Comparar(
        IReadOnlyCollection<ChavePrimariaBanco> chavesProducao,
        IReadOnlyCollection<ChavePrimariaBanco> chavesHomologacao);
}
