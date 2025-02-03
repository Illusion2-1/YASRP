namespace YASRP.Core.Configurations.Models;

public enum DnsSelectionStrategy {
    MinimumPing,
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
    public List<string> TargetDomains { get; set; } = new();

    public DnsSettings Dns { get; set; } = new();
    public IpSelectionSettings IpSelection { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();

    public class DnsSettings {
        public string PrimaryDohServer { get; set; } = "https://1.1.1.1/dns-query";
        public string FallbackDohServer { get; set; } = "https://dns.google/dns-query";
        public string PlainDnsServer { get; set; } = "1.1.1.1";
    }

    public class IpSelectionSettings {
        public DnsSelectionStrategy Strategy { get; set; } = DnsSelectionStrategy.FastestHandshake;
        public int CacheDurationMinutes { get; set; } = 30;
    }

    public class LoggingSettings {
        public LogLevel Level { get; set; } = LogLevel.Info;
    }
}