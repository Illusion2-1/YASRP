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
    public List<string> TargetDomains { get; init; } = ["steamcommunity.com", "huggingface.co"];

    public Dictionary<string, string?> CusdomSnis { get; set; } = new () {{"huggingface.co", "d3q5pwvs88w1av.cloudfront.net"}} ;
    
    public DnsSettings Dns { get; init; } = new();
    public IpSelectionSettings IpSelection { get; init; } = new();
    public LoggingSettings Logging { get; init; } = new();

    public KestrelSettings Kestrel { get; init; } = new();

    public class DnsSettings {
        public string PrimaryDohServer { get; init; } = "https://9.9.9.9/dns-query";

        public List<string> FallbackDohServers { get; init; } =
            ["https://1.1.1.1/dns-query", "https://101.101.101.101/dns-query", "https://208.67.222.222/dns-query", "https://223.5.5.5/dns-query"];

        public string PlainDnsServer { get; init; } = "1.1.1.1";
        public int MaxCacheSize { get; init; } = 1000;
        public int CleanupIntervalMinutes { get; set; } = 5;

        public int MaxDnsTimeout { get; init; } = 500;
        
        public int MaxRetries { get; init; } = 3;
    }

    public class KestrelSettings {
        public string ListenAddress { get; init; } = IPAddress.Loopback.ToString();
        public int ListenPort { get; init; } = 443;
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