using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(SedeId), "sedeId")]
public partial class EditarAdministracionSedePage : ContentPage
{
    private Guid _sedeId;
    private bool _isLoading;
    private bool _loaded;

    public string SedeId
    {
        set => Guid.TryParse(value, out _sedeId);
    }

    public EditarAdministracionSedePage() => InitializeComponent();

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

        if (!_loaded)
        {
            await CargarAsync();
            _loaded = true;
        }
    }

    private async Task CargarAsync()
    {
        if (_sedeId == Guid.Empty)
        {
            HeaderLabel.Text = "Nueva sede";
            GuardarButton.Text = "Crear sede";
            return;
        }

        _isLoading = true;
        ContenidoLayout.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionSedeApiClient>();
            var sede = await api.ObtenerAsync(_sedeId);

            HeaderLabel.Text = "Editar sede";
            GuardarButton.Text = "Guardar cambios";
            CodigoEntry.Text = sede.Codigo;
            NombreEntry.Text = sede.Nombre;
            PuntosLabel.Text = sede.PuntosActivos.ToString();
            ControlesLabel.Text = sede.ControlesAbiertos.ToString();
            UsoBorder.IsVisible = true;
            ContenidoLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionSedePage>>()?
                .LogError(ex, "Error cargando sede {SedeId}.", _sedeId);
            MostrarError("No se pudo cargar la sede.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        var codigo = CodigoEntry.Text?.Trim();
        var nombre = NombreEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
        {
            MostrarError("Código y nombre son obligatorios.");
            return;
        }

        try
        {
            _isLoading = true;
            GuardarButton.IsEnabled = false;
            ErrorBorder.IsVisible = false;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var api = AppServices.Services.GetRequiredService<IAdministracionSedeApiClient>();
            var request = new GuardarAdministracionSedeRequest(codigo, nombre);

            if (_sedeId == Guid.Empty)
                await api.CrearAsync(request);
            else
                await api.ActualizarAsync(_sedeId, request);

            await DisplayAlert(
                "Sede guardada",
                _sedeId == Guid.Empty
                    ? "La sede fue creada correctamente."
                    : "Los datos de la sede fueron actualizados.",
                "Aceptar");

            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionSedePage>>()?
                .LogError(ex, "Error guardando sede {SedeId}.", _sedeId);
            MostrarError("No se pudo guardar la sede.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string marcador = "\"detail\":\"";
        var inicio = mensaje.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
        if (inicio < 0)
            return "Revisa los datos ingresados.";

        inicio += marcador.Length;
        var fin = mensaje.IndexOf('"', inicio);
        return fin > inicio ? mensaje[inicio..fin] : "Revisa los datos ingresados.";
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
        ContenidoLayout.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();
}
