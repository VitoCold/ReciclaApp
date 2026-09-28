using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ControlId), "controlId")]
public partial class AsignarUsuarioControlPage : ContentPage
{
    private Guid _controlId;
    private CatalogosInicialDto? _catalogos;
    private bool _isSaving;

    public string ControlId
    {
        set => Guid.TryParse(value, out _controlId);
    }

    public AsignarUsuarioControlPage()
    {
        InitializeComponent();
        FechaDesdePicker.Date = DateTime.Today;
        FechaHastaPicker.Date = DateTime.Today.AddDays(15);
        RolPicker.SelectedIndex = 0;
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

        if (_catalogos is null)
            await CargarCatalogosAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        ErrorBorder.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;
        try
        {
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            _catalogos = await api.ObtenerCatalogosAsync();
            ActualizarUsuarios();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AsignarUsuarioControlPage>>()?
                .LogError(ex, "Error cargando usuarios asignables.");
            MostrarError("No se pudieron cargar los usuarios asignables.");
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void OnRolChanged(object sender, EventArgs e)
    {
        PrincipalRow.IsVisible = RolPicker.SelectedItem?.ToString() == "RESPONSABLE";
        if (!PrincipalRow.IsVisible)
            PrincipalSwitch.IsToggled = false;
        ActualizarUsuarios();
    }

    private void ActualizarUsuarios()
    {
        if (_catalogos is null)
            return;

        var rolControl = RolPicker.SelectedItem?.ToString() ?? "REGISTRADOR";
        var rolGlobal = rolControl == "RESPONSABLE" ? "RESPONSABLE_OPERATIVO" : "REGISTRADOR";
        UsuarioPicker.ItemsSource = _catalogos.UsuariosAsignables
            .Where(x => x.Roles.Contains(rolGlobal))
            .OrderBy(x => x.Nombre)
            .ToList();
        UsuarioPicker.SelectedItem = null;
    }

    private void OnFechaHastaToggled(object sender, ToggledEventArgs e)
    {
        FechaHastaPicker.IsEnabled = e.Value;
        FechaHastaPicker.Opacity = e.Value ? 1 : 0.45;
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isSaving)
            return;

        if (UsuarioPicker.SelectedItem is not UsuarioAsignableDto usuario)
        {
            MostrarError("Selecciona la persona que deseas asignar.");
            return;
        }

        var rol = RolPicker.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(rol))
        {
            MostrarError("Selecciona el rol dentro del control.");
            return;
        }

        var desde = FechaDesdePicker.Date;
        DateTime? hasta = TieneFechaHastaSwitch.IsToggled ? FechaHastaPicker.Date : null;
        if (hasta.HasValue && hasta.Value < desde)
        {
            MostrarError("La fecha hasta no puede ser anterior a la fecha desde.");
            return;
        }

        _isSaving = true;
        GuardarButton.IsEnabled = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;
        ErrorBorder.IsVisible = false;

        try
        {
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            await api.AsignarUsuarioControlGeneracionAsync(_controlId, new AsignarControlGeneracionUsuarioRequest(
                usuario.UsuarioId,
                rol,
                PrincipalSwitch.IsToggled,
                desde,
                hasta));

            await AppNavigator.VolverAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AsignarUsuarioControlPage>>()?
                .LogError(ex, "Error asignando usuario al control {ControlId}.", _controlId);
            MostrarError("No se pudo realizar la asignación. " + (ex is HttpRequestException http ? http.Message : "Inténtalo nuevamente."));
        }
        finally
        {
            _isSaving = false;
            GuardarButton.IsEnabled = true;
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
}
