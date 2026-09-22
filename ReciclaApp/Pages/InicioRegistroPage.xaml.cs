using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class InicioRegistroPage : ContentPage
{
    public InicioRegistroPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        if (ProyectoPicker.SelectedIndex < 0 ||
            ActividadPicker.SelectedIndex < 0 ||
            SedePicker.SelectedIndex < 0)
        {
            await DisplayAlert("Datos incompletos", "Selecciona proyecto, actividad y sede para continuar.", "Entendido");
            return;
        }

        await AppNavigator.IrARegistrarResiduoAsync();
    }
}
