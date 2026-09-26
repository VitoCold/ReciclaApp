using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RetiroId), "retiroId")]
public partial class RetiroDetallePage : ContentPage
{
    private Guid _retiroId;
    private bool _isLoading;

    public string RetiroId
    {
        set => Guid.TryParse(value, out _retiroId);
    }

    public RetiroDetallePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_retiroId == Guid.Empty)
        {
            MostrarError("No se recibió un retiro válido.");
            return;
        }

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        ContenidoLayout.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var api = AppServices.Services.GetRequiredService<IRetiroApiClient>();
            var retiro = await api.ObtenerAsync(_retiroId);

            CodigoLabel.Text = retiro.Codigo;
            FechaLabel.Text = retiro.FechaRetiro.ToString("dd/MM/yyyy HH:mm");
            EstadoLabel.Text = retiro.Estado;
            UbicacionLabel.Text = $"{retiro.Sede} · {retiro.PuntoAlmacenamiento}";
            GestorLabel.Text = $"Gestor: {retiro.EmpresaGestora}";
            TotalesLabel.Text = retiro.Totales.Count == 0
                ? "Sin cantidades"
                : string.Join(" · ", retiro.Totales.Select(x => $"{x.Cantidad:0.###} {x.Unidad}"));
            CreadoPorLabel.Text = $"Registrado por {retiro.CreadoPor} · {retiro.CreadoUtc.ToLocalTime():dd/MM/yyyy HH:mm}";

            DocumentoLabel.Text = string.IsNullOrWhiteSpace(retiro.DocumentoTransporte)
                ? "Documento: no registrado"
                : $"Documento: {retiro.DocumentoTransporte}";
            VehiculoLabel.Text = string.Join(" · ", new[]
            {
                string.IsNullOrWhiteSpace(retiro.Vehiculo) ? null : retiro.Vehiculo,
                string.IsNullOrWhiteSpace(retiro.Placa) ? null : retiro.Placa
            }.Where(x => !string.IsNullOrWhiteSpace(x)).DefaultIfEmpty("Vehículo: no registrado"));
            ConductorLabel.Text = string.IsNullOrWhiteSpace(retiro.Conductor)
                ? "Conductor: no registrado"
                : $"Conductor: {retiro.Conductor}";
            ObservacionLabel.Text = string.IsNullOrWhiteSpace(retiro.Observacion)
                ? "Sin observaciones"
                : retiro.Observacion;

            var detalles = retiro.Detalles
                .Select(x => new DetalleVisual(
                    x.Residuo,
                    $"{x.ControlCodigo} · {x.Clasificacion}",
                    $"{x.Cantidad:0.###} {x.Unidad}"))
                .ToArray();
            CantidadLabel.Text = detalles.Length == 1 ? "1 residuo" : $"{detalles.Length} residuos";
            BindingContext = new DetailBinding(detalles);
            ContenidoLayout.IsVisible = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RetiroDetallePage>>()?
                .LogError(ex, "Error cargando retiro {RetiroId}.", _retiroId);
            MostrarError("No se pudo cargar el retiro.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record DetailBinding(IReadOnlyCollection<DetalleVisual> Detalles);
    private sealed record DetalleVisual(string Residuo, string OrigenTexto, string CantidadTexto);
}
