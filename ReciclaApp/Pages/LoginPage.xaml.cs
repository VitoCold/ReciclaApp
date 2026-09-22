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

        await AppNavigator.IrARegistrosAsync();
    }

    private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
    {
        ClaveEntry.IsPassword = !ClaveEntry.IsPassword;
    }
}
