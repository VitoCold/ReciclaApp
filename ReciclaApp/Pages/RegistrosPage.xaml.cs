using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class RegistrosPage : ContentPage
{
    private readonly ObservableCollection<RegistroListItemDto> _registros = new();
    private bool _isLoading;

    public RegistrosPage()
    {
        InitializeComponent();
        RegistrosCollectionView.ItemsSource = _registros;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        FechaHeaderLabel.Text = DateTime.Now.ToString("dd/MM/yyyy");

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        var usuario = session.CurrentUser;
        UsuarioHeaderLabel.Text = usuario is null
            ? "Usuario"
            : string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        await CargarRegistrosAsync(showInitialLoader: _registros.Count == 0);
    }

    private async Task CargarRegistrosAsync(bool showInitialLoader)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;

        if (showInitialLoader)
        {
            RegistrosActivityIndicator.IsVisible = true;
            RegistrosActivityIndicator.IsRunning = true;
        }

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var registros = await apiClient.ListarRegistrosAsync();

            _registros.Clear();
            foreach (var registro in registros)
                _registros.Add(registro);

            CantidadRegistrosLabel.Text = _registros.Count == 1
                ? "1 registro"
                : $"{_registros.Count} registros";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrosPage>>()?
                .LogError(ex, "Error cargando los registros del usuario.");

            ErrorLabel.Text = "No se pudieron cargar los registros. Verifica tu conexión e inténtalo nuevamente.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
            RegistrosRefreshView.IsRefreshing = false;
            RegistrosActivityIndicator.IsRunning = false;
            RegistrosActivityIndicator.IsVisible = false;
        }
    }

    private void OnMenuTapped(object sender, TappedEventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnPerfilTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.IrAPerfilAsync();
    }

    private async void OnNuevoRegistroClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrAInicioRegistroAsync();
    }

    private async void OnRegistroTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid registroId)
        {
            await AppNavigator.IrADetalleRegistroAsync(registroId);
            return;
        }

        if (Guid.TryParse(e.Parameter?.ToString(), out registroId))
            await AppNavigator.IrADetalleRegistroAsync(registroId);
    }

    private async void OnRefreshRequested(object sender, EventArgs e)
    {
        await CargarRegistrosAsync(showInitialLoader: false);
    }

    private async void OnReintentarTapped(object sender, TappedEventArgs e)
    {
        await CargarRegistrosAsync(showInitialLoader: _registros.Count == 0);
    }
}
