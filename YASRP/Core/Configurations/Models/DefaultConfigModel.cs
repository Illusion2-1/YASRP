using System.Net;

namespace YASRP.Core.Configurations.Models;

public enum DnsSelectionStrategy {
    MinimumPing,
    LessPackageLoss,
    FastestHandshake
}

public enum LogLevel {
    Debug,
    Info,
    Warn,
    Error,
    None
}

public class AppConfiguration {
    public List<string> TargetDomains { get; init; } = DefaultDomainPatterns.TargetDomains;

    public Dictionary<string, string?> CustomSnis { get; set; } = DefaultDomainPatterns.CustomSnis;

    public DnsSettings Dns { get; init; } = new();
    public IpSelectionSettings IpSelection { get; init; } = new();
    public LoggingSettings Logging { get; init; } = new();

    public KestrelSettings Kestrel { get; init; } = new();

    public class DnsSettings {
        public string PrimaryDohServer { get; init; } = "https://yasrp.illusionrealm.com/dns-query";

        public List<string> FallbackDohServers { get; init; } =
            [
                "https://9.9.9.9/dns-query",
                "https://1.1.1.1/dns-query",
                "https://101.101.101.101/dns-query",
                "https://208.67.222.222/dns-query",
                "https://223.5.5.5/dns-query"
            ];
        
        public bool DnsWarmup { get; init; } = true;
        
        public int DnsWarmupDelayMs { get; init; } = 1000;
        public int MaxCacheSize { get; init; } = 1000;
        public int CleanupIntervalMinutes { get; set; } = 5;

        public int MaxDnsTimeout { get; init; } = 2000;

        public int MaxRetries { get; init; } = 3;
        
        public int MaxCnameRecursion { get; set; } = 8;
        public int RetryBaseDelay { get; set; } = 100;
        public int RetryMaxDelay { get; set; } = 3000;
        
    }

    public class KestrelSettings {
        public string ListenAddress { get; init; } = IPAddress.Loopback.ToString();
        public int ListenPort { get; init; } = 443;
    }

    public class IpSelectionSettings {
        public DnsSelectionStrategy Strategy { get; init; } = DnsSelectionStrategy.MinimumPing;

        public int MaxResponseTimeMs { get; init; } = 1000;
        public int CacheDurationMinutes { get; init; } = 1440;
    }

    public class LoggingSettings {
        public LogLevel Level { get; init; } = LogLevel.Info;
    }
}