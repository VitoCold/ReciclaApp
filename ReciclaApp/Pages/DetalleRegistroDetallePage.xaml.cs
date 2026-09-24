using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroId), "registroId")]
public partial class DetalleRegistroDetallePage : ContentPage
{
    private bool _isLoading;

    public string RegistroId { get; set; } = string.Empty;

    public DetalleRegistroDetallePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarDetalleAsync();
    }

    private async Task CargarDetalleAsync()
    {
        if (_isLoading)
            return;

        if (!Guid.TryParse(RegistroId, out var registroId))
        {
            MostrarError("No se recibió un identificador de registro válido.");
            return;
        }

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        DetalleActivityIndicator.IsVisible = true;
        DetalleActivityIndicator.IsRunning = true;

        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
            {
                await AppNavigator.IrAlLoginAsync();
                return;
            }

            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var registro = await apiClient.ObtenerRegistroAsync(registroId);

            FechaLabel.Text = registro.FechaRegistro.ToString("dd/MM/yyyy HH:mm");
            ProyectoLabel.Text = registro.Proyecto;
            ActividadLabel.Text = registro.Actividad;
            SedeLabel.Text = registro.Sede;
            CantidadResiduosLabel.Text = registro.Residuos.Count == 1
                ? "1 residuo"
                : $"{registro.Residuos.Count} residuos";
            ObservacionLabel.Text = string.IsNullOrWhiteSpace(registro.Observacion)
                ? "Sin observaciones"
                : registro.Observacion;

            var usuario = session.CurrentUser;
            RegistradoPorLabel.Text = usuario is null
                ? "Usuario"
                : string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

            AplicarEstado(registro.Estado);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MostrarError("El registro ya no existe o no tienes acceso a él.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error cargando el detalle del registro {RegistroId}.", RegistroId);
            MostrarError("No se pudo cargar el detalle del registro.");
        }
        finally
        {
            _isLoading = false;
            DetalleActivityIndicator.IsRunning = false;
            DetalleActivityIndicator.IsVisible = false;
        }
    }

    private void AplicarEstado(string estado)
    {
        EstadoLabel.Text = estado;

        switch (estado)
        {
            case "Borrador":
                EstadoBorder.BackgroundColor = Color.FromArgb("#FFF4DC");
                EstadoLabel.TextColor = Color.FromArgb("#F59E0B");
                break;
            case "Completado":
            case "Validado":
                EstadoBorder.BackgroundColor = Color.FromArgb("#E8F7EE");
                EstadoLabel.TextColor = Color.FromArgb("#079542");
                break;
            case "Observado":
            case "Anulado":
                EstadoBorder.BackgroundColor = Color.FromArgb("#FFE9E7");
                EstadoLabel.TextColor = Color.FromArgb("#E4312B");
                break;
            default:
                EstadoBorder.BackgroundColor = Color.FromArgb("#EAF2FF");
                EstadoLabel.TextColor = Color.FromArgb("#0753B7");
                break;
        }
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
