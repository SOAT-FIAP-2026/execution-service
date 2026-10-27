namespace Fiap.TechChallenge.Execution.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de regras de negócio do domínio de Execução e Produção.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Lançada quando uma transição de estado da ordem de produção ou tarefa é inválida.
/// </summary>
public sealed class TransicaoEstadoInvalidaException : DomainException
{
    public TransicaoEstadoInvalidaException(string mensagem) : base(mensagem)
    {
    }
}

/// <summary>
/// Lançada quando uma tarefa de produção não é encontrada no agregado.
/// </summary>
public sealed class TarefaProducaoNaoEncontradaException : DomainException
{
    public TarefaProducaoNaoEncontradaException(Guid tarefaId)
        : base($"Tarefa de produção com ID '{tarefaId}' não encontrada na ordem.")
    {
    }
}

/// <summary>
/// Lançada quando um mecânico tenta alterar ou finalizar uma tarefa/diagnóstico atribuído a outro profissional.
/// </summary>
public sealed class MecanicoNaoAutorizadoException : DomainException
{
    public MecanicoNaoAutorizadoException(string mensagem) : base(mensagem)
    {
    }
}
