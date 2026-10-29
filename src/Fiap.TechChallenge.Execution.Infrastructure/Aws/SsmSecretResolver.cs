using System.Collections.Concurrent;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fiap.TechChallenge.Execution.Infrastructure.Aws;

/// <summary>
/// Resolve segredos e strings de conexão priorizando configuração local/variáveis de ambiente
/// e recorrendo ao AWS Systems Manager (SSM) Parameter Store em ambiente produtivo (EKS).
/// </summary>
public class SsmSecretResolver
{
    public const string DefaultDbConnectionStringParam = "/techchallenge/prod/execution/db_connection_string";
    public const string DefaultRabbitMqConnectionStringParam = "/techchallenge/prod/rabbitmq_connection_string";
    public const string DefaultJwtSecretParam = "/techchallenge/prod/jwt_secret";

    private const string SsmPrefix = "ssm:";

    private readonly IConfiguration _configuration;
    private readonly ILogger<SsmSecretResolver> _logger;
    private readonly Func<IAmazonSimpleSystemsManagement> _ssmClientFactory;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public SsmSecretResolver(
        IConfiguration configuration,
        IAmazonSimpleSystemsManagement? ssmClient = null,
        ILogger<SsmSecretResolver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _logger = logger ?? NullLogger<SsmSecretResolver>.Instance;
        _ssmClientFactory = ssmClient is not null
            ? () => ssmClient
            : static () => new AmazonSimpleSystemsManagementClient();
    }

    /// <summary>
    /// Resolve a string de conexão do PostgreSQL exclusivo do Execution Service.
    /// </summary>
    public Task<string> ResolveDbConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ResolveSecretAsync(
            configKeys: ["ConnectionStrings:ExecutionDb", "DB_CONNECTION_STRING"],
            ssmParamConfigKeys: ["Ssm:DbConnectionStringParam", "SSM_DB_CONNECTION_STRING_PARAM"],
            defaultSsmParameterName: DefaultDbConnectionStringParam,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Resolve a string de conexão do RabbitMQ.
    /// </summary>
    public Task<string> ResolveRabbitMqConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ResolveSecretAsync(
            configKeys: ["ConnectionStrings:RabbitMq", "RABBITMQ_CONNECTION_STRING"],
            ssmParamConfigKeys: ["Ssm:RabbitMqConnectionStringParam", "SSM_RABBITMQ_CONNECTION_STRING_PARAM"],
            defaultSsmParameterName: DefaultRabbitMqConnectionStringParam,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Resolve o segredo simétrico HMAC-SHA256 de validação de tokens JWT de staff.
    /// </summary>
    public Task<string> ResolveJwtSecretAsync(CancellationToken cancellationToken = default) =>
        ResolveSecretAsync(
            configKeys: ["Jwt:Secret", "JWT_SECRET"],
            ssmParamConfigKeys: ["Ssm:JwtSecretParam", "SSM_JWT_SECRET_PARAM"],
            defaultSsmParameterName: DefaultJwtSecretParam,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Resolve um segredo verificando primeiro as chaves diretas de configuração/ambiente
    /// e consultando o AWS SSM Parameter Store quando configurado com prefixo "ssm:", caminho "/techchallenge/*"
    /// ou parâmetro SSM explícito.
    /// </summary>
    public async Task<string> ResolveSecretAsync(
        IEnumerable<string> configKeys,
        IEnumerable<string>? ssmParamConfigKeys = null,
        string? defaultSsmParameterName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configKeys);

        foreach (var key in configKeys)
        {
            var rawValue = _configuration[key];
            if (string.IsNullOrWhiteSpace(rawValue))
                continue;

            var trimmed = rawValue.Trim();
            if (trimmed.StartsWith(SsmPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var paramName = trimmed[SsmPrefix.Length..].Trim();
                return await GetParameterFromSsmAsync(paramName, cancellationToken);
            }

            if (IsSsmParameterPath(trimmed))
            {
                return await GetParameterFromSsmAsync(trimmed, cancellationToken);
            }

            return trimmed;
        }

        if (ssmParamConfigKeys is not null)
        {
            foreach (var paramKey in ssmParamConfigKeys)
            {
                var paramName = _configuration[paramKey];
                if (!string.IsNullOrWhiteSpace(paramName))
                {
                    return await GetParameterFromSsmAsync(paramName.Trim(), cancellationToken);
                }
            }
        }

        if (bool.TryParse(_configuration["Ssm:Enabled"] ?? _configuration["AWS_SSM_ENABLED"], out var ssmEnabled)
            && ssmEnabled
            && !string.IsNullOrWhiteSpace(defaultSsmParameterName))
        {
            return await GetParameterFromSsmAsync(defaultSsmParameterName, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Segredo ou string de conexão não configurada. Chaves verificadas: [{string.Join(", ", configKeys)}].");
    }

    /// <summary>
    /// Busca diretamente um parâmetro SecureString no AWS SSM Parameter Store com cache em memória.
    /// </summary>
    public async Task<string> GetParameterFromSsmAsync(string parameterName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            throw new ArgumentException("O nome do parâmetro SSM não pode ser vazio.", nameof(parameterName));

        var normalizedName = parameterName.Trim();
        if (_cache.TryGetValue(normalizedName, out var cachedValue))
            return cachedValue;

        _logger.LogInformation("Resolvendo parâmetro '{ParameterName}' no AWS SSM Parameter Store.", normalizedName);

        var client = _ssmClientFactory();
        var response = await client.GetParameterAsync(new GetParameterRequest
        {
            Name = normalizedName,
            WithDecryption = true
        }, cancellationToken);

        var value = response?.Parameter?.Value;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"O parâmetro SSM '{normalizedName}' retornou um valor vazio ou nulo.");

        _cache[normalizedName] = value;
        return value;
    }

    private static bool IsSsmParameterPath(string value) =>
        value.StartsWith("/techchallenge/", StringComparison.OrdinalIgnoreCase);
}

