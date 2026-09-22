using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class DetalleResiduoFotosPage : ContentPage
{
    public DetalleResiduoFotosPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnDetalleTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrADetalleResiduoAsync();
    }

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Tomar foto", "El botón ya está conectado. La integración con cámara y permisos queda para la siguiente iteración.", "Entendido");
    }
}
