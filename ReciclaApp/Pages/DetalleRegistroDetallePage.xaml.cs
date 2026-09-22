using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class DetalleRegistroDetallePage : ContentPage
{
    public DetalleRegistroDetallePage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnResiduosTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrAResiduosDelRegistroAsync();
    }

    private async void OnDisposicionTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrADisposicionDelRegistroAsync();
    }
}
