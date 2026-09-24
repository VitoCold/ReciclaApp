using Microsoft.Extensions.DependencyInjection;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class RegistrosPage : ContentPage
{
    public RegistrosPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        FechaHeaderLabel.Text = DateTime.Now.ToString("dd/MM/yyyy");

        var services = Handler?.MauiContext?.Services;
        if (services is null)
            return;

        var session = services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated)
            await session.RestoreSessionAsync();

        var usuario = session.CurrentUser;
        if (usuario is null)
            return;

        UsuarioHeaderLabel.Text = string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private void OnMenuTapped(object sender, TappedEventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnPerfilTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrAPerfilAsync();
    }

    private async void OnNuevoRegistroClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrAInicioRegistroAsync();
    }
}
