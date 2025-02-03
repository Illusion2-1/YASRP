using Microsoft.Extensions.DependencyInjection;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Initialize();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));
        
        logger.Info("Started.");
        
        var services = new ServiceCollection();

        services.AddConfiguration()
            .AddCertManager();
        
        
        var serviceProvider = services.BuildServiceProvider();
        
        var configurationProvider = serviceProvider.GetRequiredService<IConfigurationProvider>();
        var config = configurationProvider.LoadConfiguration();
        
        logger.Info("Info");
        logger.Warn("Warn");
        logger.Error("Error");
        logger.Debug("Debug");
    }
}