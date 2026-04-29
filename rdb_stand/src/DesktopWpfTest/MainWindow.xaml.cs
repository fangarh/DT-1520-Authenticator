using System.Windows;
using Dt1520.Authenticator.DesktopWpfTest.Services;
using Dt1520.Authenticator.DesktopWpfTest.Storage;
using Dt1520.Authenticator.DesktopWpfTest.ViewModels;

namespace Dt1520.Authenticator.DesktopWpfTest;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel(
            new FallbackSettingsStore(
                new EncryptedJsonSettingsStore(),
                ReferenceBackendDevelopmentSettingsStore.FromCurrentAppDirectory()),
            new ReferenceBackendClientFactory());
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadSettingsAsync();
    }
}
