using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RetiroId), "retiroId")]
public partial class NuevaDisposicionFinalPage : ContentPage
{
    private Guid _retiroId;
    private bool _isLoading;
    private RetiroDetalleDto? _retiro;

    public string RetiroId
    {
        set => Guid.TryParse(value, out _retiroId);
    }

    public NuevaDisposicionFinalPage()
    {
        InitializeComponent();
        FechaPicker.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_retiroId == Guid.Empty)
        {
            MostrarError("No se recibió un retiro válido.");
            return;
        }

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        var usuario = session.CurrentUser;
        if (usuario?.Roles.Contains("AMBIENTAL") != true &&
            usuario?.Roles.Contains("RESPONSABLE_OPERATIVO") != true)
        {
            MostrarError("Tu rol no tiene permiso para documentar el destino final.");
            GuardarButton.IsEnabled = false;
            return;
        }

        if (_retiro is null)
            await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var retiroApi = AppServices.Services.GetRequiredService<IRetiroApiClient>();
            var disposicionApi = AppServices.Services.GetRequiredService<IDisposicionFinalApiClient>();
            var catalogosApi = AppServices.Services.GetRequiredService<IReciclaApiClient>();

            var retiroTask = retiroApi.ObtenerAsync(_retiroId);
            var tratamientosTask = disposicionApi.ListarTiposTratamientoAsync();
            var catalogosTask = catalogosApi.ObtenerCatalogosAsync();
            await Task.WhenAll(retiroTask, tratamientosTask, catalogosTask);

            _retiro = await retiroTask;
            var tratamientos = (await tratamientosTask).OrderBy(x => x.Nombre).ToArray();
            var catalogos = await catalogosTask;
            var gestores = catalogos.Empresas
                .Where(x => x.EsGestoraResiduos)
                .OrderBy(NombreEmpresa)
                .Select(x => new GestorOpcion(x.EmpresaId, NombreEmpresa(x)))
                .ToArray();

            RetiroLabel.Text = $"{_retiro.Codigo} · {_retiro.Sede} · {_retiro.PuntoAlmacenamiento}";
            TratamientoPicker.ItemsSource = tratamientos;
            GestorPicker.ItemsSource = gestores;

            FechaPicker.MinimumDate = _retiro.FechaRetiro.Date;
            FechaPicker.Date = DateTime.Today < _retiro.FechaRetiro.Date
                ? _retiro.FechaRetiro.Date
                : DateTime.Today;

            if (tratamientos.Length == 1)
                TratamientoPicker.SelectedIndex = 0;

            var gestorRetiro = gestores.FirstOrDefault(x => x.EmpresaId == _retiro.EmpresaGestoraId);
            if (gestorRetiro is not null)
                GestorPicker.SelectedItem = gestorRetiro;
            else if (gestores.Length == 1)
                GestorPicker.SelectedIndex = 0;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevaDisposicionFinalPage>>()?
                .LogError(ex, "Error preparando disposición final del retiro {RetiroId}.", _retiroId);
            MostrarError("No se pudo preparar el formulario de destino final.");
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
        if (_isLoading || _retiro is null)
            return;

        ErrorBorder.IsVisible = false;

        if (TratamientoPicker.SelectedItem is not TipoTratamientoDto tratamiento)
        {
            MostrarError("Selecciona el tipo de disposición, valorización o tratamiento.");
            return;
        }

        if (GestorPicker.SelectedItem is not GestorOpcion gestor)
        {
            MostrarError("Selecciona la empresa gestora.");
            return;
        }

        if (FechaPicker.Date < _retiro.FechaRetiro.Date)
        {
            MostrarError("La fecha de destino final no puede ser anterior al retiro.");
            return;
        }

        try
        {
            _isLoading = true;
            GuardarButton.IsEnabled = false;
            GuardarButton.Text = "Guardando...";
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var api = AppServices.Services.GetRequiredService<IDisposicionFinalApiClient>();
            await api.CrearAsync(_retiroId, new CrearDisposicionFinalRequest(
                gestor.EmpresaId,
                FechaPicker.Date,
                tratamiento.TipoTratamientoId,
                Limpiar(DocumentoEntry.Text),
                Limpiar(ObservacionEditor.Text)));

            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            MostrarError("El retiro ya tiene un destino final activo o su estado cambió.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            MostrarError("No tienes permiso para documentar este retiro.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevaDisposicionFinalPage>>()?
                .LogError(ex, "Error guardando destino final del retiro {RetiroId}.", _retiroId);
            MostrarError("No se pudo guardar el destino final. Revisa los datos e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
            GuardarButton.Text = "Guardar destino final";
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string NombreEmpresa(EmpresaDto empresa) =>
        string.IsNullOrWhiteSpace(empresa.NombreComercial) ? empresa.RazonSocial : empresa.NombreComercial;

    private static string? Limpiar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record GestorOpcion(Guid EmpresaId, string Nombre);
}
