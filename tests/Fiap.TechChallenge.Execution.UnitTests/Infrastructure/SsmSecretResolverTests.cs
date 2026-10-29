using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Infrastructure;
using Fiap.TechChallenge.Execution.Infrastructure.Aws;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Fiap.TechChallenge.Execution.UnitTests.Infrastructure;

public class SsmSecretResolverTests
{
    [Fact]
    public async Task ResolveDbConnectionStringAsync_deve_retornar_valor_local_sem_chamar_ssm()
    {
        var ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ExecutionDb"] = "Host=localhost;Database=exec_db;Username=postgres;Password=secret"
            })
            .Build();

        var resolver = new SsmSecretResolver(config, ssmClient);

        var conn = await resolver.ResolveDbConnectionStringAsync();

        conn.Should().Be("Host=localhost;Database=exec_db;Username=postgres;Password=secret");
        await ssmClient.DidNotReceiveWithAnyArgs().GetParameterAsync(default!, default);
    }

    [Fact]
    public async Task ResolveSecretAsync_com_prefixo_ssm_deve_buscar_no_aws_ssm_e_armazenar_em_cache()
    {
        var ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();
        ssmClient.GetParameterAsync(
                Arg.Is<GetParameterRequest>(r =>
                    r.Name == SsmSecretResolver.DefaultDbConnectionStringParam && r.WithDecryption == true),
                Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter
                {
                    Name = SsmSecretResolver.DefaultDbConnectionStringParam,
                    Value = "Host=rds.aws;Database=techchallenge_execution_db"
                }
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ExecutionDb"] = $"ssm:{SsmSecretResolver.DefaultDbConnectionStringParam}"
            })
            .Build();

        var resolver = new SsmSecretResolver(config, ssmClient);

        var primeiraLeitura = await resolver.ResolveDbConnectionStringAsync();
        var segundaLeitura = await resolver.ResolveDbConnectionStringAsync();

        primeiraLeitura.Should().Be("Host=rds.aws;Database=techchallenge_execution_db");
        segundaLeitura.Should().Be(primeiraLeitura);

        await ssmClient.Received(1).GetParameterAsync(
            Arg.Any<GetParameterRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveRabbitMqConnectionStringAsync_com_caminho_ssm_direto_deve_buscar_no_ssm()
    {
        var ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();
        ssmClient.GetParameterAsync(
                Arg.Is<GetParameterRequest>(r => r.Name == SsmSecretResolver.DefaultRabbitMqConnectionStringParam),
                Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Value = "amqp://user:pass@rabbitmq.messaging.svc:5672/" }
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RabbitMq"] = SsmSecretResolver.DefaultRabbitMqConnectionStringParam
            })
            .Build();

        var resolver = new SsmSecretResolver(config, ssmClient);

        var resultado = await resolver.ResolveRabbitMqConnectionStringAsync();

        resultado.Should().Be("amqp://user:pass@rabbitmq.messaging.svc:5672/");
    }

    [Fact]
    public async Task ResolveJwtSecretAsync_com_ssm_enabled_deve_usar_parametro_padrao()
    {
        var ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();
        ssmClient.GetParameterAsync(
                Arg.Is<GetParameterRequest>(r => r.Name == SsmSecretResolver.DefaultJwtSecretParam),
                Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Value = "jwt-super-secret-key-256-bits-prod" }
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ssm:Enabled"] = "true"
            })
            .Build();

        var resolver = new SsmSecretResolver(config, ssmClient);

        var secret = await resolver.ResolveJwtSecretAsync();

        secret.Should().Be("jwt-super-secret-key-256-bits-prod");
    }

    [Fact]
    public async Task ResolveSecretAsync_sem_configuracao_deve_lancar_InvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new SsmSecretResolver(config, Substitute.For<IAmazonSimpleSystemsManagement>());

        var act = async () => await resolver.ResolveDbConnectionStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Segredo ou string de conexão não configurada*");
    }

    [Fact]
    public void AddExecutionInfrastructure_deve_registrar_AppDbContext_e_OrdemProducaoRepository()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ExecutionDb"] = "Host=localhost;Port=5433;Database=techchallenge_execution_db;Username=execution;Password=pwd"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddExecutionInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<SsmSecretResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetService<AppDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IOrdemProducaoRepository>().Should().NotBeNull();
    }
}

