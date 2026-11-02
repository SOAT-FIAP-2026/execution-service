using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Fiap.TechChallenge.Execution.Application.Consumers;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging
{
    public static class MassTransitConfiguration
    {
        public static IServiceCollection AddExecutionMessaging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // 1. Clientes AWS (SQS e SNS) direcionados ao Floci/LocalStack ou AWS nativa
            var awsServiceUrl = configuration["AWS_SERVICE_URL"]
                ?? configuration["AWS__ServiceURL"]
                ?? configuration["AWS_ENDPOINT_URL"];
            var awsRegion = configuration["AWS_REGION"] ?? configuration["AWS__Region"] ?? "sa-east-1";
            var credentials = new BasicAWSCredentials(
                configuration["AWS_ACCESS_KEY_ID"] ?? "test",
                configuration["AWS_SECRET_ACCESS_KEY"] ?? "test");

            if (!string.IsNullOrWhiteSpace(awsServiceUrl))
            {
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(credentials, new AmazonSQSConfig
                {
                    ServiceURL = awsServiceUrl,
                    AuthenticationRegion = awsRegion
                }));

                services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(credentials, new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = awsServiceUrl,
                    AuthenticationRegion = awsRegion
                }));
            }
            else
            {
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
                services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient());
            }

            // 2. MassTransit com Transactional Outbox do EF Core (transporte InMemory desacoplado do RabbitMQ)
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                x.AddConsumer<OrdemServicoAbertaConsumer>();
                x.AddConsumer<DiagnosticoRejeitadoConsumer>();
                x.AddConsumer<OrdemServicoLiberadaParaExecucaoConsumer>();
                x.AddConsumer<OrdemServicoCanceladaConsumer>();

                x.AddEntityFrameworkOutbox<AppDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                    o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
                });

                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            });

            // 3. Publicador de eventos de domínio no tópico AWS SNS
            services.AddScoped<IEventPublisher, AwsSnsEventPublisher>();

            // 4. Worker em background para consumo contínuo da fila AWS SQS
            services.AddHostedService<ExecutionSqsBackgroundWorker>();

            return services;
        }
    }
}
