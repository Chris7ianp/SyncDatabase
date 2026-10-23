
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorColunas
{
    List<DiferencaColuna> Comparar(
        IReadOnlyCollection<ColunaBanco> colunasProducao,
        IReadOnlyCollection<ColunaBanco> colunasHomologacao);
}
