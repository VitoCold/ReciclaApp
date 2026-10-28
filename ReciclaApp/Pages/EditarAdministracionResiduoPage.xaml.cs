using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ResiduoId), "residuoId")]
public partial class EditarAdministracionResiduoPage : ContentPage
{
    private Guid _id;
    private bool _loaded;
    private AdministracionResiduoCatalogoDto? _item;
    public string ResiduoId { set => Guid.TryParse(value, out _id); }

    public EditarAdministracionResiduoPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync()) { await AppNavigator.IrAlLoginAsync(); return; }
        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true) { await AppNavigator.IrAControlesGeneracionAsync(); return; }
        if (_loaded) return;
        _loaded = true;
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>();
            var catTask = api.ObtenerCatalogosAsync();
            Task<AdministracionResiduoCatalogoDto>? itemTask = _id == Guid.Empty ? null : api.ObtenerResiduoAsync(_id);

            var cat = await catTask;
            if (itemTask is not null) _item = await itemTask;

            var tipos = cat.TiposResiduo.ToList();
            if (_item is not null && tipos.All(x => x.TipoResiduoId != _item.TipoResiduoId))
                tipos.Add(new TipoResiduoDto(_item.TipoResiduoId, _item.TipoCodigo, _item.TipoNombre));

            TipoPicker.ItemsSource = tipos.OrderBy(x => x.Nombre).ToArray();
            ClasificacionPicker.ItemsSource = cat.Clasificaciones;
            UnidadPicker.ItemsSource = cat.UnidadesMedida;

            if (_item is null) return;

            HeaderLabel.Text = "Editar residuo";
            GuardarButton.Text = "Guardar cambios";
            CodigoEntry.Text = _item.Codigo;
            NombreEntry.Text = _item.Nombre;
            DescripcionEditor.Text = _item.Descripcion;
            TipoPicker.SelectedItem = ((IEnumerable<TipoResiduoDto>)TipoPicker.ItemsSource).FirstOrDefault(x => x.TipoResiduoId == _item.TipoResiduoId);
            ClasificacionPicker.SelectedItem = cat.Clasificaciones.FirstOrDefault(x => x.ClasificacionResiduoId == _item.ClasificacionResiduoId);
            UnidadPicker.SelectedItem = cat.UnidadesMedida.FirstOrDefault(x => x.UnidadMedidaId == _item.UnidadMedidaDefaultId);

            if (_item.TieneUsoHistorico)
            {
                TipoPicker.IsEnabled = false;
                ClasificacionPicker.IsEnabled = false;
                BloqueoLabel.IsVisible = true;
            }
        }
        catch { MostrarError("No se pudo cargar la información del residuo."); }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (TipoPicker.SelectedItem is not TipoResiduoDto tipo) { MostrarError("Selecciona un tipo."); return; }
        if (ClasificacionPicker.SelectedItem is not ClasificacionResiduoDto clasificacion) { MostrarError("Selecciona una clasificación."); return; }
        if (UnidadPicker.SelectedItem is not UnidadMedidaDto unidad) { MostrarError("Selecciona una unidad."); return; }

        var codigo = CodigoEntry.Text?.Trim();
        var nombre = NombreEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre)) { MostrarError("Código y nombre son obligatorios."); return; }

        var req = new GuardarAdministracionResiduoCatalogoRequest(
            tipo.TipoResiduoId, clasificacion.ClasificacionResiduoId, codigo, nombre,
            DescripcionEditor.Text, unidad.UnidadMedidaId);

        try
        {
            GuardarButton.IsEnabled = false;
            var api = AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>();
            if (_id == Guid.Empty) await api.CrearResiduoAsync(req); else await api.ActualizarResiduoAsync(_id, req);
            await DisplayAlert("Residuo guardado", "El catálogo fue actualizado correctamente.", "Aceptar");
            await AppNavigator.VolverAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) { MostrarError(ExtraerDetalle(ex.Message)); }
        catch { MostrarError("No se pudo guardar el residuo."); }
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
