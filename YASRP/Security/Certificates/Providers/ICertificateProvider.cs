using System.Security.Cryptography.X509Certificates;

namespace YASRP.Security.Certificates.Providers;

public interface ICertificateProvider {
    X509Certificate2 GenerateRootCertificate(string commonName, DateTime notBefore, DateTime notAfter);
    X509Certificate2 GenerateSiteCertificate(X509Certificate2 rootCertificate);
    bool ValidateDomainInCertificate(X509Certificate2 certificate, IEnumerable<string> allowedDomains);
}