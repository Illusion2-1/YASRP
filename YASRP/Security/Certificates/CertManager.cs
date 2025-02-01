using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using YASRP.Core.Abstractions;
using YASRP.Core.Utilities;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Security.Certificates;

public class CertManager(ICertificateProvider certificateProvider, ICertificateStore certificateStore) {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(CertManager));
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificateCache = new();
    private X509Certificate2? _rootCertificate;
    private const string RootCertName = "Yasrp Root";

    public async Task InitializeAsync(string rootCertCommonName) {
        try {
            // 尝试从存储中获取根证书
            _rootCertificate = certificateStore.GetRootCertificate(RootCertName);

            if (_rootCertificate == null || _rootCertificate.NotAfter <= DateTime.UtcNow.AddMonths(1)) {
                // 生成新的根证书（有效期10年）
                var notBefore = DateTime.UtcNow.AddDays(-1);
                var notAfter = notBefore.AddYears(10);
                _rootCertificate = certificateProvider.GenerateRootCertificate(
                    rootCertCommonName,
                    notBefore,
                    notAfter
                );

                // 存储并安装根证书
                certificateStore.StoreRootCertificate(_rootCertificate, RootCertName);
                /*_certificateStore.InstallRootCertificate(_rootCertificate);*/

                _logger.Info($"Generated and installed new root certificate: {RootCertName}");
            }
        }
        catch (Exception ex) {
            _logger.Error($"Failed to initialize certificate manager: {ex.Message}");
            throw;
        }
    }

    public X509Certificate2 GetOrCreateSiteCertificate(string domains) {
        if (_certificateCache.TryGetValue(domains, out var cachedCertificate)) {
            _logger.Info("Site certificate found in cache.");
            return cachedCertificate;
        }

        var certificateFilePath = Path.Combine(Environment.CurrentDirectory, "SiteCert.pfx");
        if (File.Exists(certificateFilePath)) {
            _logger.Info("Site certificate found on disk. Loading from file.");
            var certificate = new X509Certificate2(certificateFilePath);

            if (ValidateDomainCertificate(certificate, domains.SplitByCommaToHashSet())) {
                _certificateCache.TryAdd(domains, certificate);
                return certificate;
            }
        }

        _logger.Info("No valid cert was found. Generating new.");
        return _certificateCache.GetOrAdd(domains, (domainName) => {
            if (_rootCertificate == null)
                throw new InvalidOperationException("Root certificate not initialized");

            var siteCert = certificateProvider.GenerateSiteCertificate(domainName, _rootCertificate);
            _logger.Debug($"Generated site certificate for domain: {domainName}");
            return siteCert;
        });
    }


    private bool ValidateDomainCertificate(X509Certificate2 certificate, IEnumerable<string> allowedDomains) {
        return certificateProvider.ValidateDomainInCertificate(certificate, allowedDomains);
    }
}