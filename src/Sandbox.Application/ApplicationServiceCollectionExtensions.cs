using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sandbox.Application.FaultInjection;
using Sandbox.Application.Sales;

namespace Sandbox.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registra las operaciones de ventas sobre el catálogo semilla, decoradas con el inyector de fallos, y valida su
    /// configuración al arrancar (design D9). Devuelve las opciones del inyector para que la raíz de composición las
    /// enlace a su configuración; así esta capa no depende de <c>Microsoft.Extensions.Configuration</c>.
    /// </summary>
    public static OptionsBuilder<FaultInjectionOptions> AddSandboxApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => SeedCatalog.CreateState());
        services.AddSingleton<SalesCommands>();
        services.AddSingleton<SalesQueries>();
        services.AddSingleton<FaultInjector>();
        services.AddSingleton<ISalesCommands>(provider => new FaultInjectingSalesCommands(
            provider.GetRequiredService<SalesCommands>(),
            provider.GetRequiredService<FaultInjector>()));
        services.AddSingleton<ISalesQueries>(provider => new FaultInjectingSalesQueries(
            provider.GetRequiredService<SalesQueries>(),
            provider.GetRequiredService<FaultInjector>()));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<FaultInjectionOptions>, FaultInjectionOptionsValidator>());

        return services.AddOptions<FaultInjectionOptions>().ValidateOnStart();
    }
}
