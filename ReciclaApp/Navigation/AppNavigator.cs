namespace ReciclaApp.Navigation;

public static class AppNavigator
{
    public static Task IrAlLoginAsync() => Shell.Current.GoToAsync(AppRoutes.Login);

    public static Task IrAControlesGeneracionAsync() => Shell.Current.GoToAsync(AppRoutes.ControlesGeneracion);

    public static Task IrARegistrosAsync() => Shell.Current.GoToAsync(AppRoutes.Registros);

    public static Task IrAPerfilAsync() => Shell.Current.GoToAsync(AppRoutes.Perfil);

    public static Task IrANuevoControlGeneracionAsync() => Shell.Current.GoToAsync(AppRoutes.NuevoControlGeneracion);

    public static Task IrADetalleControlGeneracionAsync(Guid controlId) =>
        Shell.Current.GoToAsync($"{AppRoutes.DetalleControlGeneracion}?controlId={controlId}");

    public static Task IrAAsignarUsuarioControlAsync(Guid controlId) =>
        Shell.Current.GoToAsync($"{AppRoutes.AsignarUsuarioControl}?controlId={controlId}");

    public static Task IrADetalleRegistroControlAsync(Guid controlId, Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.DetalleRegistroControl}?controlId={controlId}&registroId={registroId}");

    public static Task IrADetalleRegistroAsync(Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.DetalleRegistro}?registroId={registroId}");

    // Se usa al terminar formularios hijos. Limpia la pila para que Atrás vuelva a Mis registros.
    public static async Task IrAResiduosDelRegistroAsync(Guid registroId)
    {
        await IrARegistrosAsync();
        await IrADetalleRegistroAsync(registroId);
    }

    public static Task IrADisposicionDelRegistroAsync(Guid registroId) =>
        IrADetalleRegistroAsync(registroId);

    public static Task IrADetalleResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoDetalle);

    public static Task IrAFotosResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoFotos);

    public static Task IrAInicioRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.InicioRegistro);

    public static Task IrARegistrarResiduoAsync(Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.RegistrarResiduo}?registroId={registroId}");

    public static Task IrARegistrarResiduoDesdeControlAsync(Guid controlId, Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.RegistrarResiduo}?controlId={controlId}&registroId={registroId}");

    public static Task IrAEditarResiduoAsync(Guid registroId, Guid registroResiduoId) =>
        Shell.Current.GoToAsync(
            $"{AppRoutes.RegistrarResiduo}?registroId={registroId}&registroResiduoId={registroResiduoId}");

    public static Task IrAEditarResiduoDesdeControlAsync(Guid controlId, Guid registroId, Guid registroResiduoId) =>
        Shell.Current.GoToAsync(
            $"{AppRoutes.RegistrarResiduo}?controlId={controlId}&registroId={registroId}&registroResiduoId={registroResiduoId}");

    public static Task VolverAsync() => Shell.Current.GoToAsync("..");
}
