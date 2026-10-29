using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Infrastructure.Aws;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using Fiap.TechChallenge.Execution.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.Execution.Infrastructure;

/// <summary>
/// Registro de dependências da camada de Infraestrutura (PostgreSQL 16 + EF Core + AWS SSM).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddExecutionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var ssmResolver = new SsmSecretResolver(configuration);
        services.AddSingleton(ssmResolver);

        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = ResolveDbConnectionStringOrFallback(configuration, ssmResolver);
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IOrdemProducaoRepository, OrdemProducaoRepository>();

        return services;
    }

    private static string ResolveDbConnectionStringOrFallback(
        IConfiguration configuration,
        SsmSecretResolver ssmResolver)
    {
        var configured = configuration.GetConnectionString("ExecutionDb")
            ?? configuration["DB_CONNECTION_STRING"]
            ?? configuration["Ssm:DbConnectionStringParam"]
            ?? configuration["SSM_DB_CONNECTION_STRING_PARAM"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ssmResolver.ResolveDbConnectionStringAsync().GetAwaiter().GetResult();
        }

        if (bool.TryParse(configuration["Ssm:Enabled"] ?? configuration["AWS_SSM_ENABLED"], out var ssmEnabled) && ssmEnabled)
        {
            return ssmResolver.ResolveDbConnectionStringAsync().GetAwaiter().GetResult();
        }

        // Fallback seguro para inicialização de host/design-time quando a connection string não foi injetada
        return "Host=localhost;Port=5433;Database=techchallenge_execution_db;Username=execution;Password=execution_local_pwd";
    }
}

