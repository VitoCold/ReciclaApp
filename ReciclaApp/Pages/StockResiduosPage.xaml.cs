using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class StockResiduosPage : ContentPage
{
    private bool _isLoading;
    private InventarioResiduosDto? _inventario;
    private CatalogosInicialDto? _catalogos;
    private UsuarioDto? _usuario;

    public StockResiduosPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        _usuario = session.CurrentUser;
        if (_usuario is not null)
        {
            UsuarioHeaderLabel.Text = string.Join(" ", new[] { _usuario.Nombres, _usuario.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        await CargarAsync(_inventario is null);
    }

    private async Task CargarAsync(bool showLoader)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        if (showLoader)
        {
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
        }

        try
        {
            var inventarioApi = AppServices.Services.GetRequiredService<IInventarioApiClient>();
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();

            var inventarioTask = inventarioApi.ObtenerAsync();
            var catalogosTask = api.ObtenerCatalogosAsync();
            await Task.WhenAll(inventarioTask, catalogosTask);

            _inventario = await inventarioTask;
            _catalogos = await catalogosTask;

            CargarSedes();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<StockResiduosPage>>()?
                .LogError(ex, "Error cargando el inventario de residuos.");
            MostrarError("No se pudo cargar el stock de residuos. Verifica la API e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            StockRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void CargarSedes()
    {
        if (_catalogos is null)
            return;

        var selectedId = (SedePicker.SelectedItem as SedeFiltroItem)?.SedeId;
        var opciones = new List<SedeFiltroItem> { new(null, "Todas las sedes") };
        opciones.AddRange(_catalogos.Sedes
            .OrderBy(x => x.Nombre)
            .Select(x => new SedeFiltroItem(x.SedeId, x.Nombre)));

        SedePicker.ItemsSource = opciones;
        SedePicker.SelectedItem = opciones.FirstOrDefault(x => x.SedeId == selectedId) ?? opciones[0];
    }

    private void AplicarFiltro()
    {
        if (_inventario is null)
        {
            StockCollectionView.ItemsSource = Array.Empty<StockVisualItem>();
            ResumenLabel.Text = "Sin stock disponible";
            return;
        }

        var sedeId = (SedePicker.SelectedItem as SedeFiltroItem)?.SedeId;
        var items = _inventario.Items
            .Where(x => !sedeId.HasValue || x.SedeId == sedeId.Value)
            .Select(MapVisual)
            .ToArray();

        StockCollectionView.ItemsSource = items;
        PintarResumen(items);
    }

    private StockVisualItem MapVisual(InventarioResiduoItemDto item)
    {
        var puedeGestionar = _usuario?.Roles.Contains("AMBIENTAL") == true ||
                             _usuario?.Roles.Contains("RESPONSABLE_OPERATIVO") == true;

        var almacenamientos = item.Almacenamientos.Count == 0
            ? "Aún no trasladado"
            : string.Join(" · ", item.Almacenamientos.Select(x => $"{x.Punto}: {x.Cantidad:0.###} {item.Unidad}"));

        return new StockVisualItem(
            item.RegistroResiduoId,
            item.Residuo,
            item.Clasificacion,
            $"{item.CantidadDisponible:0.###} {item.Unidad}",
            $"{item.Sede} · {item.ControlCodigo}",
            $"{item.FechaRegistro:dd/MM/yyyy HH:mm} · {item.RegistradoPor}",
            item.CantidadPendienteAlmacenamiento > 0
                ? $"{item.CantidadPendienteAlmacenamiento:0.###} {item.Unidad}"
                : "0 · todo ubicado",
            almacenamientos,
            puedeGestionar && item.CantidadPendienteAlmacenamiento > 0,
            item);
    }

    private void PintarResumen(StockVisualItem[] items)
    {
        if (items.Length == 0)
        {
            ResumenLabel.Text = "Sin stock disponible";
            return;
        }

        var resumen = items
            .GroupBy(x => x.Item.Unidad)
            .Select(g =>
            {
                var disponible = g.Sum(x => x.Item.CantidadDisponible);
                var almacenado = g.Sum(x => x.Item.Almacenamientos.Sum(a => a.Cantidad));
                var pendiente = g.Sum(x => x.Item.CantidadPendienteAlmacenamiento);
                return $"{disponible:0.###} {g.Key} disponible · {almacenado:0.###} almacenado · {pendiente:0.###} pendiente";
            });

        ResumenLabel.Text = string.Join("\n", resumen);
    }

    private async void OnAlmacenarClicked(object sender, EventArgs e)
    {
        if (_isLoading || _inventario is null || _catalogos is null ||
            sender is not Button button || !TryGetGuid(button.CommandParameter, out var registroResiduoId))
            return;

        var item = _inventario.Items.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);
        if (item is null || item.CantidadPendienteAlmacenamiento <= 0)
            return;

        var puntos = _catalogos.PuntosResiduo
            .Where(x =>
                x.SedeId == item.SedeId &&
                (string.Equals(x.Tipo, "ALMACENAMIENTO", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.Tipo, "AMBOS", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Nombre)
            .ToArray();

        if (puntos.Length == 0)
        {
            await DisplayAlert("Sin almacén configurado", "No existe un punto de almacenamiento activo para esta sede.", "Aceptar");
            return;
        }

        var opciones = puntos.Select(x => $"{x.Codigo} · {x.Nombre}").ToArray();
        var seleccion = await DisplayActionSheet("Punto de almacenamiento", "Cancelar", null, opciones);
        if (string.IsNullOrWhiteSpace(seleccion) || seleccion == "Cancelar")
            return;

        var indice = Array.IndexOf(opciones, seleccion);
        if (indice < 0)
            return;

        var cantidadTexto = await DisplayPromptAsync(
            "Cantidad a trasladar",
            $"Saldo pendiente: {item.CantidadPendienteAlmacenamiento:0.###} {item.Unidad}",
            "Continuar",
            "Cancelar",
            "Cantidad",
            keyboard: Keyboard.Numeric,
            initialValue: item.CantidadPendienteAlmacenamiento.ToString("0.###", CultureInfo.CurrentCulture));

        if (string.IsNullOrWhiteSpace(cantidadTexto))
            return;

        if (!TryParseDecimal(cantidadTexto, out var cantidad) || cantidad <= 0 || cantidad > item.CantidadPendienteAlmacenamiento)
        {
            await DisplayAlert("Cantidad inválida", $"Ingresa una cantidad entre 0 y {item.CantidadPendienteAlmacenamiento:0.###} {item.Unidad}.", "Aceptar");
            return;
        }

        var punto = puntos[indice];
        var confirmar = await DisplayAlert(
            "Confirmar almacenamiento",
            $"Registrar {cantidad:0.###} {item.Unidad} de {item.Residuo} en {punto.Nombre}?",
            "Registrar",
            "Cancelar");
        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var inventarioApi = AppServices.Services.GetRequiredService<IInventarioApiClient>();
            await inventarioApi.AlmacenarAsync(registroResiduoId, new AlmacenarResiduoRequest(
                punto.PuntoResiduoId,
                cantidad,
                DateTime.Now,
                null));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("Saldo actualizado", "El saldo cambió mientras registrabas el traslado. Actualiza el inventario e inténtalo nuevamente.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<StockResiduosPage>>()?
                .LogError(ex, "Error almacenando residuo {RegistroResiduoId}.", registroResiduoId);
            await DisplayAlert("No se pudo registrar", "No se pudo registrar el traslado al almacenamiento.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }

        await CargarAsync(false);
    }

    private static bool TryParseDecimal(string value, out decimal cantidad) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out cantidad) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out cantidad);

    private static bool TryGetGuid(object? value, out Guid guid) =>
        value is Guid direct
            ? (guid = direct) != Guid.Empty
            : Guid.TryParse(value?.ToString(), out guid);

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private void OnSedeChanged(object sender, EventArgs e) => AplicarFiltro();

    private void OnMenuTapped(object sender, TappedEventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private async void OnPerfilTapped(object sender, TappedEventArgs e) => await AppNavigator.IrAPerfilAsync();

    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);

    private async void OnReintentarTapped(object sender, TappedEventArgs e) => await CargarAsync(_inventario is null);

    private sealed record SedeFiltroItem(Guid? SedeId, string Nombre);

    private sealed record StockVisualItem(
        Guid RegistroResiduoId,
        string Residuo,
        string Clasificacion,
        string DisponibleTexto,
        string OrigenTexto,
        string RegistroTexto,
        string PendienteTexto,
        string AlmacenamientosTexto,
        bool PuedeAlmacenar,
        InventarioResiduoItemDto Item);
}
