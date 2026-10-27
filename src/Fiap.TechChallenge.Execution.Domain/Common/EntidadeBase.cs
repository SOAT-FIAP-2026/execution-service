namespace Fiap.TechChallenge.Execution.Domain.Common;

/// <summary>
/// Classe base para entidades do domínio com identificador único UUID e timestamps de auditoria.
/// </summary>
public abstract class EntidadeBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CriadoEm { get; protected set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; protected set; } = DateTime.UtcNow;

    protected void AtualizarTimestamp()
    {
        AtualizadoEm = DateTime.UtcNow;
    }
}
