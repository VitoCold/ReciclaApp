using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class DetalleRegistroDisposicionPage : ContentPage
{
    public DetalleRegistroDisposicionPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnDetalleTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrADetalleRegistroAsync();
    }

    private async void OnResiduosTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrAResiduosDelRegistroAsync();
    }

    private async void OnAgregarEvidenciaClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Evidencia", "La captura de evidencia se conectará al selector/cámara en la siguiente iteración.", "Entendido");
    }
}
