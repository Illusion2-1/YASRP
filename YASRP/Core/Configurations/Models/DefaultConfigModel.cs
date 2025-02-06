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
    public List<string> TargetDomains { get; init; } = new();

    public DnsSettings Dns { get; init; } = new();
    public IpSelectionSettings IpSelection { get; init; } = new();
    public LoggingSettings Logging { get; init; } = new();

    public class DnsSettings {
        public string PrimaryDohServer { get; init; } = "https://9.9.9.9/dns-query";

        public List<string> FallbackDohServers { get; init; } =
            ["https://1.1.1.1/dns-query", "https://101.101.101.101/dns-query", "https://208.67.222.222/dns-query", "https://223.5.5.5/dns-query"];

        public string PlainDnsServer { get; init; } = "1.1.1.1";
        public int MaxCacheSize { get; init; } = 1000;
        public int CleanupIntervalMinutes { get; set; } = 5;
    }

    public class IpSelectionSettings {
        public DnsSelectionStrategy Strategy { get; init; } = DnsSelectionStrategy.MinimumPing;
        
        public int MaxResponseTimeMs { get; init; } = 1000;
        public int CacheDurationMinutes { get; init; } = 30;
    }

    public class LoggingSettings {
        public LogLevel Level { get; init; } = LogLevel.Debug;
    }
}