using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorChavesEstrangeiras
{
    List<DiferencaChaveEstrangeira> Comparar(
        IReadOnlyCollection<ChaveEstrangeiraBanco> chavesProducao,
        IReadOnlyCollection<ChaveEstrangeiraBanco> chavesHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas);
}
