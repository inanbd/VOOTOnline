using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Generation;
using Voot.CodeGen.Infrastructure.Data;
using Voot.CodeGen.Infrastructure.Identity;
using Voot.CodeGen.Infrastructure.Packaging;
using Voot.CodeGen.Infrastructure.Repositories;
using Voot.CodeGen.Infrastructure.Runs;
using Voot.CodeGen.Infrastructure.Schema;
using Voot.CodeGen.Infrastructure.Security;
using Voot.CodeGen.Infrastructure.Sql;
using Voot.CodeGen.Infrastructure.Storage;
using Voot.CodeGen.Infrastructure.Time;

namespace Voot.CodeGen.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure implementations of the application's ports, the Dapper
    /// Identity stores, and the background generation worker.
    /// </summary>
    /// <param name="contentRootPath">Used to resolve a relative artifact storage path.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("Application")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings:Application to the database this application stores its own data in.");

        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));

        services.Configure<ArtifactStorageOptions>(configuration.GetSection("ArtifactStorage"));
        services.Configure<SeedAdministratorOptions>(configuration.GetSection("SeedAdministrator"));

        // ---- application ports ----
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IArchiveBuilder, ZipArchiveBuilder>();
        services.AddSingleton<ISchemaReader, SqlServerSchemaReader>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddScoped<ISqlScriptExecutor, SqlScriptExecutor>();
        services.AddScoped<IConnectionStringProtector, ConnectionStringProtector>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IGenerationRepository, GenerationRepository>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        services.AddSingleton<IArtifactStorage>(sp => new FileSystemArtifactStorage(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ArtifactStorageOptions>>(),
            contentRootPath));

        // The queue is shared between request threads and the worker, so it is a singleton.
        services.AddSingleton<GenerationQueue>();
        services.AddSingleton<IGenerationQueue>(sp => sp.GetRequiredService<GenerationQueue>());

        // ---- application services ----
        services.AddScoped<ProjectAccessService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ChangeSubmissionService>();
        services.AddScoped<RunHistoryService>();
        services.AddScoped<SchemaBrowsingService>();
        services.AddScoped<SchemaChangeService>();
        services.AddScoped<ArtifactDownloadService>();
        services.AddScoped<GenerationPipeline>();

        // ---- startup and maintenance ----
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<RunRecoveryService>();
        services.AddScoped<ArtifactRetentionService>();

        services.AddHostedService<GenerationWorker>();

        return services;
    }

    /// <summary>
    /// Registers the Dapper-backed Identity stores. The web layer calls AddIdentity itself,
    /// since the cookie pipeline is a web concern and lives in the ASP.NET Core framework
    /// reference. No Entity Framework is involved anywhere in this solution.
    /// </summary>
    public static IServiceCollection AddDapperIdentityStores(this IServiceCollection services)
    {
        services.AddScoped<IUserStore<ApplicationUser>, DapperUserStore>();
        services.AddScoped<IRoleStore<ApplicationRole>, DapperRoleStore>();

        return services;
    }
}
