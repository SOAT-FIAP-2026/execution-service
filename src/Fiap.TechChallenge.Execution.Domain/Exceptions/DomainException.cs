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
/// Lançada quando uma ordem de produção não é encontrada pelo seu ID ou pelo ID da Ordem de Serviço de origem.
/// </summary>
public sealed class OrdemProducaoNaoEncontradaException : DomainException
{
    public OrdemProducaoNaoEncontradaException(Guid id, bool buscaPorOrdemServico = false)
        : base(buscaPorOrdemServico
            ? $"Ordem de produção para a Ordem de Serviço '{id}' não encontrada."
            : $"Ordem de produção com ID '{id}' não encontrada.")
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

/// <summary>
/// Lançada quando ocorre conflito de concorrência otimista (versão divergente) ao persistir alterações no agregado.
/// </summary>
public sealed class ConflitoConcorrenciaException : DomainException
{
    public ConflitoConcorrenciaException(Guid ordemProducaoId, Exception? innerException = null)
        : base($"Conflito de concorrência detectado ao atualizar a ordem de produção '{ordemProducaoId}'. O registro foi modificado por outra operação concorrente.", innerException!)
    {
    }
}
