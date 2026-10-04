using Mustask.Application.Features.Dictionaries.CreateLanguage;
using Mustask.Application.Features.Dictionaries.GetLanguages;
using Mustask.Application.Features.Dictionaries.SetDefaultLanguage;
using Mustask.Application.Features.Dictionaries.UpdateLanguage;
using Mustask.Application.Features.Process;
using Mustask.Application.Features.Process.GetProcessDefinition;
using Mustask.Application.Features.Process.SeedProcess;
using Mustask.Application.Features.UserDirectory.ApplySnapshotPage;
using Mustask.Application.Features.UserDirectory.ApplyUserChanges;
using Mustask.Application.Features.UserDirectory.BeginSnapshot;
using Mustask.Application.Features.UserDirectory.CompleteSnapshot;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Application;

/// <summary>
/// Регистрация handler'ов слайсов. Не инфраструктурный пакет —
/// `Microsoft.Extensions.DependencyInjection.Abstractions` разрешён
/// архитектурным тестом (запрещены только EF Core/Npgsql/AspNetCore/Aspire/Serilog).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // One handler per route of DictionaryEndpoints.cs.
        services.AddScoped<GetLanguagesHandler>();
        services.AddScoped<CreateLanguageHandler>();
        services.AddScoped<UpdateLanguageHandler>();
        services.AddScoped<SetDefaultLanguageHandler>();

        // Process (mod-001): the seed seam for the organization-creation flow
        // (IProcessSeed is the interface other modules depend on, not the slice).
        services.AddScoped<IProcessSeed, SeedProcessHandler>();
        services.AddScoped<GetProcessDefinitionHandler>();
        // One handler per route of UserDirectoryEndpoints.cs (mod-014).
        services.AddScoped<ApplyUserChangesHandler>();
        services.AddScoped<BeginSnapshotHandler>();
        services.AddScoped<ApplySnapshotPageHandler>();
        services.AddScoped<CompleteSnapshotHandler>();

        return services;
    }
}
