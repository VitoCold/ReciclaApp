using ReciclaApp.Data;
using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class InicioRegistroPage : ContentPage
{
    private bool _catalogsLoaded;

    public InicioRegistroPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogsLoaded)
        {
            return;
        }

        try
        {
            ProyectoPicker.ItemsSource = await App.Database.GetProjectsAsync();
            SedePicker.ItemsSource = await App.Database.GetSitesAsync();
            ActividadPicker.ItemsSource = await App.Database.GetActivitiesAsync();
            _catalogsLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Datos locales", "No fue posible cargar los catálogos guardados en el dispositivo.", "Aceptar");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnProyectoChanged(object sender, EventArgs e)
    {
        if (ProyectoPicker.SelectedItem is not ProjectEntity project)
        {
            return;
        }

        try
        {
            ActividadPicker.SelectedItem = null;
            ActividadPicker.ItemsSource = await App.Database.GetActivitiesAsync(project.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        if (ProyectoPicker.SelectedItem is not ProjectEntity project ||
            ActividadPicker.SelectedItem is not ActivityEntity activity ||
            SedePicker.SelectedItem is not SiteEntity site)
        {
            await DisplayAlert("Datos incompletos", "Selecciona proyecto, actividad y sede para continuar.", "Entendido");
            return;
        }

        var userId = await App.Database.GetSettingAsync(DatabaseConstants.CurrentUserIdKey);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await DisplayAlert("Sesión local", "No se encontró el usuario activo. Inicia sesión nuevamente.", "Aceptar");
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        ContinuarButton.IsEnabled = false;

        try
        {
            var record = await App.Database.CreateRecordAsync(
                userId,
                project.Id,
                activity.Id,
                site.Id);

            await App.Database.SetSettingAsync(DatabaseConstants.CurrentRecordIdKey, record.Id);
            await AppNavigator.IrARegistrarResiduoAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("No se pudo guardar", "El registro no pudo guardarse localmente.", "Aceptar");
        }
        finally
        {
            ContinuarButton.IsEnabled = true;
        }
    }
}
