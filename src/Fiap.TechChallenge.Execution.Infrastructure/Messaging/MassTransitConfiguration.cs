using Fiap.TechChallenge.Execution.Application.Consumers;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Infrastructure.Aws;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging;

public static class MassTransitConfiguration
{
    public const string DefaultDevRabbitMqConnectionString = "amqp://guest:guest@localhost:5672";

    public const string QueueOrdemServicoAberta = "execution-ordem-servico-aberta";
    public const string QueueDiagnosticoRejeitado = "execution-diagnostico-rejeitado";
    public const string QueueOrdemServicoLiberadaExecucao = "execution-os-liberada-execucao";
    public const string QueueOrdemServicoCancelada = "execution-ordem-servico-cancelada";

    public static IServiceCollection AddExecutionMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            // Registro dos 4 Consumers da Saga
            x.AddConsumer<OrdemServicoAbertaConsumer>();
            x.AddConsumer<DiagnosticoRejeitadoConsumer>();
            x.AddConsumer<OrdemServicoLiberadaParaExecucaoConsumer>();
            x.AddConsumer<OrdemServicoCanceladaConsumer>();

            // Configuração do Transactional Outbox / Inbox no PostgreSQL
            x.AddEntityFrameworkOutbox<AppDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            // Configuração do transporte (RabbitMQ ou InMemory para testes/local)
            var ssmResolver = new SsmSecretResolver(configuration);
            var rabbitConnectionString = ResolveRabbitMqConnectionString(configuration, ssmResolver);

            if (string.Equals(rabbitConnectionString, "in-memory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configuration["RabbitMq:Transport"], "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var runtimeResolver = context.GetRequiredService<SsmSecretResolver>();
                    var runtimeConfig = context.GetService<IConfiguration>() ?? configuration;
                    var connStr = ResolveRabbitMqConnectionString(runtimeConfig, runtimeResolver);

                    cfg.Host(new Uri(connStr));

                    // 1. Fila de Abertura de OS (Início do Diagnóstico)
                    cfg.ReceiveEndpoint(QueueOrdemServicoAberta, e =>
                    {
                        e.UseEntityFrameworkOutbox<AppDbContext>(context);
                        e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.ConfigureConsumer<OrdemServicoAbertaConsumer>(context);
                    });

                    // 2. Fila de Diagnóstico Rejeitado (Retorno para Análise Técnica)
                    cfg.ReceiveEndpoint(QueueDiagnosticoRejeitado, e =>
                    {
                        e.UseEntityFrameworkOutbox<AppDbContext>(context);
                        e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.ConfigureConsumer<DiagnosticoRejeitadoConsumer>(context);
                    });

                    // 3. Fila de OS Liberada para Execução (Entrada na Fila FIFO de Reparos)
                    cfg.ReceiveEndpoint(QueueOrdemServicoLiberadaExecucao, e =>
                    {
                        e.UseEntityFrameworkOutbox<AppDbContext>(context);
                        e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.ConfigureConsumer<OrdemServicoLiberadaParaExecucaoConsumer>(context);
                    });

                    // 4. Fila de OS Cancelada (Compensação A - Falha Externa)
                    cfg.ReceiveEndpoint(QueueOrdemServicoCancelada, e =>
                    {
                        e.UseEntityFrameworkOutbox<AppDbContext>(context);
                        e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.ConfigureConsumer<OrdemServicoCanceladaConsumer>(context);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        // Registro do publicador concreto que utiliza o Outbox
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return services;
    }

    private static string ResolveRabbitMqConnectionString(
        IConfiguration configuration,
        SsmSecretResolver ssmResolver)
    {
        var configured = configuration.GetConnectionString("RabbitMq")
            ?? configuration["RABBITMQ_CONNECTION_STRING"]
            ?? configuration["Ssm:RabbitMqConnectionStringParam"]
            ?? configuration["SSM_RABBITMQ_CONNECTION_STRING_PARAM"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ssmResolver.ResolveRabbitMqConnectionStringAsync().GetAwaiter().GetResult();
        }

        if (bool.TryParse(configuration["Ssm:Enabled"] ?? configuration["AWS_SSM_ENABLED"], out var ssmEnabled) && ssmEnabled)
        {
            return ssmResolver.ResolveRabbitMqConnectionStringAsync().GetAwaiter().GetResult();
        }

        return DefaultDevRabbitMqConnectionString;
    }
}
