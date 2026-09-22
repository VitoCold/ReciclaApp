using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class DetalleRegistroResiduosPage : ContentPage
{
    public DetalleRegistroResiduosPage()
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

    private async void OnDisposicionTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrADisposicionDelRegistroAsync();
    }

    private async void OnAgregarResiduoClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrARegistrarResiduoAsync();
    }

    private async void OnResiduoTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrADetalleResiduoAsync();
    }
}
