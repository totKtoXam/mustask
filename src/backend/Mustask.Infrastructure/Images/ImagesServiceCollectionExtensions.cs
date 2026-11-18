using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mustask.Application.Abstractions.Images;
using Mustask.Infrastructure.Images.Intake;
using Mustask.Infrastructure.Images.Intake.Processing;

namespace Mustask.Infrastructure.Images;

/// <summary>The image store's services (feat-078). Public for the test hosts, like the other modules' extensions.</summary>
public static class ImagesServiceCollectionExtensions
{
    public static IServiceCollection AddImagesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var options = new ImagesOptions();
            provider.GetService<IConfiguration>()?.GetSection(ImagesOptions.SectionName).Bind(options);

            return options;
        });
        services.AddSingleton(provider => new DecodeGate(provider.GetRequiredService<ImagesOptions>().Intake));
        services.AddSingleton<IImageCodec>(provider => new VipsCodec(provider.GetRequiredService<ImagesOptions>()));
        services.AddSingleton<IImageIntake, ImageIntake>();
        services.AddScoped<IImageStore, ImageStore>();
        services.AddScoped<IImageBinding, ImageBinding>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<UnboundImageSweeper>();
        services.AddHostedService(provider => provider.GetRequiredService<UnboundImageSweeper>());

        return services;
    }
}
