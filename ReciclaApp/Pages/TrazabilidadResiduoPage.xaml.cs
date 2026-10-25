using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroResiduoId), "registroResiduoId")]
public partial class TrazabilidadResiduoPage : ContentPage
{
    private Guid _registroResiduoId;
    private bool _isLoading;

    public string RegistroResiduoId
    {
        set => Guid.TryParse(value, out _registroResiduoId);
    }

    public TrazabilidadResiduoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_registroResiduoId == Guid.Empty)
        {
            MostrarError("No se recibió un residuo válido.");
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
            var api = AppServices.Services.GetRequiredService<IInventarioApiClient>();
            var trazabilidad = await api.ObtenerTrazabilidadAsync(_registroResiduoId);

            ResiduoLabel.Text = trazabilidad.Residuo;
            ClasificacionLabel.Text = trazabilidad.Clasificacion;
            OrigenLabel.Text = $"{trazabilidad.Sede} · {trazabilidad.ControlCodigo}";
            CantidadLabel.Text = $"{trazabilidad.CantidadGenerada:0.###} {trazabilidad.Unidad} generados";
            EstadoLabel.Text = trazabilidad.EstadoActual;
            CierreLabel.Text = trazabilidad.CierreDocumentalCompleto
                ? "✓ Cierre documental completo"
                : "Cierre documental pendiente";

            var eventos = trazabilidad.Eventos
                .Select(x => new EventoVisual(
                    Icono(x.TipoCodigo),
                    x.Titulo,
                    x.TipoCodigo == "DESTINO_FINAL"
                        ? x.Fecha.ToString("dd/MM/yyyy")
                        : x.Fecha.ToString("dd/MM/yyyy HH:mm"),
                    string.IsNullOrWhiteSpace(x.Estado) ? string.Empty : x.Estado,
                    x.Cantidad.HasValue && !string.IsNullOrWhiteSpace(x.Unidad)
                        ? $"{x.Cantidad.Value:0.###} {x.Unidad}"
                        : string.Empty,
                    x.Detalle))
                .ToArray();

            BindingContext = new TrazabilidadBinding(eventos);
            ContenidoLayout.IsVisible = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MostrarError("El residuo no existe o ya no tienes acceso a su trazabilidad.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<TrazabilidadResiduoPage>>()?
                .LogError(ex, "Error cargando trazabilidad del residuo {RegistroResiduoId}.", _registroResiduoId);
            MostrarError("No se pudo cargar la trazabilidad del residuo.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string Icono(string tipo) => tipo switch
    {
        "GENERACION" => "📝",
        "ALMACENAMIENTO" => "📦",
        "RETIRO" => "🚚",
        "DESTINO_FINAL" => "✅",
        _ => "•"
    };

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record TrazabilidadBinding(IReadOnlyCollection<EventoVisual> Eventos);
    private sealed record EventoVisual(
        string Icono,
        string Titulo,
        string FechaTexto,
        string Estado,
        string CantidadTexto,
        string Detalle);
}
