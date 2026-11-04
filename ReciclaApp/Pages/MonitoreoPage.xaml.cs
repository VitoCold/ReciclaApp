using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class MonitoreoPage : ContentPage
{
    private bool _loading;
    private bool _catalogosCargados;

    private static readonly PeriodoVisual[] Periodos =
    [
        new("Hoy", 0),
        new("Últimos 7 días", 6),
        new("Últimos 30 días", 29),
        new("Todo", null)
    ];

    private static readonly EstadoVisual[] Estados =
    [
        new("Todos", null),
        new("Pendiente de traslado", "PENDIENTE_TRASLADO"),
        new("Traslado parcial", "TRASLADO_PARCIAL"),
        new("En almacén temporal", "EN_ALMACEN_TEMPORAL"),
        new("Retirado", "RETIRADO")
    ];

    public MonitoreoPage()
    {
        InitializeComponent();
        PeriodoPicker.ItemsSource = Periodos;
        PeriodoPicker.SelectedIndex = 0;
        EstadoPicker.ItemsSource = Estados;
        EstadoPicker.SelectedIndex = 0;
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

        var roles = session.CurrentUser?.Roles ?? Array.Empty<string>();
        if (!roles.Contains("AMBIENTAL") && !roles.Contains("ADMINISTRADOR"))
        {
            await AppNavigator.IrAControlesGeneracionAsync();
            return;
        }

        await CargarAsync(showLoader: true);
    }

    private async Task CargarAsync(bool showLoader)
    {
        if (_loading)
            return;

        _loading = true;
        ErrorBorder.IsVisible = false;

        if (showLoader)
        {
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
        }

        try
        {
            var periodo = PeriodoPicker.SelectedItem as PeriodoVisual ?? Periodos[0];
            DateTime? desdeUtc = null;
            DateTime? hastaUtc = null;

            if (periodo.DiasAtras.HasValue)
            {
                var desdeLocal = DateTime.Today.AddDays(-periodo.DiasAtras.Value);
                var hastaLocal = DateTime.Today.AddDays(1).AddTicks(-1);
                desdeUtc = desdeLocal.ToUniversalTime();
                hastaUtc = hastaLocal.ToUniversalTime();
            }

            var sede = SedePicker.SelectedItem as SedeVisual;
            var clasificacion = ClasificacionPicker.SelectedItem as ClasificacionVisual;
            var estado = EstadoPicker.SelectedItem as EstadoVisual;

            var filtro = new MonitoreoActividadFiltroRequest(
                desdeUtc,
                hastaUtc,
                sede?.SedeId,
                clasificacion?.ClasificacionResiduoId,
                estado?.Codigo,
                BuscarSearchBar.Text,
                150);

            var api = AppServices.Services.GetRequiredService<IMonitoreoApiClient>();
            var data = await api.ObtenerActividadAsync(filtro);

            if (!_catalogosCargados)
            {
                SedePicker.ItemsSource = new[]
                    {
                        new SedeVisual(null, "Todas las sedes")
                    }
                    .Concat(data.Sedes.Select(x => new SedeVisual(x.SedeId, x.Nombre)))
                    .ToArray();
                SedePicker.SelectedIndex = 0;

                ClasificacionPicker.ItemsSource = new[]
                    {
                        new ClasificacionVisual(null, "Todas")
                    }
                    .Concat(data.Clasificaciones.Select(x =>
                        new ClasificacionVisual(x.ClasificacionResiduoId, x.Nombre)))
                    .ToArray();
                ClasificacionPicker.SelectedIndex = 0;

                _catalogosCargados = true;
            }

            TotalLabel.Text = data.Resumen.Total.ToString();
            PeligrososLabel.Text = data.Resumen.Peligrosos.ToString();
            NoPeligrososLabel.Text = data.Resumen.NoPeligrosos.ToString();
            PendientesLabel.Text = data.Resumen.PendientesTraslado.ToString();

            var items = data.Items
                .Select(x => new ActividadVisual(
                    x.RegistroResiduoId,
                    x.RegistradoUtc.ToLocalTime().ToString("dd/MM/yyyy · HH:mm"),
                    x.Residuo,
                    x.Clasificacion,
                    $"{x.Cantidad:0.###} {x.Unidad}",
                    $"{x.Sede} · {x.Proyecto} / {x.Actividad} · {x.ControlCodigo}",
                    $"Registrado por {x.RegistradoPor}",
                    x.EstadoOperativo,
                    EvidenciaTexto(x.TieneFoto, x.TieneGps)))
                .ToArray();

            Collection.ItemsSource = items;
            ResultadosLabel.Text = items.Length == 1
                ? "1 resultado"
                : $"{items.Length} resultados";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            ErrorLabel.Text = "Tu usuario no tiene acceso a Monitoreo.";
            ErrorBorder.IsVisible = true;
        }
        catch
        {
            ErrorLabel.Text = "No se pudo cargar la actividad reciente. Verifica la conexión e inténtalo nuevamente.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _loading = false;
            Refresh.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string EvidenciaTexto(bool foto, bool gps) =>
        (foto, gps) switch
        {
            (true, true) => "📷 Foto · 📍 GPS",
            (true, false) => "📷 Foto",
            (false, true) => "📍 GPS",
            _ => "Sin evidencia"
        };

    private void OnMenuTapped(object sender, TappedEventArgs e) =>
        Shell.Current.FlyoutIsPresented = true;

    private async void OnActualizarTapped(object sender, TappedEventArgs e) =>
        await CargarAsync(showLoader: false);

    private async void OnAplicarFiltrosClicked(object sender, EventArgs e) =>
        await CargarAsync(showLoader: true);

    private async void OnBuscarPressed(object sender, EventArgs e) =>
        await CargarAsync(showLoader: true);

    private async void OnRefreshRequested(object sender, EventArgs e) =>
        await CargarAsync(showLoader: false);

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(e.Parameter?.ToString(), out var id))
            await AppNavigator.IrATrazabilidadResiduoAsync(id);
    }

    private sealed record PeriodoVisual(string Nombre, int? DiasAtras);
    private sealed record SedeVisual(Guid? SedeId, string Nombre);
    private sealed record ClasificacionVisual(int? ClasificacionResiduoId, string Nombre);
    private sealed record EstadoVisual(string Nombre, string? Codigo);

    private sealed record ActividadVisual(
        Guid RegistroResiduoId,
        string FechaTexto,
        string Residuo,
        string Clasificacion,
        string CantidadTexto,
        string OrigenTexto,
        string UsuarioTexto,
        string EstadoOperativo,
        string EvidenciaTexto);
}
