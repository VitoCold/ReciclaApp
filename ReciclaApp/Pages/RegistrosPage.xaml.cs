using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class RegistrosPage : ContentPage
{
    public RegistrosPage()
    {
        InitializeComponent();
    }

    private async void OnNuevoRegistroClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrAInicioRegistroAsync();
    }

    private async void OnVerDetalleClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrADetalleRegistroAsync();
    }
}
