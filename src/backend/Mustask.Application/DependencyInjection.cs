using Mustask.Application.Features.Dictionaries.CreateLanguage;
using Mustask.Application.Features.Dictionaries.GetLanguages;
using Mustask.Application.Features.Dictionaries.SetDefaultLanguage;
using Mustask.Application.Features.Dictionaries.UpdateLanguage;
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

        return services;
    }
}
