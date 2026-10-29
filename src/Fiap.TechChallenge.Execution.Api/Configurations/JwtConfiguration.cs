using System.Text;
using Fiap.TechChallenge.Execution.Infrastructure.Aws;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Fiap.TechChallenge.Execution.Api.Configurations;

public static class JwtConfiguration
{
    public const string DefaultDevSecret = "dev-only-jwt-secret-key-at-least-32-bytes-long!";

    public static IServiceCollection AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, runtimeConfig) =>
            {
                var effectiveConfig = runtimeConfig ?? configuration;
                var secret = ResolveJwtSecretOrFallback(effectiveConfig);
                var issuer = effectiveConfig["Jwt:Issuer"] ?? "fiap-tech-challenge";
                var audience = effectiveConfig["Jwt:Audience"] ?? "fiap-api";
                var keyBytes = Encoding.UTF8.GetBytes(secret);

                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static string ResolveJwtSecretOrFallback(IConfiguration configuration)
    {
        var configured = configuration["Jwt:Secret"]
            ?? configuration["JWT_SECRET"]
            ?? configuration["Ssm:JwtSecretParam"]
            ?? configuration["SSM_JWT_SECRET_PARAM"];

        var ssmEnabled = bool.TryParse(
            configuration["Ssm:Enabled"] ?? configuration["AWS_SSM_ENABLED"],
            out var enabled) && enabled;

        if (!string.IsNullOrWhiteSpace(configured) || ssmEnabled)
        {
            var resolver = new SsmSecretResolver(configuration);
            return resolver.ResolveJwtSecretAsync().GetAwaiter().GetResult();
        }

        return DefaultDevSecret;
    }
}
