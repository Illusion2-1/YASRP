using YASRP.Core.Configurations;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Security.Certificates;
using YASRP.Security.Certificates.Providers;
using YASRP.Security.Certificates.Stores;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Configure();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));
        logger.Info("Started.");
        var certificateProvider = new DefaultCertificateProvider();
        var certificateStore = new UnixCertificateStore();
        var certManager = new CertManager(certificateProvider, certificateStore);

// 初始化根证书
        await certManager.InitializeAsync("example.com");
        logger.Info("Initialized root CA.");

// 获取特定域名的证书
        var siteCert = certManager.GetOrCreateSiteCertificate("example.com,example.org");
        logger.Error(siteCert.ToString());
    }
}