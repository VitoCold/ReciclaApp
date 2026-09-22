using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class RegistrarResiduoPage : ContentPage
{
    public RegistrarResiduoPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnRegistrarResiduoClicked(object sender, EventArgs e)
    {
        if (TipoResiduoPicker.SelectedIndex < 0 || ResiduoPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Datos incompletos", "Selecciona el tipo de residuo y el residuo.", "Entendido");
            return;
        }

        if (!decimal.TryParse(CantidadEntry.Text, out var cantidad) || cantidad <= 0)
        {
            await DisplayAlert("Cantidad inválida", "Ingresa una cantidad mayor que cero.", "Entendido");
            return;
        }

        await DisplayAlert("Residuo registrado", "El residuo se agregó al registro actual.", "Aceptar");
        await AppNavigator.IrAResiduosDelRegistroAsync();
    }
}
