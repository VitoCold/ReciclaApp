using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ControlId), "controlId")]
public partial class ControlGeneracionDetallePage : ContentPage
{
    private Guid _controlId;
    private bool _isLoading;
    private ControlGeneracionDetalleDto? _control;
    private IReadOnlyCollection<RegistroControlListItemDto> _registros = Array.Empty<RegistroControlListItemDto>();

    public string ControlId
    {
        set => Guid.TryParse(value, out _controlId);
    }

    public ControlGeneracionDetallePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_controlId == Guid.Empty)
            return;

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
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            _control = await api.ObtenerControlGeneracionAsync(_controlId);
            _registros = await api.ListarRegistrosControlAsync(_controlId);

            // Un registro sin residuos todavía es un borrador técnico. No debe
            // mostrarse como un registro real del control hasta tener contenido.
            var registrosVisibles = _registros
                .Where(x => x.CantidadResiduos > 0)
                .ToArray();

            PintarControl(_control, registrosVisibles);
            ConfigurarAcciones(_control);
            BindingContext = new DetailBinding(_control.Usuarios, registrosVisibles);
            ContenidoLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<ControlGeneracionDetallePage>>()?
                .LogError(ex, "Error cargando control {ControlId}.", _controlId);
            MostrarError("No se pudo cargar el control. " + MensajeHttp(ex));
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void PintarControl(ControlGeneracionDetalleDto control, IReadOnlyCollection<RegistroControlListItemDto> registros)
    {
        CodigoLabel.Text = control.Codigo;
        EstadoLabel.Text = control.Estado;
        SedeEmpresaLabel.Text = $"{control.Sede} · {control.EmpresaResponsable}";
        ProyectoLabel.Text = control.Proyecto;
        ActividadLabel.Text = control.Actividad;
        TrabajoLabel.Text = string.IsNullOrWhiteSpace(control.DescripcionTrabajo) ? "Sin descripción" : control.DescripcionTrabajo;
        PuntoLabel.Text = string.IsNullOrWhiteSpace(control.PuntoGeneracion) ? "No especificado" : control.PuntoGeneracion;
        VigenciaLabel.Text = control.FechaFin.HasValue
            ? $"{control.FechaInicio:dd/MM/yyyy} al {control.FechaFin:dd/MM/yyyy}"
            : $"Desde {control.FechaInicio:dd/MM/yyyy}";

        ObservacionLabel.IsVisible = !string.IsNullOrWhiteSpace(control.Observacion);
        ObservacionLabel.Text = string.IsNullOrWhiteSpace(control.Observacion) ? null : $"Observación: {control.Observacion}";
        MotivoLabel.IsVisible = !string.IsNullOrWhiteSpace(control.MotivoUltimoCambio);
        MotivoLabel.Text = string.IsNullOrWhiteSpace(control.MotivoUltimoCambio) ? null : $"Último cambio: {control.MotivoUltimoCambio}";
        CantidadRegistrosLabel.Text = registros.Count == 1 ? "1 registro" : $"{registros.Count} registros";
    }

    private void ConfigurarAcciones(ControlGeneracionDetalleDto control)
    {
        var usuario = AppServices.Services.GetRequiredService<IAuthSessionService>().CurrentUser;
        if (usuario is null)
            return;

        var esAmbiental = usuario.Roles.Contains("AMBIENTAL");
        var now = DateTime.Now;
        var asignacion = control.Usuarios.FirstOrDefault(x =>
            x.UsuarioId == usuario.UsuarioId &&
            x.EsActivo &&
            x.FechaDesde <= now &&
            (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now));

        var esResponsableControl = asignacion?.RolControl == "RESPONSABLE";
        var esParticipante = asignacion?.RolControl is "RESPONSABLE" or "REGISTRADOR";
        var pendiente = control.Estado == "Pendiente de aprobación";

        AmbientalActions.IsVisible = esAmbiental && pendiente;
        AprobarButton.IsVisible = esAmbiental && pendiente;
        RechazarButton.IsVisible = esAmbiental && pendiente;
        GestionarEquipoButton.IsVisible = esResponsableControl;
        RegistrarButton.IsVisible = esParticipante && control.Estado == "Activo";
    }

    private async void OnAprobarClicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Aprobar control", "¿Confirmas que este control puede iniciar el registro de residuos?", "Aprobar", "Cancelar"))
            return;

        await EjecutarAccionAsync(async api => await api.AprobarControlGeneracionAsync(_controlId));
    }

    private async void OnRechazarClicked(object sender, EventArgs e)
    {
        var motivo = await DisplayPromptAsync("Rechazar control", "Indica el motivo para que el responsable conozca la observación:", "Rechazar", "Cancelar", "Motivo obligatorio", maxLength: 300);
        if (string.IsNullOrWhiteSpace(motivo))
            return;

        await EjecutarAccionAsync(async api => await api.RechazarControlGeneracionAsync(_controlId, new RechazarControlGeneracionRequest(motivo.Trim())));
    }

    private async Task EjecutarAccionAsync(Func<IReciclaApiClient, Task<ControlGeneracionDetalleDto>> accion)
    {
        ErrorBorder.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;
        try
        {
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            await accion(api);
            await CargarAsync();
        }
        catch (Exception ex)
        {
            MostrarError("No se pudo completar la acción. " + MensajeHttp(ex));
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnGestionarEquipoClicked(object sender, EventArgs e) =>
        await AppNavigator.IrAAsignarUsuarioControlAsync(_controlId);

    private async void OnRegistrarClicked(object sender, EventArgs e)
    {
        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            var usuario = session.CurrentUser;

            // Si el usuario abandonó anteriormente la captura antes de guardar
            // el primer residuo, reutilizamos ese borrador en vez de crear otro.
            var borradorVacio = usuario is null
                ? null
                : _registros
                    .Where(x =>
                        x.RegistradoPorUsuarioId == usuario.UsuarioId &&
                        x.CantidadResiduos == 0 &&
                        (string.Equals(x.Estado, "En proceso", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Estado, "Borrador", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(x => x.CreadoUtc)
                    .FirstOrDefault();

            Guid registroId;
            if (borradorVacio is not null)
            {
                registroId = borradorVacio.RegistroId;
            }
            else
            {
                var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
                var creado = await api.CrearRegistroEnControlAsync(_controlId, new CrearRegistroEnControlRequest(
                    $"MOB-{Guid.NewGuid():N}"[..12],
                    DateTime.Now,
                    null,
                    DeviceInfo.Current.Platform.ToString()));
                registroId = creado.RegistroId;
            }

            // Dejamos el detalle del registro debajo del formulario para que
            // guardar o cancelar vuelva al contexto del control y no a Mis registros.
            await AppNavigator.IrADetalleRegistroControlAsync(_controlId, registroId);
            await AppNavigator.IrARegistrarResiduoDesdeControlAsync(_controlId, registroId);
        }
        catch (Exception ex)
        {
            MostrarError("No se pudo iniciar el registro. " + MensajeHttp(ex));
        }
    }

    private async void OnRegistroTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid registroId)
        {
            await AppNavigator.IrADetalleRegistroControlAsync(_controlId, registroId);
            return;
        }

        if (Guid.TryParse(e.Parameter?.ToString(), out registroId))
            await AppNavigator.IrADetalleRegistroControlAsync(_controlId, registroId);
    }

    private static string MensajeHttp(Exception ex) => ex is HttpRequestException http && !string.IsNullOrWhiteSpace(http.Message)
        ? http.Message
        : "Inténtalo nuevamente.";

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record DetailBinding(
        IReadOnlyCollection<ControlGeneracionUsuarioDto> Usuarios,
        IReadOnlyCollection<RegistroControlListItemDto> Registros);
}
