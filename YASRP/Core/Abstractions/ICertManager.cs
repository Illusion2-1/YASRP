using System.Security.Cryptography.X509Certificates;

namespace YASRP.Core.Abstractions;

public interface ICertManager {
    Task InitializeAsync(string rootCertCommonName);
    X509Certificate2 GetOrCreateSiteCertificate(List<string> domains);
}