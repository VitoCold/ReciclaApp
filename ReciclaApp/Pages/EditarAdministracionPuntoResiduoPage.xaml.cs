using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(PuntoResiduoId), "puntoResiduoId")]
public partial class EditarAdministracionPuntoResiduoPage : ContentPage
{
    private Guid _puntoResiduoId;
    private bool _isLoading;
    private bool _loaded;
    private AdministracionPuntoResiduoDto? _punto;

    public string PuntoResiduoId
    {
        set => Guid.TryParse(value, out _puntoResiduoId);
    }

    public EditarAdministracionPuntoResiduoPage() => InitializeComponent();

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
        ContenidoLayout.IsVisible = false;
        ErrorBorder.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionPuntoResiduoApiClient>();
            var catalogosTask = api.ObtenerCatalogosAsync();
            Task<AdministracionPuntoResiduoDto>? puntoTask = _puntoResiduoId == Guid.Empty
                ? null
                : api.ObtenerAsync(_puntoResiduoId);

            var catalogos = await catalogosTask;
            if (puntoTask is not null)
                _punto = await puntoTask;

            SedePicker.ItemsSource = catalogos.Sedes;
            TipoPicker.ItemsSource = catalogos.Tipos
                .Select(TipoVisual)
                .ToArray();

            if (_punto is null)
            {
                HeaderLabel.Text = "Nuevo punto";
                GuardarButton.Text = "Crear punto";
            }
            else
            {
                HeaderLabel.Text = "Editar punto";
                GuardarButton.Text = "Guardar cambios";
                CodigoEntry.Text = _punto.Codigo;
                NombreEntry.Text = _punto.Nombre;
                SedePicker.SelectedItem = catalogos.Sedes.FirstOrDefault(x => x.SedeId == _punto.SedeId);
                TipoPicker.SelectedItem = TipoVisual(_punto.Tipo);

                UsoBorder.IsVisible = true;
                UsoLabel.Text = ConstruirUso(_punto);

                if (_punto.TieneUsoHistorico)
                {
                    SedePicker.IsEnabled = false;
                    TipoPicker.IsEnabled = false;
                    BloqueoLabel.IsVisible = true;
                }
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
            AppServices.Services.GetService<ILogger<EditarAdministracionPuntoResiduoPage>>()?
                .LogError(ex, "Error cargando punto {PuntoResiduoId}.", _puntoResiduoId);
            MostrarError("No se pudo cargar el punto de residuo.");
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

        if (SedePicker.SelectedItem is not SedeDto sede)
        {
            MostrarError("Selecciona una sede.");
            return;
        }

        if (TipoPicker.SelectedItem is not string tipoVisual)
        {
            MostrarError("Selecciona un tipo.");
            return;
        }

        var codigo = CodigoEntry.Text?.Trim();
        var nombre = NombreEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
        {
            MostrarError("Código y nombre son obligatorios.");
            return;
        }

        var request = new GuardarAdministracionPuntoResiduoRequest(
            sede.SedeId,
            codigo,
            nombre,
            TipoCodigo(tipoVisual));

        try
        {
            _isLoading = true;
            GuardarButton.IsEnabled = false;
            ErrorBorder.IsVisible = false;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var api = AppServices.Services.GetRequiredService<IAdministracionPuntoResiduoApiClient>();

            if (_puntoResiduoId == Guid.Empty)
                await api.CrearAsync(request);
            else
                await api.ActualizarAsync(_puntoResiduoId, request);

            await DisplayAlert(
                "Punto guardado",
                _puntoResiduoId == Guid.Empty
                    ? "El punto fue creado correctamente."
                    : "Los datos del punto fueron actualizados.",
                "Aceptar");

            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<EditarAdministracionPuntoResiduoPage>>()?
                .LogError(ex, "Error guardando punto {PuntoResiduoId}.", _puntoResiduoId);
            MostrarError("No se pudo guardar el punto.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string ConstruirUso(AdministracionPuntoResiduoDto punto)
    {
        var partes = new List<string>();

        if (punto.ControlesAbiertos > 0)
            partes.Add($"{punto.ControlesAbiertos} control(es) abierto(s)");
        if (punto.TieneSaldoAlmacenado)
            partes.Add("saldo operativo almacenado");
        if (punto.TieneUsoHistorico)
            partes.Add("historial operativo existente");

        return partes.Count == 0 ? "Sin uso operativo." : string.Join(" · ", partes);
    }

    private static string TipoVisual(string tipo) => tipo switch
    {
        "GENERACION" => "Generación",
        "ALMACENAMIENTO" => "Almacenamiento",
        "AMBOS" => "Generación + almacenamiento",
        _ => tipo
    };

    private static string TipoCodigo(string visual) => visual switch
    {
        "Generación" => "GENERACION",
        "Almacenamiento" => "ALMACENAMIENTO",
        "Generación + almacenamiento" => "AMBOS",
        _ => visual.ToUpperInvariant()
    };

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
