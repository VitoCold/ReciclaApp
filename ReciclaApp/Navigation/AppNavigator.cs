namespace ReciclaApp.Navigation;

public static class AppNavigator
{
    private static readonly SemaphoreSlim NavigationGate = new(1, 1);

    public static Task IrAlLoginAsync() => IrARaizAsync(AppRoutes.Login);

    public static Task IrAControlesGeneracionAsync() => IrARaizAsync(AppRoutes.ControlesGeneracion);

    public static Task IrAStockResiduosAsync() => IrARaizAsync(AppRoutes.StockResiduos);

    public static Task IrARetirosAsync() => IrARaizAsync(AppRoutes.Retiros);

    public static Task IrAReportesAsync() => IrARaizAsync(AppRoutes.Reportes);

    public static Task IrARegistrosAsync() => IrARaizAsync(AppRoutes.Registros);

    public static Task IrAPerfilAsync() => IrARaizAsync(AppRoutes.Perfil);

    public static Task IrANuevoControlGeneracionAsync() => ApilarAsync(AppRoutes.NuevoControlGeneracion);

    public static Task IrADetalleControlGeneracionAsync(Guid controlId) =>
        ApilarAsync($"{AppRoutes.DetalleControlGeneracion}?controlId={controlId}");

    public static Task IrADetalleControlGeneracionDesdeCreacionAsync(Guid controlId) =>
        ReemplazarActualAsync($"{AppRoutes.DetalleControlGeneracion}?controlId={controlId}");

    public static Task IrAAsignarUsuarioControlAsync(Guid controlId) =>
        ApilarAsync($"{AppRoutes.AsignarUsuarioControl}?controlId={controlId}");

    public static Task IrADetalleRegistroControlAsync(Guid controlId, Guid registroId) =>
        ApilarAsync($"{AppRoutes.DetalleRegistroControl}?controlId={controlId}&registroId={registroId}");

    public static Task IrADetalleRegistroControlDesdeCapturaAsync(Guid controlId, Guid registroId) =>
        ReemplazarActualAsync($"{AppRoutes.DetalleRegistroControl}?controlId={controlId}&registroId={registroId}");

    public static Task IrANuevoRetiroAsync() => ApilarAsync(AppRoutes.NuevoRetiro);

    public static Task IrADetalleRetiroAsync(Guid retiroId) =>
        ApilarAsync($"{AppRoutes.DetalleRetiro}?retiroId={retiroId}");

    public static Task IrANuevaDisposicionFinalAsync(Guid retiroId) =>
        ApilarAsync($"{AppRoutes.NuevaDisposicionFinal}?retiroId={retiroId}");

    public static Task IrADetalleRetiroDesdeCreacionAsync(Guid retiroId) =>
        ReemplazarActualAsync($"{AppRoutes.DetalleRetiro}?retiroId={retiroId}");

    public static Task IrADetalleRegistroAsync(Guid registroId) =>
        ApilarAsync($"{AppRoutes.DetalleRegistro}?registroId={registroId}");

    public static Task IrAResiduosDelRegistroAsync(Guid registroId) =>
        IrARaizYDetalleAsync(
            AppRoutes.Registros,
            $"{AppRoutes.DetalleRegistro}?registroId={registroId}");

    public static Task IrADisposicionDelRegistroAsync(Guid registroId) =>
        IrADetalleRegistroAsync(registroId);

    public static Task IrADetalleResiduoAsync() => ApilarAsync(AppRoutes.DetalleResiduoDetalle);

    public static Task IrAFotosResiduoAsync() => ApilarAsync(AppRoutes.DetalleResiduoFotos);

    public static Task IrAInicioRegistroAsync() => ApilarAsync(AppRoutes.InicioRegistro);

    public static Task IrARegistrarResiduoAsync(Guid registroId) =>
        ApilarAsync($"{AppRoutes.RegistrarResiduo}?registroId={registroId}");

    public static Task IrARegistrarResiduoDesdeControlAsync(Guid controlId, Guid registroId) =>
        ApilarAsync($"{AppRoutes.RegistrarResiduo}?controlId={controlId}&registroId={registroId}");

    public static Task IrAEditarResiduoAsync(Guid registroId, Guid registroResiduoId) =>
        ApilarAsync(
            $"{AppRoutes.RegistrarResiduo}?registroId={registroId}&registroResiduoId={registroResiduoId}");

    public static Task IrAEditarResiduoDesdeControlAsync(Guid controlId, Guid registroId, Guid registroResiduoId) =>
        ApilarAsync(
            $"{AppRoutes.RegistrarResiduo}?controlId={controlId}&registroId={registroId}&registroResiduoId={registroResiduoId}");

    public static Task VolverAsync() => EjecutarSerializadoAsync(async shell =>
    {
        if (shell.FlyoutIsPresented)
        {
            shell.FlyoutIsPresented = false;
            return;
        }

        if (shell.Navigation.NavigationStack.Count <= 1)
            return;

        await shell.GoToAsync("..", true);
    });

    private static Task IrARaizAsync(string route) => EjecutarSerializadoAsync(async shell =>
    {
        shell.FlyoutIsPresented = false;

        if (EsRutaActual(shell, route))
            return;

        await shell.GoToAsync(route, false);
    });

    private static Task ApilarAsync(string route) => EjecutarSerializadoAsync(async shell =>
    {
        shell.FlyoutIsPresented = false;

        if (EsRutaActual(shell, route))
            return;

        await shell.GoToAsync(route, true);
    });

    private static Task ReemplazarActualAsync(string route) => EjecutarSerializadoAsync(async shell =>
    {
        shell.FlyoutIsPresented = false;

        if (EsRutaActual(shell, route))
            return;

        var navigation = shell.Navigation;
        var paginaAnterior = navigation.NavigationStack.LastOrDefault();
        var paginaRaiz = navigation.NavigationStack.FirstOrDefault();

        await shell.GoToAsync(route, true);

        if (paginaAnterior is not null &&
            !ReferenceEquals(paginaAnterior, paginaRaiz) &&
            navigation.NavigationStack.Contains(paginaAnterior))
        {
            navigation.RemovePage(paginaAnterior);
        }
    });

    private static Task IrARaizYDetalleAsync(string rootRoute, string detailRoute) =>
        EjecutarSerializadoAsync(async shell =>
        {
            shell.FlyoutIsPresented = false;

            await shell.GoToAsync(rootRoute, false);
            await shell.GoToAsync(detailRoute, true);
        });

    private static async Task EjecutarSerializadoAsync(Func<Shell, Task> action)
    {
        await NavigationGate.WaitAsync();
        try
        {
            var shell = Shell.Current;
            if (shell is null)
                return;

            await action(shell);
        }
        finally
        {
            NavigationGate.Release();
        }
    }

    private static bool EsRutaActual(Shell shell, string route)
    {
        var current = shell.CurrentState?.Location.OriginalString;
        if (string.IsNullOrWhiteSpace(current))
            return false;

        var normalizedCurrent = current.TrimStart('/');
        var normalizedTarget = route.TrimStart('/');

        if (route.StartsWith("//", StringComparison.Ordinal))
            return string.Equals(normalizedCurrent, normalizedTarget, StringComparison.OrdinalIgnoreCase);

        return normalizedCurrent.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }
}
