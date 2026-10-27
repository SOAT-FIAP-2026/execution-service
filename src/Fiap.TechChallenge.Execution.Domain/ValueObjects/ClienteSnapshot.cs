using Fiap.TechChallenge.Execution.Domain.Exceptions;

namespace Fiap.TechChallenge.Execution.Domain.ValueObjects;

/// <summary>
/// Snapshot imutável dos dados mínimos do cliente para contato/identificação no chão de oficina.
/// Conforme diretrizes da LGPD, CPF/CNPJ e e-mail não são trafegados nem armazenados.
/// </summary>
public sealed record ClienteSnapshot
{
    public Guid IdCliente { get; }
    public string Nome { get; }
    public string? Telefone { get; }

    public ClienteSnapshot(Guid idCliente, string nome, string? telefone = null)
    {
        if (idCliente == Guid.Empty)
            throw new DomainException("O identificador do cliente não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do cliente é obrigatório no snapshot.");

        IdCliente = idCliente;
        Nome = nome.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
    }
}
