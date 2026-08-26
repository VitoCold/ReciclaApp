using ReciclaApp.Navigation;
namespace ReciclaApp.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await AppNavigator.IrARegistrosAsync();
    }
}
