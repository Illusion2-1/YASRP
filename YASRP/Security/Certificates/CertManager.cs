using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using YASRP.Core.Abstractions;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Security.Certificates.Providers;
using YASRP.Security.Certificates.Stores;

namespace YASRP.Security.Certificates;

public class CertManager(ICertificateProvider certificateProvider, ICertificateStore certificateStore) : ICertManager {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(CertManager));
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificateCache = new();
    private X509Certificate2? _rootCertificate;
    private const string RootCertName = "Yasrp Root";

    public Task InitializeAsync(string rootCertCommonName) {
        _logger.Info("Initializing certificate manager...");
        try {
            _rootCertificate = certificateStore.GetRootCertificate(RootCertName);

            if (_rootCertificate == null) {
                _logger.Info("No existing root certificate found.");
            } else if (_rootCertificate.NotAfter <= DateTime.UtcNow.AddMonths(1)) {
                _logger.Warn($"Root certificate expires soon ({_rootCertificate.NotAfter:yyyy-MM-dd}), renewing...");
            }

            if (_rootCertificate == null || _rootCertificate.NotAfter <= DateTime.UtcNow.AddMonths(1)) {
                var notBefore = DateTime.UtcNow.AddDays(-1);
                var notAfter = notBefore.AddYears(10);
                _logger.Info($"Generating new root certificate with validity {notBefore:yyyy-MM-dd} to {notAfter:yyyy-MM-dd}");
                
                _rootCertificate = certificateProvider.GenerateRootCertificate(
                    rootCertCommonName,
                    notBefore,
                    notAfter
                );
                
                certificateStore.StoreRootCertificate(_rootCertificate, RootCertName);
                /*_certificateStore.InstallRootCertificate(_rootCertificate);*/

                _logger.Info($"Root certificate '{RootCertName}' generated and stored successfully");
            } else {
                _logger.Info($"Using existing valid root certificate (expires {_rootCertificate.NotAfter:yyyy-MM-dd})");
            }
        }
        catch (Exception ex) {
            _logger.Error(ex);
            Environment.Exit(1);
        }

        _logger.Info("Certificate manager initialized successfully");
        return Task.CompletedTask;
    }

    public X509Certificate2 GetOrCreateSiteCertificate(List<string> domains) {
        _logger.Debug($"Requesting site certificate for domains: {string.Join(", ", domains)}");
        
        var primaryDomain = domains.FirstOrDefault() ?? string.Empty;
        if (_certificateCache.TryGetValue(primaryDomain, out var cachedCertificate)) {
            _logger.Debug($"Cache hit for domain: {primaryDomain}");
            return cachedCertificate;
        }

        var certificateFilePath = Path.Combine(Environment.CurrentDirectory, "SiteCert.pfx");
        if (File.Exists(certificateFilePath)) {
            _logger.Info("Found existing site certificate file, attempting to load...");
            try {
                var certificate = new X509Certificate2(certificateFilePath, string.Empty,
                    X509KeyStorageFlags.Exportable |
                    X509KeyStorageFlags.PersistKeySet |
                    X509KeyStorageFlags.MachineKeySet);

                if (ValidateDomainCertificate(certificate, domains)) {
                    _logger.Info("Loaded valid site certificate from disk");
                    foreach (var domain in domains) {
                        _certificateCache.TryAdd(domain, certificate);
                        _logger.Debug($"Cached certificate for domain: {domain}");
                    }
                    return certificate;
                }
                _logger.Warn("Existing site certificate failed domain validation");
            }
            catch (Exception ex) {
                _logger.Error(ex);
            }
        }

        _logger.Info("No valid certificate available, generating new site certificate...");
        return _certificateCache.GetOrAdd(primaryDomain, domainName => {
            if (_rootCertificate == null) {
                _logger.Error("Root certificate not initialized when creating site certificate");
                throw new InvalidOperationException("Root certificate not initialized");
            }

            _logger.Info($"Generating new site certificate for domains: {string.Join(", ", domains)}");
            var siteCert = certificateProvider.GenerateSiteCertificate(_rootCertificate);
            
            _logger.Debug($"New site certificate generated (Thumbprint: {siteCert.Thumbprint})");
            foreach (var domain in domains.Skip(1)) {
                _certificateCache.TryAdd(domain, siteCert);
                _logger.Debug($"Cached certificate for additional domain: {domain}");
            }
            return siteCert;
        });
    }

    private bool ValidateDomainCertificate(X509Certificate2 certificate, IEnumerable<string> allowedDomains) {
        var enumerable = allowedDomains.ToList();
        _logger.Debug($"Validating certificate for domains: {string.Join(", ", enumerable)}");
        var result = certificateProvider.ValidateDomainInCertificate(certificate, enumerable);
        _logger.Debug($"Certificate validation result: {(result ? "Valid" : "Invalid")}");
        return result;
    }
}