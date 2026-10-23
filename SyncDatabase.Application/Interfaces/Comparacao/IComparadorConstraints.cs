using SyncDatabase.Application.DTOs.Comparacao;
using SyncDatabase.Application.DTOs.Estrutura;

namespace SyncDatabase.Application.Interfaces.Comparacao;

public interface IComparadorConstraints
{
    List<DiferencaConstraint> Comparar(
        IReadOnlyCollection<ConstraintBanco> constraintsProducao,
        IReadOnlyCollection<ConstraintBanco> constraintsHomologacao,
        IReadOnlySet<string> tabelasCompartilhadas);
}
