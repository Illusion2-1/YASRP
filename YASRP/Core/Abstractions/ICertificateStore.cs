using System.Security.Cryptography.X509Certificates;

namespace YASRP.Core.Abstractions;

public interface ICertificateStore {
    void StoreRootCertificate(X509Certificate2 certificate, string friendlyName);
    X509Certificate2? GetRootCertificate(string friendlyName);
    void InstallRootCertificate(X509Certificate2 rootCertificate);
    void RemoveRootCertificate(X509Certificate2 rootCertificate);
}