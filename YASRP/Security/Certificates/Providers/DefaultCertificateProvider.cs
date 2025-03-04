using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Security.Certificates.Providers;

public class DefaultCertificateProvider (AppConfiguration config) : ICertificateProvider {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(DefaultCertificateProvider));

    public X509Certificate2 GenerateRootCertificate(string commonName, DateTime notBefore, DateTime notAfter) {
        using var rsa = RSA.Create(4096);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));

        var certificate = request.CreateSelfSigned(notBefore, notAfter);
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx), string.Empty,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    public X509Certificate2 GenerateSiteCertificate(X509Certificate2 rootCertificate) {
        using var rsa = RSA.Create(2048);
        var domainArray = config.TargetDomains.ToArray();
        var request = new CertificateRequest(
            $"CN={domainArray[0]}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        foreach (var domain in domainArray) sanBuilder.AddDnsName(domain);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTime.UtcNow.AddDays(-1);
        var notAfter = notBefore.AddYears(1);

        using var rootCertificatePrivateKey = rootCertificate.GetRSAPrivateKey();
        if (rootCertificatePrivateKey == null)
            throw new InvalidOperationException("Root certificate doesn't have a private key");

        var certificate = request.Create(rootCertificate, notBefore, notAfter, Guid.NewGuid().ToByteArray());
        var pfxCertificate = certificate.CopyWithPrivateKey(rsa);

        var pfxBytes = pfxCertificate.Export(X509ContentType.Pfx, string.Empty);
        var fileName = Path.Combine(Environment.CurrentDirectory, "SiteCert.pfx");
        File.WriteAllBytes(fileName, pfxBytes);

        return new X509Certificate2(pfxBytes, string.Empty,
            X509KeyStorageFlags.Exportable |
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.MachineKeySet);
    }

    public bool ValidateDomainInCertificate(X509Certificate2 certificate, IEnumerable<string> allowedDomains) {
        var allowedDomainsSet = new HashSet<string>(allowedDomains, StringComparer.OrdinalIgnoreCase);
        _logger.Info("Listing allowed domains:");
        foreach (var s in allowedDomainsSet) _logger.Info(s);

        var subjectName = certificate.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.IsNullOrEmpty(subjectName) && !allowedDomainsSet.Contains(subjectName)) {
            _logger.Warn($"Certificate CN {subjectName} not in allowed domains list");
            return false;
        }

        var sanExtension = certificate.Extensions
            .FirstOrDefault(ext => ext.Oid?.Value == "2.5.29.17");

        if (sanExtension != null) {
            var asnData = new AsnEncodedData(sanExtension.Oid!, sanExtension.RawData);
            var sanString = asnData.Format(false);

            // 使用正则表达式匹配所有DNS条目
            var matches = Regex.Matches(sanString, @"DNS\s*(?:Name)?\s*:\s*([^,\s]+)", RegexOptions.IgnoreCase);
            foreach (Match match in matches) {
                if (match.Groups.Count < 2) continue;

                var domain = match.Groups[1].Value.Trim();
                if (!allowedDomainsSet.Contains(domain)) {
                    _logger.Warn($"SAN domain {domain} not in allowed domains list");
                    return false;
                }
            }
        }

        return true;
    }
}