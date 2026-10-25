using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(UsuarioId), "usuarioId")]
public partial class EditarAdministracionUsuarioPage : ContentPage
{
    private Guid _usuarioId;
    private bool _isLoading;
    private bool _loaded;
    private AdministracionUsuarioDto? _usuario;
    private List<RolSeleccion> _roles = [];
    private List<SedeSeleccion> _sedes = [];

    public string UsuarioId
    {
        set => Guid.TryParse(value, out _usuarioId);
    }

    public EditarAdministracionUsuarioPage() => InitializeComponent();

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
        ErrorBorder.IsVisible = false;
        ContenidoLayout.IsVisible = false;

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();
            var catalogosTask = api.ObtenerCatalogosAsync();
            Task<AdministracionUsuarioDto>? usuarioTask = _usuarioId == Guid.Empty
                ? null
                : api.ObtenerAsync(_usuarioId);

            var catalogos = await catalogosTask;
            if (usuarioTask is not null)
                _usuario = await usuarioTask;

            _roles = catalogos.Roles
                .Select(x => new RolSeleccion(
                    x.RolId,
                    x.Codigo,
                    x.Nombre,
                    _usuario?.Roles.Any(r => r.RolId == x.RolId) == true))
                .ToList();

            _sedes = catalogos.Sedes
                .Select(x => new SedeSeleccion(
                    x.SedeId,
                    x.Codigo,
                    x.Nombre,
                    _usuario?.Sedes.Any(s => s.SedeId == x.SedeId) == true))
                .ToList();

            BindableLayout.SetItemsSource(RolesStack, _roles);
            BindableLayout.SetItemsSource(SedesStack, _sedes);

            if (_usuario is null)
            {
                HeaderLabel.Text = "Nuevo usuario";
                GuardarButton.Text = "Crear usuario";
                PasswordSection.IsVisible = true;
                RestablecerClaveButton.IsVisible = false;
            }
            else
            {
                HeaderLabel.Text = "Editar usuario";
                UsuarioEntry.Text = _usuario.Usuario;
                NombresEntry.Text = _usuario.Nombres;
                ApellidosEntry.Text = _usuario.Apellidos;
                EmailEntry.Text = _usuario.Email;
                PasswordSection.IsVisible = false;
                RestablecerClaveButton.IsVisible = true;
            }

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
            AppServices.Services.GetService<ILogger<EditarAdministracionUsuarioPage>>()?
                .LogError(ex, "Error cargando formulario administrativo de usuario.");
            MostrarError("No se pudo cargar la información del usuario.");
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

        var usuario = UsuarioEntry.Text?.Trim();
        var nombres = NombresEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(nombres))
        {
            MostrarError("Usuario y nombres son obligatorios.");
            return;
        }

        var roles = _roles.Where(x => x.Seleccionado).Select(x => x.RolId).ToArray();
        if (roles.Length == 0)
        {
            MostrarError("Selecciona al menos un rol.");
            return;
        }

        var sedes = _sedes.Where(x => x.Seleccionado).Select(x => x.SedeId).ToArray();
        var request = new GuardarAdministracionUsuarioRequest(
            usuario,
            nombres,
            ApellidosEntry.Text,
            EmailEntry.Text,
            roles,
            sedes,
            _usuarioId == Guid.Empty ? PasswordEntry.Text : null);

        try
        {
            _isLoading = true;
            GuardarButton.IsEnabled = false;
            ErrorBorder.IsVisible = false;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();

            if (_usuarioId == Guid.Empty)
                _usuario = await api.CrearAsync(request);
            else
                _usuario = await api.ActualizarAsync(_usuarioId, request);

            await DisplayAlert(
                "Usuario guardado",
                _usuarioId == Guid.Empty
                    ? "El usuario fue creado correctamente."
                    : "Los datos del usuario fueron actualizados.",
                "Aceptar");

            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionUsuarioPage>>()?
                .LogError(ex, "Error guardando usuario {UsuarioId}.", _usuarioId);
            MostrarError("No se pudo guardar el usuario.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnRestablecerClaveClicked(object sender, EventArgs e)
    {
        if (_isLoading || _usuarioId == Guid.Empty)
            return;

        var password = await DisplayPromptAsync(
            "Restablecer contraseña",
            "Ingresa una contraseña temporal de al menos 8 caracteres.",
            "Restablecer",
            "Cancelar",
            "Contraseña temporal",
            maxLength: 80,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(password))
            return;

        if (password.Length < 8)
        {
            await DisplayAlert("Contraseña inválida", "Debe tener al menos 8 caracteres.", "Aceptar");
            return;
        }

        var confirmar = await DisplayAlert(
            "Confirmar restablecimiento",
            "La contraseña actual dejará de funcionar y el usuario deberá cambiar la nueva al iniciar sesión.",
            "Restablecer",
            "Cancelar");

        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            RestablecerClaveButton.IsEnabled = false;
            var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();
            await api.RestablecerClaveAsync(_usuarioId, password);
            await DisplayAlert("Contraseña restablecida", "La nueva contraseña temporal fue guardada.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionUsuarioPage>>()?
                .LogError(ex, "Error restableciendo contraseña de {UsuarioId}.", _usuarioId);
            await DisplayAlert("No se pudo restablecer", "No se pudo actualizar la contraseña.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            RestablecerClaveButton.IsEnabled = true;
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
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed class RolSeleccion(int rolId, string codigo, string nombre, bool seleccionado)
    {
        public int RolId { get; } = rolId;
        public string Codigo { get; } = codigo;
        public string Nombre { get; } = nombre;
        public bool Seleccionado { get; set; } = seleccionado;
    }

    private sealed class SedeSeleccion(Guid sedeId, string codigo, string nombre, bool seleccionado)
    {
        public Guid SedeId { get; } = sedeId;
        public string Codigo { get; } = codigo;
        public string Nombre { get; } = nombre;
        public bool Seleccionado { get; set; } = seleccionado;
    }
}
