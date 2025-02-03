using Microsoft.Extensions.DependencyInjection;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Provider;
using YASRP.Security.Certificates;
using YASRP.Security.Certificates.Providers;
using YASRP.Security.Certificates.Stores;

namespace YASRP;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddCertManager(this IServiceCollection services) {
        services.AddSingleton<ICertificateProvider, DefaultCertificateProvider>();
        services.AddSingleton<ICertificateStore, UnixCertificateStore>();
        services.AddSingleton<ICertManager, CertManager>();
        return services;
    }

    public static IServiceCollection AddConfiguration(this IServiceCollection services) {
        services.AddSingleton<IConfigurationProvider, AppConfigurationProvider>();
        return services;
    }
}
