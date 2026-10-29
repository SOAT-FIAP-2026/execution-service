using Fiap.TechChallenge.Execution.Domain.Common;
using Fiap.TechChallenge.Execution.Domain.Exceptions;

namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Representa o processo de inspeção técnica diagnóstica realizada pelo mecânico.
/// </summary>
public class Diagnostico : EntidadeBase
{
    public Guid OrdemProducaoId { get; private set; }
    public Guid IdMecanico { get; private set; }
    public string NomeMecanico { get; private set; } = string.Empty;
    public DateTime DataHoraInicio { get; private set; }
    public DateTime? DataHoraFim { get; private set; }
    public string? LaudoTecnico { get; private set; }

    private readonly List<ServicoSugerido> _servicosSugeridos = [];
    public IReadOnlyList<ServicoSugerido> ServicosSugeridos => _servicosSugeridos.AsReadOnly();

    private readonly List<PecaSugerida> _pecasSugeridas = [];
    public IReadOnlyList<PecaSugerida> PecasSugeridas => _pecasSugeridas.AsReadOnly();

    protected Diagnostico() { }

    public Diagnostico(Guid ordemProducaoId, Guid idMecanico, string nomeMecanico, DateTime? dataHoraInicio = null)
    {
        if (ordemProducaoId == Guid.Empty)
            throw new DomainException("Ordem de produção inválida para o diagnóstico.");

        if (idMecanico == Guid.Empty)
            throw new DomainException("O identificador do mecânico é obrigatório.");

        if (string.IsNullOrWhiteSpace(nomeMecanico))
            throw new DomainException("O nome do mecânico é obrigatório.");

        OrdemProducaoId = ordemProducaoId;
        IdMecanico = idMecanico;
        NomeMecanico = nomeMecanico.Trim();
        DataHoraInicio = dataHoraInicio ?? DateTime.UtcNow;
    }

    public void Reiniciar(Guid idMecanico, string nomeMecanico, DateTime? dataHoraInicio = null)
    {
        if (idMecanico == Guid.Empty)
            throw new DomainException("O identificador do mecânico é obrigatório.");

        if (string.IsNullOrWhiteSpace(nomeMecanico))
            throw new DomainException("O nome do mecânico é obrigatório.");

        IdMecanico = idMecanico;
        NomeMecanico = nomeMecanico.Trim();
        DataHoraInicio = dataHoraInicio ?? DateTime.UtcNow;
        DataHoraFim = null;
        LaudoTecnico = null;
        _servicosSugeridos.Clear();
        _pecasSugeridas.Clear();
        AtualizarTimestamp();
    }

    public void Finalizar(
        string laudoTecnico,
        IEnumerable<ServicoSugerido>? servicos = null,
        IEnumerable<PecaSugerida>? pecas = null,
        DateTime? dataHoraFim = null)
    {
        if (string.IsNullOrWhiteSpace(laudoTecnico))
            throw new DomainException("O laudo técnico é obrigatório para concluir o diagnóstico.");

        DataHoraFim = dataHoraFim ?? DateTime.UtcNow;

        if (DataHoraFim < DataHoraInicio)
            throw new DomainException("A data/hora de término do diagnóstico não pode ser anterior ao início.");

        LaudoTecnico = laudoTecnico.Trim();

        _servicosSugeridos.Clear();
        if (servicos != null)
        {
            foreach (var servico in servicos)
            {
                servico.AssociarDiagnostico(Id);
                _servicosSugeridos.Add(servico);
            }
        }

        _pecasSugeridas.Clear();
        if (pecas != null)
        {
            foreach (var peca in pecas)
            {
                peca.AssociarDiagnostico(Id);
                _pecasSugeridas.Add(peca);
            }
        }

        AtualizarTimestamp();
    }

    public int? ObterDuracaoMinutos()
    {
        if (!DataHoraFim.HasValue)
            return null;

        var totalMinutos = (DataHoraFim.Value - DataHoraInicio).TotalMinutes;
        return (int)Math.Max(1, Math.Round(totalMinutos));
    }
}

/// <summary>
/// Sugestão de serviço a ser executado, identificado no catálogo comercial do OS Service.
/// </summary>
public class ServicoSugerido : EntidadeBase
{
    public Guid DiagnosticoId { get; private set; }
    public Guid IdServicoCatalogo { get; private set; }
    public string? Descricao { get; private set; }

    protected ServicoSugerido() { }

    public ServicoSugerido(Guid idServicoCatalogo, string? descricao = null)
    {
        if (idServicoCatalogo == Guid.Empty)
            throw new DomainException("O identificador do serviço de catálogo é obrigatório.");

        IdServicoCatalogo = idServicoCatalogo;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
    }

    public void AssociarDiagnostico(Guid diagnosticoId)
    {
        if (diagnosticoId == Guid.Empty)
            throw new DomainException("Diagnóstico inválido.");

        DiagnosticoId = diagnosticoId;
        AtualizarTimestamp();
    }
}

/// <summary>
/// Sugestão de peça/insumo necessário, identificada no catálogo do OS Service.
/// </summary>
public class PecaSugerida : EntidadeBase
{
    public Guid DiagnosticoId { get; private set; }
    public Guid IdPecaCatalogo { get; private set; }
    public int Quantidade { get; private set; }
    public string? Descricao { get; private set; }

    protected PecaSugerida() { }

    public PecaSugerida(Guid idPecaCatalogo, int quantidade, string? descricao = null)
    {
        if (idPecaCatalogo == Guid.Empty)
            throw new DomainException("O identificador da peça de catálogo é obrigatório.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade sugerida de peças deve ser maior que zero.");

        IdPecaCatalogo = idPecaCatalogo;
        Quantidade = quantidade;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
    }

    public void AssociarDiagnostico(Guid diagnosticoId)
    {
        if (diagnosticoId == Guid.Empty)
            throw new DomainException("Diagnóstico inválido.");

        DiagnosticoId = diagnosticoId;
        AtualizarTimestamp();
    }
}
