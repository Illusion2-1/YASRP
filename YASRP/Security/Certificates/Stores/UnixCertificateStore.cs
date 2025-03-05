using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Security.Certificates.Stores;

public class UnixCertificateStore : ICertificateStore {
    private const string PfxStore = "./";
    private const string UbuntuCertStore = "/usr/local/share/ca-certificates/";
    private const string UbuntuCertUpdateCommand = "update-ca-certificates";
    private readonly ILogWrapper _logger;

    public UnixCertificateStore() {
        _logger = LogWrapperFactory.CreateLogger(nameof(UnixCertificateStore));
    }

    public void StoreRootCertificate(X509Certificate2 certificate, string friendlyName) {
        var certPath = Path.Combine(PfxStore, $"{friendlyName}.pfx");
        
        var certData = certificate.Export(X509ContentType.Pfx);
        File.WriteAllBytes(certPath, certData);

        _logger.Info($"Stored certificate at: {certPath}");
    }

    public X509Certificate2? GetRootCertificate(string friendlyName) {
        var certPath = Path.Combine(PfxStore, $"{friendlyName}.pfx");
        if (!File.Exists(certPath))
            return null;

        try {
            return new X509Certificate2(certPath);
        }
        catch (Exception ex) {
            _logger.Error($"Failed to load certificate: {ex.Message}");
            return null;
        }
    }

    public void InstallRootCertificate(X509Certificate2 rootCertificate) {
        var certPath = Path.Combine(UbuntuCertStore, $"Yasrp.crt");

        if (File.Exists(certPath)) return;

        try {
            var certPem = ExportCertificateAsPem(rootCertificate);

            File.WriteAllText(certPath, certPem);

            // 执行 update-ca-certificates 命令更新证书存储
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo {
                FileName = UbuntuCertUpdateCommand,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                var error = process.StandardError.ReadToEnd();
                throw new Exception($"Failed to update certificates: {error}");
            }

            _logger.Info("Successfully updated system certificates");
        }
        catch (Exception ex) {
            _logger.Error($"Failed to install root certificate: {ex.Message}");
            throw;
        }
    }

    private string ExportCertificateAsPem(X509Certificate2 certificate) {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        builder.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }

    public void RemoveRootCertificate(X509Certificate2 rootCertificate) {
        File.Delete(Path.Combine(UbuntuCertStore, "Yasrp.crt"));
    }
}