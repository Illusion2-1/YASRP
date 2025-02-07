using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.DoH;
using YASRP.Network.Proxy;
using YASRP.Security.Certificates;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Initialize();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));

        logger.Info("Started.");

        var services = new ServiceCollection();

        var (_, serviceProvider) = ConfigureServices(services);
        
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // 注册服务
                var (config, _) = ConfigureServices(services);
                services.AddSingleton(config); // 添加配置到容器
            })
            .Build();
        
        var certManager = serviceProvider.GetRequiredService<ICertManager>();
        await certManager.InitializeAsync("example.org");

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