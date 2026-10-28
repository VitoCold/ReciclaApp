using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(TipoResiduoId), "tipoResiduoId")]
public partial class EditarAdministracionTipoResiduoPage : ContentPage
{
    private Guid _id;
    private bool _loaded;
    public string TipoResiduoId { set => Guid.TryParse(value, out _id); }

    public EditarAdministracionTipoResiduoPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync()) { await AppNavigator.IrAlLoginAsync(); return; }
        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true) { await AppNavigator.IrAControlesGeneracionAsync(); return; }
        if (_loaded || _id == Guid.Empty) return;
        _loaded = true;

        try
        {
            var item = await AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>().ObtenerTipoAsync(_id);
            HeaderLabel.Text = "Editar tipo";
            GuardarButton.Text = "Guardar cambios";
            CodigoEntry.Text = item.Codigo;
            NombreEntry.Text = item.Nombre;
            UsoLabel.Text = $"{item.ResiduosActivos} residuo(s) activo(s)";
            UsoLabel.IsVisible = true;
        }
        catch { MostrarError("No se pudo cargar el tipo de residuo."); }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        var codigo = CodigoEntry.Text?.Trim();
        var nombre = NombreEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre)) { MostrarError("Código y nombre son obligatorios."); return; }

        try
        {
            GuardarButton.IsEnabled = false;
            var api = AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>();
            var req = new GuardarAdministracionTipoResiduoRequest(codigo, nombre);
            if (_id == Guid.Empty) await api.CrearTipoAsync(req); else await api.ActualizarTipoAsync(_id, req);
            await DisplayAlert("Tipo guardado", "Los datos fueron guardados correctamente.", "Aceptar");
            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) { MostrarError(ExtraerDetalle(ex.Message)); }
        catch { MostrarError("No se pudo guardar el tipo."); }
        finally { GuardarButton.IsEnabled = true; }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string m = "\"detail\":\""; var i = mensaje.IndexOf(m, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "Revisa los datos ingresados."; i += m.Length; var j = mensaje.IndexOf('"', i);
        return j > i ? mensaje[i..j] : "Revisa los datos ingresados.";
    }

    private void MostrarError(string m) { ErrorLabel.Text = m; ErrorBorder.IsVisible = true; }
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();
}
