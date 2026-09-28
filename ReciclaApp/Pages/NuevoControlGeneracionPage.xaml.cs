using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class NuevoControlGeneracionPage : ContentPage
{
    private CatalogosInicialDto? _catalogos;
    private bool _isSaving;

    public NuevoControlGeneracionPage()
    {
        InitializeComponent();
        FechaInicioPicker.Date = DateTime.Today;
        FechaFinPicker.Date = DateTime.Today.AddMonths(1);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        if (session.CurrentUser is null || !session.CurrentUser.Roles.Contains("RESPONSABLE_OPERATIVO"))
        {
            await DisplayAlert("Sin permiso", "Solo un responsable operativo puede crear controles de generación.", "Aceptar");
            await AppNavigator.VolverAsync();
            return;
        }

        if (_catalogos is null)
            await CargarCatalogosAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        ErrorBorder.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            _catalogos = await api.ObtenerCatalogosAsync();

            SedePicker.ItemsSource = _catalogos.Sedes.OrderBy(x => x.Nombre).ToList();
            EmpresaPicker.ItemsSource = _catalogos.Empresas
                .Where(x => !x.EsGestoraResiduos)
                .OrderBy(x => x.NombreComercial ?? x.RazonSocial)
                .ToList();
            ProyectoPicker.ItemsSource = _catalogos.Proyectos.OrderBy(x => x.Nombre).ToList();
            ActividadPicker.ItemsSource = Array.Empty<ActividadDto>();
            PuntoPicker.ItemsSource = Array.Empty<PuntoResiduoDto>();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevoControlGeneracionPage>>()?
                .LogError(ex, "Error cargando catálogos para nuevo control.");
            MostrarError("No se pudieron cargar los datos para crear el control. " + MensajeHttp(ex));
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void OnSedeChanged(object sender, EventArgs e)
    {
        if (_catalogos is null || SedePicker.SelectedItem is not SedeDto sede)
        {
            PuntoPicker.ItemsSource = Array.Empty<PuntoResiduoDto>();
            return;
        }

        PuntoPicker.ItemsSource = _catalogos.PuntosResiduo
            .Where(x => x.SedeId == sede.SedeId && (x.Tipo == "GENERACION" || x.Tipo == "AMBOS"))
            .OrderBy(x => x.Nombre)
            .ToList();
        PuntoPicker.SelectedItem = null;
    }

    private void OnProyectoChanged(object sender, EventArgs e)
    {
        if (_catalogos is null || ProyectoPicker.SelectedItem is not ProyectoDto proyecto)
        {
            ActividadPicker.ItemsSource = null;
            return;
        }

        ActividadPicker.ItemsSource = _catalogos.Actividades
            .Where(x => x.ProyectoId == proyecto.ProyectoId || x.ProyectoId is null)
            .OrderBy(x => x.Nombre)
            .ToList();
        ActividadPicker.SelectedItem = null;
    }

    private void OnFechaFinToggled(object sender, ToggledEventArgs e)
    {
        FechaFinPicker.IsEnabled = e.Value;
        FechaFinPicker.Opacity = e.Value ? 1 : 0.45;
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isSaving)
            return;

        ErrorBorder.IsVisible = false;

        if (SedePicker.SelectedItem is not SedeDto sede ||
            EmpresaPicker.SelectedItem is not EmpresaDto empresa ||
            ProyectoPicker.SelectedItem is not ProyectoDto proyecto ||
            ActividadPicker.SelectedItem is not ActividadDto actividad)
        {
            MostrarError("Selecciona sede, empresa, proyecto y actividad.");
            return;
        }

        var fechaInicio = FechaInicioPicker.Date;
        DateTime? fechaFin = TieneFechaFinSwitch.IsToggled ? FechaFinPicker.Date : null;
        if (fechaFin.HasValue && fechaFin.Value < fechaInicio)
        {
            MostrarError("La fecha de fin no puede ser anterior a la fecha de inicio.");
            return;
        }

        _isSaving = true;
        GuardarButton.IsEnabled = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var punto = PuntoPicker.SelectedItem as PuntoResiduoDto;
            var request = new CrearControlGeneracionRequest(
                sede.SedeId,
                empresa.EmpresaId,
                proyecto.ProyectoId,
                actividad.ActividadId,
                punto?.PuntoResiduoId,
                TextoONull(DescripcionTrabajoEntry.Text),
                fechaInicio,
                fechaFin,
                TextoONull(ObservacionEditor.Text));

            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var creado = await api.CrearControlGeneracionAsync(request);

            await AppNavigator.IrADetalleControlGeneracionDesdeCreacionAsync(creado.ControlGeneracionId);
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevoControlGeneracionPage>>()?
                .LogError(ex, "Error creando control de generación.");
            MostrarError("No se pudo crear el control. " + MensajeHttp(ex));
        }
        finally
        {
            _isSaving = false;
            GuardarButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string? TextoONull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string MensajeHttp(Exception ex) => ex is HttpRequestException http && !string.IsNullOrWhiteSpace(http.Message)
        ? http.Message
        : "Inténtalo nuevamente.";

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();
}
