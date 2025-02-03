using Microsoft.Extensions.DependencyInjection;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Configure();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));
        
        logger.Info("Started.");
        
        var services = new ServiceCollection();

        services.AddConfiguration()
            .AddCertManager();
        
        
        var serviceProvider = services.BuildServiceProvider();
    }
}