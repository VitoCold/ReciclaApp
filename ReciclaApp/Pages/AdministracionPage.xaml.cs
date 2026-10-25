using Microsoft.Extensions.DependencyInjection;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionPage : ContentPage
{
    public AdministracionPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true)
            await AppNavigator.IrAControlesGeneracionAsync();
    }

    private void OnMenuTapped(object sender, TappedEventArgs e) => Shell.Current.FlyoutIsPresented = true;
    private async void OnUsuariosTapped(object sender, TappedEventArgs e) => await AppNavigator.IrAAdministracionUsuariosAsync();
}
