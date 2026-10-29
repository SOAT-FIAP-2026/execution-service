using Fiap.TechChallenge.Execution.Application.Queries;
using Fiap.TechChallenge.Execution.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.Execution.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddExecutionApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use Cases (Commands)
        services.AddScoped<IniciarDiagnosticoUseCase>();
        services.AddScoped<FinalizarDiagnosticoUseCase>();
        services.AddScoped<IniciarTarefaUseCase>();
        services.AddScoped<FinalizarTarefaUseCase>();
        services.AddScoped<CancelarProducaoUseCase>();

        // Queries
        services.AddScoped<ObterFilaProducaoQuery>();
        services.AddScoped<ObterDiagnosticosPendentesQuery>();
        services.AddScoped<ObterDetalhesOrdemProducaoQuery>();
        services.AddScoped<ObterMetricasProdutividadeQuery>();

        return services;
    }
}

