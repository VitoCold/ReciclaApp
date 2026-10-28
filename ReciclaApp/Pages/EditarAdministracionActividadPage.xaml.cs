using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ActividadId), "actividadId")]
public partial class EditarAdministracionActividadPage : ContentPage
{
    private Guid _actividadId;
    private bool _loaded;
    private bool _isLoading;
    private AdministracionActividadDto? _actividad;

    public string ActividadId { set => Guid.TryParse(value, out _actividadId); }

    public EditarAdministracionActividadPage() => InitializeComponent();

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
        _isLoading = true;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;
        ContenidoLayout.IsVisible = false;

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            var catalogosTask = api.ObtenerCatalogosActividadesAsync();
            Task<AdministracionActividadDto>? actividadTask = _actividadId == Guid.Empty
                ? null
                : api.ObtenerActividadAsync(_actividadId);

            var catalogos = await catalogosTask;
            if (actividadTask is not null)
                _actividad = await actividadTask;

            var proyectos = catalogos.Proyectos.ToList();
            if (_actividad?.ProyectoId is Guid proyectoId &&
                proyectos.All(x => x.ProyectoId != proyectoId))
            {
                proyectos.Add(new ProyectoDto(
                    proyectoId,
                    _actividad.ProyectoCodigo ?? "LEGADO",
                    _actividad.ProyectoNombre ?? "Proyecto histórico"));
            }

            ProyectoPicker.ItemsSource = proyectos.OrderBy(x => x.Nombre).ToArray();

            if (_actividad is null)
            {
                HeaderLabel.Text = "Nueva actividad";
                GuardarButton.Text = "Crear actividad";
            }
            else
            {
                HeaderLabel.Text = "Editar actividad";
                GuardarButton.Text = "Guardar cambios";
                CodigoEntry.Text = _actividad.Codigo;
                NombreEntry.Text = _actividad.Nombre;

                if (_actividad.ProyectoId is Guid actual)
                {
                    ProyectoPicker.SelectedItem = ((IEnumerable<ProyectoDto>)ProyectoPicker.ItemsSource)
                        .FirstOrDefault(x => x.ProyectoId == actual);
                }

                UsoBorder.IsVisible = true;
                UsoLabel.Text = _actividad.ControlesAbiertos > 0
                    ? $"{_actividad.ControlesAbiertos} control(es) abierto(s)"
                    : _actividad.TieneUsoHistorico ? "Con historial operativo" : "Sin uso operativo";

                if (_actividad.TieneUsoHistorico)
                {
                    ProyectoPicker.IsEnabled = false;
                    ProyectoBloqueadoLabel.IsVisible = true;
                }
            }

            ContenidoLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionActividadPage>>()?
                .LogError(ex, "Error cargando actividad {ActividadId}.", _actividadId);
            MostrarError("No se pudo cargar la actividad.");
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

        if (ProyectoPicker.SelectedItem is not ProyectoDto proyecto)
        {
            MostrarError("Selecciona un proyecto.");
            return;
        }

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
            var request = new GuardarAdministracionActividadRequest(proyecto.ProyectoId, codigo, nombre);

            if (_actividadId == Guid.Empty)
                await api.CrearActividadAsync(request);
            else
                await api.ActualizarActividadAsync(_actividadId, request);

            await DisplayAlert("Actividad guardada", "Los datos de la actividad fueron guardados correctamente.", "Aceptar");
            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch
        {
            MostrarError("No se pudo guardar la actividad.");
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
