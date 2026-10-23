using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorObjetosAdicionais
{
    List<DiferencaObjetoBanco> Comparar(
        IReadOnlyCollection<ObjetoBanco> objetosProducao,
        IReadOnlyCollection<ObjetoBanco> objetosHomologacao);
}
