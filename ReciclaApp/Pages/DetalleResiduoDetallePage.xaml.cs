using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class DetalleResiduoDetallePage : ContentPage
{
    public DetalleResiduoDetallePage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnFotosTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrAFotosResiduoAsync();
    }
}
