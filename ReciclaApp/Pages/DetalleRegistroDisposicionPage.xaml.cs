using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroId), "registroId")]
public partial class DetalleRegistroDisposicionPage : ContentPage
{
    private readonly ObservableCollection<DisposicionEvidenciaDto> _evidencias = new();
    private bool _isLoading;

    public string RegistroId { get; set; } = string.Empty;

    public DetalleRegistroDisposicionPage()
    {
        InitializeComponent();
        EvidenciasCollectionView.ItemsSource = _evidencias;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarAsync();
    }

    private async Task CargarAsync()
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
        SinDisposicionView.IsVisible = false;
        DisposicionView.IsVisible = false;
        DisposicionActivityIndicator.IsVisible = true;
        DisposicionActivityIndicator.IsRunning = true;

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
            var disposicion = registro.Disposicion;

            _evidencias.Clear();

            if (disposicion is null)
            {
                SinDisposicionView.IsVisible = true;
                return;
            }

            DocumentoLabel.Text = string.IsNullOrWhiteSpace(disposicion.CodigoDocumento)
                ? "Sin documento"
                : disposicion.CodigoDocumento;
            FechaDisposicionLabel.Text = disposicion.FechaDisposicion?.ToString("dd/MM/yyyy") ?? "Sin fecha";
            EmpresaLabel.Text = string.IsNullOrWhiteSpace(disposicion.EmpresaDisposicion)
                ? "Sin empresa registrada"
                : disposicion.EmpresaDisposicion;
            ObservacionLabel.Text = string.IsNullOrWhiteSpace(disposicion.Observacion)
                ? "Sin observaciones"
                : disposicion.Observacion;

            foreach (var evidencia in disposicion.Evidencias.OrderByDescending(x => x.CreadoUtc))
                _evidencias.Add(evidencia);

            CantidadEvidenciasLabel.Text = _evidencias.Count == 1
                ? "1 evidencia"
                : $"{_evidencias.Count} evidencias";

            DisposicionView.IsVisible = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MostrarError("El registro no existe o ya no tienes acceso a él.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDisposicionPage>>()?
                .LogError(ex, "Error cargando disposición del registro {RegistroId}.", RegistroId);
            MostrarError("No se pudo cargar la información de disposición.");
        }
        finally
        {
            _isLoading = false;
            DisposicionActivityIndicator.IsRunning = false;
            DisposicionActivityIndicator.IsVisible = false;
        }
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnDetalleTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrADetalleRegistroAsync(registroId);
    }

    private async void OnResiduosTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrAResiduosDelRegistroAsync(registroId);
    }

    private async void OnVolverTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }
}
