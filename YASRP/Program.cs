using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Initialize();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));

        logger.Info("Started.");

        var services = new ServiceCollection();

        var (config, _) = ConfigureServices(services);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) => {
                services.AddSingleton(config);

                services.AddCertManager()
                    .AddDoHResolver()
                    .AddReverseProxyCore();
            })
            .Build();

        var certManager = host.Services.GetRequiredService<ICertManager>();
        await certManager.InitializeAsync("YASRP");

        await host.RunAsync();
    }

    private static (AppConfiguration, ServiceProvider) ConfigureServices(IServiceCollection services) {
        services.AddConfiguration()
            .AddCertManager()
            .AddDoHResolver()
            .AddReverseProxyCore();

        var tempProvider = services.BuildServiceProvider();
        var configProvider = tempProvider.GetRequiredService<IConfigurationProvider>();
        var config = configProvider.LoadConfiguration();

        services.AddSingleton(config);

        return (config, services.BuildServiceProvider());
    }
}