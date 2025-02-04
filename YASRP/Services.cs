using Microsoft.Extensions.DependencyInjection;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Network.Dns.Caching;
using YASRP.Network.Dns.DoH;
using YASRP.Security.Certificates;
using YASRP.Security.Certificates.Providers;
using YASRP.Security.Certificates.Stores;

namespace YASRP;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddCertManager(this IServiceCollection services) {
        // 使用工厂方法延迟初始化
        services.AddSingleton<ICertificateProvider, DefaultCertificateProvider>();
        services.AddSingleton<ICertificateStore, UnixCertificateStore>();
        services.AddSingleton<ICertManager, CertManager>();
        return services;
    }

    public static IServiceCollection AddConfiguration(this IServiceCollection services) {
        services.AddSingleton<IConfigurationProvider, AppConfigurationProvider>();
        return services;
    }

    public static IServiceCollection AddDoHResolver(this IServiceCollection services) {
        services.AddSingleton<IDoHResolver, DoHResolver>();
        services.AddSingleton<IDnsCacheService, DnsCacheService>();
        return services;
    }
}