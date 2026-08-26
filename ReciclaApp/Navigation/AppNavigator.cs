namespace ReciclaApp.Navigation;

public static class AppNavigator
{
    public static Task IrAlLoginAsync() => Shell.Current.GoToAsync(AppRoutes.Login);

    public static Task IrARegistrosAsync() => Shell.Current.GoToAsync(AppRoutes.Registros);

    public static Task IrADetalleRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleRegistroDetalle);

    public static Task IrAResiduosDelRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleRegistroResiduos);

    public static Task IrADisposicionDelRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleRegistroDisposicion);

    public static Task IrADetalleResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoDetalle);

    public static Task IrAFotosResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.DetalleResiduoFotos);

    public static Task IrAInicioRegistroAsync() => Shell.Current.GoToAsync(AppRoutes.InicioRegistro);

    public static Task IrARegistrarResiduoAsync() => Shell.Current.GoToAsync(AppRoutes.RegistrarResiduo);

    public static Task VolverAsync() => Shell.Current.GoToAsync("..");
}
