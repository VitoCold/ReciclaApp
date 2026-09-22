using System.Globalization;
using ReciclaApp.Data;
using ReciclaApp.Navigation;

namespace ReciclaApp.Pages;

public partial class RegistrarResiduoPage : ContentPage
{
    private bool _catalogLoaded;

    public RegistrarResiduoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogLoaded)
        {
            return;
        }

        try
        {
            TipoResiduoPicker.ItemsSource = await App.Database.GetWasteTypesAsync();
            ResiduoPicker.ItemsSource = await App.Database.GetWasteCatalogAsync();
            _catalogLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Datos locales", "No fue posible cargar el catálogo de residuos guardado en el dispositivo.", "Aceptar");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private async void OnTipoResiduoChanged(object sender, EventArgs e)
    {
        if (TipoResiduoPicker.SelectedItem is not WasteTypeEntity wasteType)
        {
            return;
        }

        try
        {
            ResiduoPicker.SelectedItem = null;
            ResiduoPicker.ItemsSource = await App.Database.GetWasteCatalogAsync(wasteType.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void OnRegistrarResiduoClicked(object sender, EventArgs e)
    {
        if (TipoResiduoPicker.SelectedItem is not WasteTypeEntity ||
            ResiduoPicker.SelectedItem is not WasteCatalogEntity waste)
        {
            await DisplayAlert("Datos incompletos", "Selecciona el tipo de residuo y el residuo.", "Entendido");
            return;
        }

        if (!TryParseQuantity(CantidadEntry.Text, out var quantity) || quantity <= 0)
        {
            await DisplayAlert("Cantidad inválida", "Ingresa una cantidad mayor que cero.", "Entendido");
            return;
        }

        var recordId = await App.Database.GetSettingAsync(DatabaseConstants.CurrentRecordIdKey);
        if (string.IsNullOrWhiteSpace(recordId))
        {
            await DisplayAlert("Registro no disponible", "No se encontró un registro activo. Inicia uno nuevo.", "Aceptar");
            await AppNavigator.IrAInicioRegistroAsync();
            return;
        }

        RegistrarButton.IsEnabled = false;

        try
        {
            var item = await App.Database.CreateRecordWasteAsync(
                recordId,
                waste.Id,
                quantity,
                waste.DefaultUnit);

            await App.Database.SetSettingAsync(DatabaseConstants.CurrentRecordWasteIdKey, item.Id);

            await DisplayAlert(
                "Residuo guardado",
                "El residuo quedó almacenado en el dispositivo y está listo para sincronizarse cuando haya conexión.",
                "Aceptar");

            await AppNavigator.IrAResiduosDelRegistroAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("No se pudo guardar", "El residuo no pudo almacenarse localmente.", "Aceptar");
        }
        finally
        {
            RegistrarButton.IsEnabled = true;
        }
    }

    private static bool TryParseQuantity(string? text, out double quantity)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out quantity))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out quantity);
    }
}
