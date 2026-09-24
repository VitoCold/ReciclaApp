namespace ReciclaApp.Navigation;

public static class AppNavigator
{
    public static Task IrAlLoginAsync() => Shell.Current.GoToAsync(AppRoutes.Login);

    public static Task IrARegistrosAsync() => Shell.Current.GoToAsync(AppRoutes.Registros);

    public static Task IrAPerfilAsync() => Shell.Current.GoToAsync(AppRoutes.Perfil);

    public static Task IrADetalleRegistroAsync(Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.DetalleRegistro}?registroId={registroId}");

    // Compatibilidad con el flujo existente: residuos y disposición vuelven al detalle unificado.
    public static Task IrAResiduosDelRegistroAsync(Guid registroId) =>
        IrADetalleRegistroAsync(registroId);

    public static Task IrADisposicionDelRegistroAsync(Guid registroId) =>
        IrADetalleRegistroAsync(registroId);

    public static Task IrADetalleResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoDetalle);

    public static Task IrAFotosResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoFotos);

    public static Task IrAInicioRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.InicioRegistro);

    public static Task IrARegistrarResiduoAsync(Guid registroId) =>
        Shell.Current.GoToAsync($"{AppRoutes.RegistrarResiduo}?registroId={registroId}");

    public static Task VolverAsync() => Shell.Current.GoToAsync("..");
}
