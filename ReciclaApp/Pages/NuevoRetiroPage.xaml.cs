using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class NuevoRetiroPage : ContentPage
{
    private bool _isLoading;
    private InventarioResiduosDto? _inventario;
    private CatalogosInicialDto? _catalogos;
    private RetiroSelectableItem[] _items = Array.Empty<RetiroSelectableItem>();

    public NuevoRetiroPage()
    {
        InitializeComponent();
        FechaPicker.Date = DateTime.Today;
        HoraPicker.Time = DateTime.Now.TimeOfDay;
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

        var usuario = session.CurrentUser;
        var puedeCrear = usuario?.Roles.Contains("AMBIENTAL") == true;
        if (!puedeCrear)
        {
            MostrarError("Tu rol no tiene permiso para registrar retiros.");
            GuardarButton.IsEnabled = false;
            return;
        }

        if (_inventario is null)
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
            var inventarioApi = AppServices.Services.GetRequiredService<IInventarioApiClient>();
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();

            var inventarioTask = inventarioApi.ObtenerAsync();
            var catalogosTask = api.ObtenerCatalogosAsync();
            await Task.WhenAll(inventarioTask, catalogosTask);

            _inventario = await inventarioTask;
            _catalogos = await catalogosTask;

            CargarGestores();
            CargarSedesConStock();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevoRetiroPage>>()?
                .LogError(ex, "Error preparando nuevo retiro.");
            MostrarError("No se pudo cargar el stock disponible para retiro.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void CargarGestores()
    {
        if (_catalogos is null)
            return;

        var gestores = _catalogos.Empresas
            .Where(x => x.EsGestoraResiduos)
            .OrderBy(x => NombreEmpresa(x))
            .Select(x => new GestorOpcion(x.EmpresaId, NombreEmpresa(x)))
            .ToArray();

        GestorPicker.ItemsSource = gestores;
        if (gestores.Length == 1)
            GestorPicker.SelectedIndex = 0;
    }

    private void CargarSedesConStock()
    {
        if (_inventario is null)
            return;

        var sedes = _inventario.Items
            .Where(x => x.Almacenamientos.Any(a => a.Cantidad > 0))
            .GroupBy(x => new { x.SedeId, x.Sede })
            .Select(g => new SedeOpcion(g.Key.SedeId, g.Key.Sede))
            .OrderBy(x => x.Nombre)
            .ToArray();

        SedePicker.ItemsSource = sedes;
        if (sedes.Length == 1)
            SedePicker.SelectedIndex = 0;
        else if (sedes.Length == 0)
            SinStockLabel.Text = "No hay residuos almacenados disponibles para retiro.";
    }

    private void CargarPuntos()
    {
        if (_inventario is null || SedePicker.SelectedItem is not SedeOpcion sede)
        {
            PuntoPicker.ItemsSource = Array.Empty<PuntoOpcion>();
            LimpiarItems();
            return;
        }

        var puntos = _inventario.Items
            .Where(x => x.SedeId == sede.SedeId)
            .SelectMany(x => x.Almacenamientos.Where(a => a.Cantidad > 0))
            .GroupBy(x => new { x.PuntoResiduoId, x.Punto })
            .Select(g => new PuntoOpcion(g.Key.PuntoResiduoId, g.Key.Punto))
            .OrderBy(x => x.Nombre)
            .ToArray();

        PuntoPicker.ItemsSource = puntos;
        PuntoPicker.SelectedItem = null;
        if (puntos.Length == 1)
            PuntoPicker.SelectedIndex = 0;
        else
            LimpiarItems();
    }

    private void CargarItems()
    {
        if (_inventario is null ||
            SedePicker.SelectedItem is not SedeOpcion sede ||
            PuntoPicker.SelectedItem is not PuntoOpcion punto)
        {
            LimpiarItems();
            return;
        }

        _items = _inventario.Items
            .Where(x => x.SedeId == sede.SedeId)
            .Select(x => new
            {
                Item = x,
                Ubicacion = x.Almacenamientos.FirstOrDefault(a => a.PuntoResiduoId == punto.PuntoResiduoId && a.Cantidad > 0)
            })
            .Where(x => x.Ubicacion is not null)
            .Select(x => new RetiroSelectableItem
            {
                RegistroResiduoId = x.Item.RegistroResiduoId,
                Residuo = x.Item.Residuo,
                OrigenTexto = $"{x.Item.ControlCodigo} · {x.Item.RegistradoPor}",
                Unidad = x.Item.Unidad,
                DisponiblePunto = x.Ubicacion!.Cantidad,
                DisponibleTexto = $"Saldo estimado en almacén: {x.Ubicacion.Cantidad:0.###} {x.Item.Unidad}",
                TipoRetiro = "PARCIAL",
                PesoEorsTexto = string.Empty
            })
            .OrderBy(x => x.Residuo)
            .ToArray();

        BindingContext = new ItemsBinding(_items);
        SinStockLabel.IsVisible = _items.Length == 0;
        SinStockLabel.Text = _items.Length == 0
            ? "Este punto no tiene stock disponible para retiro."
            : string.Empty;
        ActualizarCantidadSeleccionada();
    }

    private void LimpiarItems()
    {
        _items = Array.Empty<RetiroSelectableItem>();
        BindingContext = new ItemsBinding(_items);
        SinStockLabel.IsVisible = true;
        SinStockLabel.Text = "Selecciona un almacén para ver el stock disponible.";
        ActualizarCantidadSeleccionada();
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        ErrorBorder.IsVisible = false;

        if (SedePicker.SelectedItem is not SedeOpcion sede || PuntoPicker.SelectedItem is not PuntoOpcion punto)
        {
            MostrarError("Selecciona la sede y el punto de almacenamiento.");
            return;
        }

        if (GestorPicker.SelectedItem is not GestorOpcion gestor)
        {
            MostrarError("Selecciona la empresa gestora que realizará el retiro.");
            return;
        }

        var seleccionados = _items.Where(x => x.Seleccionado).ToArray();
        if (seleccionados.Length == 0)
        {
            MostrarError("Selecciona al menos un residuo almacenado.");
            return;
        }

        var detalles = new List<CrearRetiroDetalleRequest>();
        foreach (var item in seleccionados)
        {
            if (!TryParseDecimal(item.PesoEorsTexto, out var pesoEors) || pesoEors <= 0)
            {
                MostrarError($"Ingresa el peso reportado por la EO-RS para {item.Residuo}.");
                return;
            }

            var tipo = item.TipoRetiro?.Trim().ToUpperInvariant();
            if (tipo is not ("PARCIAL" or "TOTAL"))
            {
                MostrarError($"Indica si el retiro de {item.Residuo} es PARCIAL o TOTAL.");
                return;
            }

            if (tipo == "PARCIAL" && pesoEors >= item.DisponiblePunto)
            {
                MostrarError($"El peso de {item.Residuo} consume o supera el saldo estimado. Si la EO-RS se llevó todo lo existente, marca el retiro como TOTAL.");
                return;
            }

            detalles.Add(new CrearRetiroDetalleRequest(item.RegistroResiduoId, pesoEors, tipo));
        }

        var fecha = FechaPicker.Date.Date + HoraPicker.Time;
        var totales = seleccionados
            .Select((x, i) => new { x.Unidad, PesoEors = detalles[i].PesoEors })
            .GroupBy(x => x.Unidad)
            .Select(g => $"{g.Sum(x => x.PesoEors):0.###} {g.Key} según balanza EO-RS");

        var confirmar = await DisplayAlert(
            "Confirmar retiro",
            $"Se registrará la salida realizada de {seleccionados.Length} residuos desde {punto.Nombre} hacia {gestor.Nombre}.\n\n{string.Join(" · ", totales)}",
            "Registrar retiro",
            "Cancelar");
        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            GuardarButton.IsEnabled = false;
            GuardarButton.Text = "Registrando...";
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var retiroApi = AppServices.Services.GetRequiredService<IRetiroApiClient>();
            var creado = await retiroApi.CrearAsync(new CrearRetiroRequest(
                fecha,
                sede.SedeId,
                punto.PuntoResiduoId,
                gestor.EmpresaId,
                Limpiar(DocumentoEntry.Text),
                Limpiar(VehiculoEntry.Text),
                Limpiar(PlacaEntry.Text),
                Limpiar(ConductorEntry.Text),
                Limpiar(ObservacionEditor.Text),
                detalles));

            await AppNavigator.IrADetalleRetiroDesdeCreacionAsync(creado.RetiroId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            MostrarError("El stock cambió mientras preparabas el retiro. Vuelve a cargar la pantalla y revisa las cantidades.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            MostrarError("El retiro contiene residuos de un control que no tienes permitido gestionar.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<NuevoRetiroPage>>()?
                .LogError(ex, "Error creando retiro.");
            MostrarError("No se pudo registrar el retiro. Revisa los datos e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            GuardarButton.IsEnabled = true;
            GuardarButton.Text = "Registrar retiro";
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void OnSedeChanged(object sender, EventArgs e) => CargarPuntos();

    private void OnPuntoChanged(object sender, EventArgs e) => CargarItems();

    private void OnSeleccionChanged(object sender, CheckedChangedEventArgs e) => ActualizarCantidadSeleccionada();

    private void ActualizarCantidadSeleccionada()
    {
        var cantidad = _items.Count(x => x.Seleccionado);
        CantidadSeleccionLabel.Text = cantidad == 1 ? "1 seleccionado" : $"{cantidad} seleccionados";
    }

    private static bool TryParseDecimal(string? value, out decimal cantidad)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            cantidad = 0;
            return false;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out cantidad) ||
               decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out cantidad);
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

    private sealed record SedeOpcion(Guid SedeId, string Nombre);
    private sealed record PuntoOpcion(Guid PuntoResiduoId, string Nombre);
    private sealed record GestorOpcion(Guid EmpresaId, string Nombre);
    private sealed record ItemsBinding(IReadOnlyCollection<RetiroSelectableItem> Items);

    private sealed class RetiroSelectableItem
    {
        public Guid RegistroResiduoId { get; init; }
        public string Residuo { get; init; } = string.Empty;
        public string OrigenTexto { get; init; } = string.Empty;
        public string Unidad { get; init; } = string.Empty;
        public decimal DisponiblePunto { get; init; }
        public string DisponibleTexto { get; init; } = string.Empty;
        public IReadOnlyList<string> TiposRetiro { get; } = new[] { "PARCIAL", "TOTAL" };
        public bool Seleccionado { get; set; }
        public string TipoRetiro { get; set; } = "PARCIAL";
        public string PesoEorsTexto { get; set; } = string.Empty;
    }
}
