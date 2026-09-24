using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class InicioRegistroPage : ContentPage
{
    private CatalogosInicialDto? _catalogos;
    private bool _isLoading;

    public InicioRegistroPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogos is null)
            await CargarCatalogosAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
            {
                await AppNavigator.IrAlLoginAsync();
                return;
            }

            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            _catalogos = await apiClient.ObtenerCatalogosAsync();

            ProyectoPicker.ItemsSource = _catalogos.Proyectos.ToList();
            SedePicker.ItemsSource = _catalogos.Sedes.ToList();
            ActividadPicker.ItemsSource = Array.Empty<ActividadDto>();

            if (_catalogos.Sedes.Count == 0)
                MostrarError("Tu usuario no tiene sedes asignadas. Solicita una sede antes de crear registros.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<InicioRegistroPage>>()?
                .LogError(ex, "Error cargando catálogos para iniciar un registro.");
            MostrarError("No se pudieron cargar los proyectos, actividades y sedes.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private void OnProyectoChanged(object sender, EventArgs e)
    {
        if (_catalogos is null || ProyectoPicker.SelectedItem is not ProyectoDto proyecto)
        {
            ActividadPicker.ItemsSource = Array.Empty<ActividadDto>();
            ActividadPicker.SelectedItem = null;
            return;
        }

        var actividades = _catalogos.Actividades
            .Where(x => x.ProyectoId is null || x.ProyectoId == proyecto.ProyectoId)
            .OrderBy(x => x.Nombre)
            .ToList();

        ActividadPicker.ItemsSource = actividades;
        ActividadPicker.SelectedItem = null;
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        ErrorBorder.IsVisible = false;

        if (ProyectoPicker.SelectedItem is not ProyectoDto proyecto ||
            ActividadPicker.SelectedItem is not ActividadDto actividad ||
            SedePicker.SelectedItem is not SedeDto sede)
        {
            MostrarError("Selecciona proyecto, actividad y sede para continuar.");
            return;
        }

        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var request = new CrearRegistroRequest(
                CodigoLocal: Guid.NewGuid().ToString("N"),
                FechaRegistro: DateTime.Now,
                ProyectoId: proyecto.ProyectoId,
                ActividadId: actividad.ActividadId,
                SedeId: sede.SedeId,
                Observacion: string.IsNullOrWhiteSpace(ObservacionEditor.Text)
                    ? null
                    : ObservacionEditor.Text.Trim(),
                OrigenDispositivo: DeviceInfo.Current.Platform.ToString());

            var registro = await apiClient.CrearRegistroAsync(request);
            await AppNavigator.IrARegistrarResiduoAsync(registro.RegistroId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<InicioRegistroPage>>()?
                .LogError(ex, "Error creando el registro inicial.");
            MostrarError("No se pudo crear el registro. Revisa los datos e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        ContinuarButton.IsEnabled = !busy;
        ProyectoPicker.IsEnabled = !busy;
        ActividadPicker.IsEnabled = !busy;
        SedePicker.IsEnabled = !busy;
        ObservacionEditor.IsEnabled = !busy;
        RegistroActivityIndicator.IsVisible = busy;
        RegistroActivityIndicator.IsRunning = busy;
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnVolverTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }
}
