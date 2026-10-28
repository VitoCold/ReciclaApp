using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(EmpresaId), "empresaId")]
public partial class EditarAdministracionEmpresaPage : ContentPage
{
    private Guid _id;
    private bool _loaded;
    private AdministracionEmpresaDto? _item;

    public string EmpresaId { set => Guid.TryParse(value, out _id); }

    public EditarAdministracionEmpresaPage() => InitializeComponent();

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

        if (_loaded)
            return;

        _loaded = true;
        if (_id != Guid.Empty)
            await CargarAsync();
    }

    private async Task CargarAsync()
    {
        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionEmpresaApiClient>();
            _item = await api.ObtenerAsync(_id);

            HeaderLabel.Text = "Editar empresa";
            GuardarButton.Text = "Guardar cambios";
            CodigoEntry.Text = _item.Codigo;
            RucEntry.Text = _item.Ruc;
            RazonSocialEntry.Text = _item.RazonSocial;
            NombreComercialEntry.Text = _item.NombreComercial;
            DireccionEditor.Text = _item.Direccion;
            TelefonoEntry.Text = _item.Telefono;
            EmailEntry.Text = _item.Email;
            EorsSwitch.IsToggled = _item.EsGestoraResiduos;
            AutorizacionEntry.Text = _item.NumeroAutorizacion;

            UsoBorder.IsVisible = true;
            UsoLabel.Text = ConstruirUso(_item);

            if (_item.TieneUsoHistorico)
            {
                EorsSwitch.IsEnabled = false;
                RolBloqueadoLabel.IsVisible = true;
            }
        }
        catch
        {
            MostrarError("No se pudo cargar la empresa.");
        }
    }

    private void OnEorsToggled(object sender, ToggledEventArgs e) =>
        AutorizacionBorder.IsVisible = e.Value;

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        var codigo = CodigoEntry.Text?.Trim();
        var razonSocial = RazonSocialEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(razonSocial))
        {
            MostrarError("Código y razón social son obligatorios.");
            return;
        }

        var request = new GuardarAdministracionEmpresaRequest(
            codigo,
            RucEntry.Text,
            razonSocial,
            NombreComercialEntry.Text,
            DireccionEditor.Text,
            TelefonoEntry.Text,
            EmailEntry.Text,
            EorsSwitch.IsToggled,
            EorsSwitch.IsToggled ? AutorizacionEntry.Text : null);

        try
        {
            GuardarButton.IsEnabled = false;
            ErrorBorder.IsVisible = false;

            var api = AppServices.Services.GetRequiredService<IAdministracionEmpresaApiClient>();
            if (_id == Guid.Empty)
                await api.CrearAsync(request);
            else
                await api.ActualizarAsync(_id, request);

            await DisplayAlert(
                "Empresa guardada",
                EorsSwitch.IsToggled
                    ? "La EO-RS fue guardada correctamente."
                    : "La empresa responsable fue guardada correctamente.",
                "Aceptar");

            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            MostrarError(ExtraerDetalle(ex.Message));
        }
        catch
        {
            MostrarError("No se pudo guardar la empresa.");
        }
        finally
        {
            GuardarButton.IsEnabled = true;
        }
    }

    private static string ConstruirUso(AdministracionEmpresaDto x)
    {
        var partes = new List<string>();
        if (x.ControlesAbiertos > 0) partes.Add($"{x.ControlesAbiertos} control(es) abierto(s)");
        if (x.RetirosRegistrados > 0) partes.Add($"{x.RetirosRegistrados} retiro(s)");
        if (x.DisposicionesRegistradas > 0) partes.Add($"{x.DisposicionesRegistradas} disposición(es)");
        if (x.TieneUsoHistorico && partes.Count == 0) partes.Add("historial operativo existente");
        return partes.Count == 0 ? "Sin uso operativo." : string.Join(" · ", partes);
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

    private async void OnBackTapped(object sender, TappedEventArgs e) =>
        await AppNavigator.VolverAsync();
}
