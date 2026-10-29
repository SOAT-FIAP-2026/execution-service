using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.Queries;

public class ObterMetricasProdutividadeQuery
{
    private readonly IOrdemProducaoRepository _repository;

    public ObterMetricasProdutividadeQuery(IOrdemProducaoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<RelatorioProdutividadeResponse> ExecuteAsync(
        DateTime? de = null,
        DateTime? ate = null,
        Guid? idMecanico = null,
        CancellationToken cancellationToken = default)
    {
        if (de.HasValue && ate.HasValue && de.Value > ate.Value)
        {
            throw new DomainException("A data inicial ('de') não pode ser posterior à data final ('ate').");
        }

        var tarefas = await _repository.ObterTarefasConcluidasPorPeriodoAsync(de, ate, idMecanico, cancellationToken);

        var porMecanico = tarefas
            .Where(t => t.IdMecanicoResponsavel.HasValue)
            .GroupBy(t => t.IdMecanicoResponsavel!.Value)
            .Select(grupo =>
            {
                var totalTarefas = grupo.Count();
                var totalEstimado = grupo.Sum(t => t.TempoEstimadoMinutos);
                var totalRealizado = grupo.Sum(t => t.ObterDuracaoRealMinutos() ?? t.TempoEstimadoMinutos);

                var mediaEstimada = Math.Round((double)totalEstimado / totalTarefas, 2);
                var mediaRealizada = Math.Round((double)totalRealizado / totalTarefas, 2);
                var eficiencia = totalRealizado > 0
                    ? Math.Round((double)totalEstimado / totalRealizado * 100.0, 2)
                    : 100.0;

                var nomeMecanico = grupo
                    .Select(t => t.NomeMecanicoResponsavel)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                    ?? "Mecânico";

                return new MetricaProdutividadeMecanicoResponse(
                    IdMecanico: grupo.Key,
                    NomeMecanico: nomeMecanico,
                    TarefasConcluidas: totalTarefas,
                    TempoTotalEstimadoMinutos: totalEstimado,
                    TempoTotalRealizadoMinutos: totalRealizado,
                    MediaEstimadaMinutos: mediaEstimada,
                    MediaRealizadaMinutos: mediaRealizada,
                    IndiceEficienciaPercentual: eficiencia);
            })
            .OrderByDescending(m => m.TarefasConcluidas)
            .ThenBy(m => m.NomeMecanico)
            .ToList();

        return new RelatorioProdutividadeResponse(
            PeriodoInicio: de,
            PeriodoFim: ate,
            TotalTarefasConcluidas: tarefas.Count,
            Mecanicos: porMecanico);
    }
}

