using System.Windows;
using Katib3omomy.Core.Services;
using Katib3omomy.Infrastructure.Data;
using Katib3omomy.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Katib3omomy;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IDocxPlaceholderService, DocxPlaceholderService>();
        services.AddSingleton<ITemplateRepository, TemplateRepository>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
