namespace ReciclaApp.Services;

public static class AppServices
{
    private static IServiceProvider? _services;

    public static IServiceProvider Services => _services
        ?? throw new InvalidOperationException("El contenedor de servicios de la aplicación aún no está disponible.");

    public static void Initialize(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }
}
