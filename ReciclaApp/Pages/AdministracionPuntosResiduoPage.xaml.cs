using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionPuntosResiduoPage : ContentPage
{
    private IReadOnlyCollection<AdministracionPuntoResiduoDto> _puntos = Array.Empty<AdministracionPuntoResiduoDto>();
    private bool _isLoading;

    public AdministracionPuntosResiduoPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true)
        {
            await AppNavigator.IrAControlesGeneracionAsync();
            return;
        }

        await CargarAsync(_puntos.Count == 0);
    }

    private async Task CargarAsync(bool showLoader)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;

        if (showLoader)
        {
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
        }

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionPuntoResiduoApiClient>();
            _puntos = await api.ListarAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionPuntosResiduoPage>>()?
                .LogError(ex, "Error cargando puntos de residuo administrativos.");
            MostrarError("No se pudieron cargar los puntos de residuo.");
        }
        finally
        {
            _isLoading = false;
            PuntosRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void AplicarFiltro()
    {
        var filtro = BuscarEntry.Text?.Trim();

        PuntosCollectionView.ItemsSource = _puntos
            .Where(x =>
                string.IsNullOrWhiteSpace(filtro) ||
                x.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Codigo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.SedeNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Tipo.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .Select(x => new PuntoVisual(
                x.PuntoResiduoId,
                x.Nombre,
                $"{x.SedeNombre} · {x.SedeCodigo}",
                $"Código: {x.Codigo}",
                TipoNombre(x.Tipo),
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                ConstruirUso(x)))
            .ToArray();
    }

    private static string ConstruirUso(AdministracionPuntoResiduoDto x)
    {
        var partes = new List<string>();

        if (x.ControlesAbiertos > 0)
            partes.Add($"{x.ControlesAbiertos} control(es) abierto(s)");
        if (x.TieneSaldoAlmacenado)
            partes.Add("con saldo almacenado");
        if (x.TieneUsoHistorico && partes.Count == 0)
            partes.Add("con historial operativo");

        return partes.Count == 0 ? "Sin uso operativo" : string.Join(" · ", partes);
    }

    private static string TipoNombre(string tipo) => tipo switch
    {
        "GENERACION" => "Generación",
        "ALMACENAMIENTO" => "Almacenamiento",
        "AMBOS" => "Generación + almacenamiento",
        _ => tipo
    };

    private async void OnNuevoClicked(object sender, EventArgs e) =>
        await AppNavigator.IrANuevoAdministracionPuntoResiduoAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button button &&
            Guid.TryParse(button.CommandParameter?.ToString(), out var puntoResiduoId))
        {
            await AppNavigator.IrAEditarAdministracionPuntoResiduoAsync(puntoResiduoId);
        }
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button ||
            !Guid.TryParse(button.CommandParameter?.ToString(), out var puntoResiduoId))
            return;

        var punto = _puntos.FirstOrDefault(x => x.PuntoResiduoId == puntoResiduoId);
        if (punto is null)
            return;

        var activar = !punto.EsActivo;
        var confirmar = await DisplayAlert(
            activar ? "Activar punto" : "Desactivar punto",
            activar
                ? $"¿Activar nuevamente {punto.Nombre}?"
                : $"¿Desactivar {punto.Nombre}? No estará disponible para nuevas operaciones.",
            activar ? "Activar" : "Desactivar",
            "Cancelar");

        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionPuntoResiduoApiClient>();
            await api.CambiarEstadoAsync(puntoResiduoId, activar);
            _puntos = await api.ListarAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede cambiar el estado", ExtraerDetalle(ex.Message), "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionPuntosResiduoPage>>()?
                .LogError(ex, "Error cambiando estado de punto {PuntoResiduoId}.", puntoResiduoId);
            await DisplayAlert("No se pudo actualizar", "No se pudo cambiar el estado del punto.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string marcador = "\"detail\":\"";
        var inicio = mensaje.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
        if (inicio < 0)
            return "El punto todavía tiene dependencias operativas.";

        inicio += marcador.Length;
        var fin = mensaje.IndexOf('"', inicio);
        return fin > inicio ? mensaje[inicio..fin] : "El punto todavía tiene dependencias operativas.";
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record PuntoVisual(
        Guid PuntoResiduoId,
        string Nombre,
        string SedeTexto,
        string CodigoTexto,
        string TipoTexto,
        string EstadoTexto,
        string AccionEstadoTexto,
        string UsoTexto);
}
