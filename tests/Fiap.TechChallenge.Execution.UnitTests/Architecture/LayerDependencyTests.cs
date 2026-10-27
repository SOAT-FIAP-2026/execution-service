using FluentAssertions;
using NetArchTest.Rules;

namespace Fiap.TechChallenge.Execution.UnitTests.Architecture;

/// <summary>
/// Garante as regras de dependência da Clean Architecture (seção 7 do plano):
/// Domain não depende de nada; Application depende só de Domain (e Contracts);
/// Infrastructure implementa as interfaces; Api faz a composição.
/// </summary>
public class LayerDependencyTests
{
    private const string Domain = "Fiap.TechChallenge.Execution.Domain";
    private const string Application = "Fiap.TechChallenge.Execution.Application";
    private const string Infrastructure = "Fiap.TechChallenge.Execution.Infrastructure";
    private const string Api = "Fiap.TechChallenge.Execution.Api";

    [Fact]
    public void Domain_nao_deve_depender_de_outras_camadas_nem_de_frameworks()
    {
        var result = Types.InAssembly(typeof(global::Fiap.TechChallenge.Execution.Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api,
                "Microsoft.EntityFrameworkCore", "MassTransit", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_nao_deve_depender_de_Infrastructure_nem_de_Api()
    {
        var result = Types.InAssembly(typeof(global::Fiap.TechChallenge.Execution.Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_nao_deve_depender_de_Api()
    {
        var result = Types.InAssembly(typeof(global::Fiap.TechChallenge.Execution.Infrastructure.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        "tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []);
}
