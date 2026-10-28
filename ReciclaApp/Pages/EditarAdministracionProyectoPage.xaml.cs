using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ProyectoId), "proyectoId")]
public partial class EditarAdministracionProyectoPage : ContentPage
{
    private Guid _proyectoId;
    private bool _loaded;
    private bool _isLoading;

    public string ProyectoId { set => Guid.TryParse(value, out _proyectoId); }

    public EditarAdministracionProyectoPage() => InitializeComponent();

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
        if (_proyectoId == Guid.Empty) return;

        _isLoading = true;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;
        ContenidoLayout.IsVisible = false;

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            var item = await api.ObtenerProyectoAsync(_proyectoId);

            HeaderLabel.Text = "Editar proyecto";
            GuardarButton.Text = "Guardar cambios";
            CodigoEntry.Text = item.Codigo;
            NombreEntry.Text = item.Nombre;
            UsoBorder.IsVisible = true;
            UsoLabel.Text = $"{item.ActividadesActivas} actividad(es) activa(s) · {item.ControlesAbiertos} control(es) abierto(s)";
            ContenidoLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionProyectoPage>>()?
                .LogError(ex, "Error cargando proyecto {ProyectoId}.", _proyectoId);
            MostrarError("No se pudo cargar el proyecto.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsVisible = false;
            ActivityIndicator.IsRunning = false;
        }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isLoading) return;

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
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            var request = new GuardarAdministracionProyectoRequest(codigo, nombre);

            if (_proyectoId == Guid.Empty)
                await api.CrearProyectoAsync(request);
            else
                await api.ActualizarProyectoAsync(_proyectoId, request);

            await DisplayAlert("Proyecto guardado", "Los datos del proyecto fueron guardados correctamente.", "Aceptar");
            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch
        {
            MostrarError("No se pudo guardar el proyecto.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
        }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string marcador = "\"detail\":\"";
        var inicio = mensaje.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
        if (inicio < 0) return "Revisa los datos ingresados.";
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
