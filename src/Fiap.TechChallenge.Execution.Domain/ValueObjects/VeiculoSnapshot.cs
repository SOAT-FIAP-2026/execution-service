using Fiap.TechChallenge.Execution.Domain.Exceptions;

namespace Fiap.TechChallenge.Execution.Domain.ValueObjects;

/// <summary>
/// Snapshot imutável dos dados de identificação do veículo para a oficina.
/// </summary>
public sealed record VeiculoSnapshot
{
    public string Placa { get; private init; } = string.Empty;
    public string Modelo { get; private init; } = string.Empty;
    public int Ano { get; private init; }

    private VeiculoSnapshot() { }

    public VeiculoSnapshot(string placa, string modelo, int ano)
    {
        if (string.IsNullOrWhiteSpace(placa))
            throw new DomainException("A placa do veículo é obrigatória no snapshot.");

        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("O modelo do veículo é obrigatório no snapshot.");

        if (ano < 1900 || ano > DateTime.UtcNow.Year + 2)
            throw new DomainException($"O ano do veículo '{ano}' é inválido.");

        Placa = placa.Trim().ToUpperInvariant();
        Modelo = modelo.Trim();
        Ano = ano;
    }
}
