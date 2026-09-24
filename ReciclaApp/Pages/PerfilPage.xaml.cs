using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class PerfilPage : ContentPage
{
    public PerfilPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var services = Handler?.MauiContext?.Services;
        if (services is null)
            return;

        var session = services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        var usuario = session.CurrentUser;
        if (usuario is null)
            return;

        NombreLabel.Text = string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        UsuarioLabel.Text = $"@{usuario.Usuario}";
        LoginLabel.Text = usuario.Usuario;
        EmailLabel.Text = string.IsNullOrWhiteSpace(usuario.Email) ? "Sin correo registrado" : usuario.Email;
        RolesLabel.Text = usuario.Roles.Count == 0 ? "Sin rol asignado" : string.Join(", ", usuario.Roles);
        SedesLabel.Text = usuario.SedeIds.Count == 1 ? "1 sede" : $"{usuario.SedeIds.Count} sedes";
        InitialsLabel.Text = GetInitials(usuario.Nombres, usuario.Apellidos);
    }

    private static string GetInitials(string nombres, string? apellidos)
    {
        var first = string.IsNullOrWhiteSpace(nombres) ? string.Empty : nombres.Trim()[0].ToString();
        var last = string.IsNullOrWhiteSpace(apellidos) ? string.Empty : apellidos.Trim()[0].ToString();
        var initials = (first + last).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(initials) ? "US" : initials;
    }

    private void OnMenuTapped(object sender, TappedEventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Cerrar sesión",
            "¿Deseas cerrar tu sesión en ReciclaApp?",
            "Cerrar sesión",
            "Cancelar");

        if (!confirm)
            return;

        var services = Handler?.MauiContext?.Services;
        if (services is null)
            return;

        try
        {
            var session = services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            Shell.Current.FlyoutIsPresented = false;
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            services.GetService<ILogger<PerfilPage>>()?
                .LogError(ex, "Error cerrando sesión.");
            await DisplayAlert("Error", "No se pudo cerrar la sesión.", "Aceptar");
        }
    }
}
