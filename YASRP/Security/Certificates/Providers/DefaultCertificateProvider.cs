using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Security.Certificates.Providers;

public class DefaultCertificateProvider(AppConfiguration config) : ICertificateProvider {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(DefaultCertificateProvider));

    public X509Certificate2 GenerateRootCertificate(string commonName, DateTime notBefore, DateTime notAfter) {
        _logger.Info($"Generating root certificate with CN={commonName}");

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
        _logger.Info("Root certificate generated successfully.");

        return new X509Certificate2(certificate.Export(X509ContentType.Pfx), string.Empty,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    public X509Certificate2 GenerateSiteCertificate(X509Certificate2 rootCertificate) {
        _logger.Info("Generating site certificate...");

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
        foreach (var domain in domainArray) {
            sanBuilder.AddDnsName(domain);
            _logger.Debug($"Added DNS name to SAN: {domain}");
        }
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTime.UtcNow.AddDays(-1);
        var notAfter = notBefore.AddYears(1);

        using var rootCertificatePrivateKey = rootCertificate.GetRSAPrivateKey();
        if (rootCertificatePrivateKey == null) {
            _logger.Error("Root certificate doesn't have a private key");
            throw new InvalidOperationException("Root certificate doesn't have a private key");
        }

        var certificate = request.Create(rootCertificate, notBefore, notAfter, Guid.NewGuid().ToByteArray());
        var pfxCertificate = certificate.CopyWithPrivateKey(rsa);

        var pfxBytes = pfxCertificate.Export(X509ContentType.Pfx, string.Empty);
        var fileName = Path.Combine(Environment.CurrentDirectory, "SiteCert.pfx");
        File.WriteAllBytes(fileName, pfxBytes);

        _logger.Info($"Site certificate generated and saved to {fileName}");

        return new X509Certificate2(pfxBytes, string.Empty,
            X509KeyStorageFlags.Exportable |
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.MachineKeySet);
    }

    public bool ValidateDomainsInCertificate(X509Certificate2 certificate, IEnumerable<string> requiredDomains) {
        _logger.Info("Validating ALL required domains are present in certificate...");

        var requiredDomainsSet = new HashSet<string>(requiredDomains, StringComparer.OrdinalIgnoreCase);
        
        if (!requiredDomainsSet.Any()) {
            _logger.Info("No specific domains required. Validation successful by definition.");
            return true;
        }

        _logger.Info($"Required domains set: {{{string.Join(", ", requiredDomainsSet)}}}");
        
        var domainsFoundInCert = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        var subjectName = certificate.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.IsNullOrEmpty(subjectName)) {
            _logger.Debug($"Found CN: '{subjectName}'");
            domainsFoundInCert.Add(subjectName);
        }
        else {
            _logger.Debug("Certificate has no CN (SimpleName).");
        }
        
        const string sanOid = "2.5.29.17";

        if (certificate.Extensions
                .FirstOrDefault(ext => ext.Oid?.Value == sanOid) is { } sanExtension) {
            _logger.Debug("Found SAN extension. Parsing DNS names...");
            try
            {
                var asnData = new AsnEncodedData(sanExtension.Oid!, sanExtension.RawData);
                var sanString = asnData.Format(false);
                _logger.Debug($"Raw SAN string from Format(false): {sanString}");
                
                var matches = Regex.Matches(sanString, @"(?:DNS Name=|DNS:)\s*([^,\s]+)", RegexOptions.IgnoreCase);

                if (matches.Count == 0) _logger.Debug("No DNS names found in SAN extension via Regex on formatted string.");

                foreach (Match match in matches)
                    if (match.Groups.Count > 1) {
                        var domainInSan = match.Groups[1].Value.Trim();
                        _logger.Debug($"Found SAN DNS Name: '{domainInSan}'");
                        if (!string.IsNullOrEmpty(domainInSan)) domainsFoundInCert.Add(domainInSan);
                    }

                _logger.Debug("Finished parsing SAN DNS names via Regex.");
            }
            catch (Exception ex) {
                _logger.Error(ex);
                return false;
            }
        }
        else {
            _logger.Debug("Certificate has no SAN extension.");
        }

        _logger.Debug($"Domains found in certificate (CN + SAN DNS): {{{string.Join(", ", domainsFoundInCert)}}}");
        
        var allRequiredFound = true;
        var missingDomains = new List<string>();

        foreach (var requiredDomain in requiredDomainsSet)
            if (!domainsFoundInCert.Contains(requiredDomain)) {
                _logger.Warn($"Required domain '{requiredDomain}' was NOT found in the certificate.");
                allRequiredFound = false;
                missingDomains.Add(requiredDomain);
            }
            else {
                _logger.Debug($"Required domain '{requiredDomain}' was found in the certificate.");
            }
        
        if (allRequiredFound) {
            _logger.Info("Validation successful: Certificate contains ALL required domains.");
            return true;
        }
        else {
            _logger.Warn($"Validation failed: Certificate is missing the following required domains: {string.Join(", ", missingDomains)}");
            return false;
        }
    }
}