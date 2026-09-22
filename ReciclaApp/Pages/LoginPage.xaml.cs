using ReciclaApp.Data;
using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ClaveEntry.Text))
        {
            await DisplayAlert("Datos incompletos", "Ingresa usuario y clave para continuar.", "Entendido");
            return;
        }

        try
        {
            // La clave no se persiste en SQLite. Cuando exista autenticación remota,
            // el token deberá ir en SecureStorage y la BD solo conservará el perfil.
            var user = await App.Database.GetOrCreateLocalUserAsync(
                UsuarioEntry.Text,
                UsuarioEntry.Text);

            await App.Database.SetSettingAsync(DatabaseConstants.CurrentUserIdKey, user.Id);
            await AppNavigator.IrARegistrosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("No se pudo iniciar", "No fue posible preparar los datos locales. Intenta nuevamente.", "Aceptar");
        }
    }

    private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
    {
        ClaveEntry.IsPassword = !ClaveEntry.IsPassword;
    }
}
