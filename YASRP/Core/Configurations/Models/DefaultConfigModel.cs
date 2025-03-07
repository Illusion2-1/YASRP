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
    public List<string> TargetDomains { get; init; } = [
        "steamcommunity.com",
        "www.steamcommunity.com",
        "store.steampowered.com",
        "store.akamai.steamstatic.com",
        "google.com",
        "www.google.com",
        "huggingface.co",
        "www.huggingface.co",
        "datasets-server.huggingface.co",
        "cdn-thumbnails.huggingface.co",
        "transformer.huggingface.co",
        "transformers.huggingface.co",
        "cdn-avatars.huggingface.co",
        "cdn-lfs-us-1.huggingface.co",
        "cdn-uploads.huggingface.co",
        "cdn-lfs-eu-1.huggingface.co",
        "convai.huggingface.co",
        "cdn-lfs.huggingface.co",
        "cdn.huggingface.co",
        "cdn-datasets.huggingface.co",
        "discuss.huggingface.co",
        "status.huggingface.co",
        "ui.endpoints.huggingface.co",
        "store.huggingface.co",
        "dell.huggingface.co",
        "neuralconvo.huggingface.co",
        "home.huggingface.co",
        "api-inference.huggingface.co",
        "thumbnails.huggingface.co",
        "ui.autotrain.huggingface.co",
        "github.com",
        "www.github.com",
        "gist.github.com",
        "api.github.com",
        "codeload.github.com",
        "support.github.com",
        "raw.githubusercontent.com",
        "raw.github.com",
        "camo.githubusercontent.com",
        "cloud.githubusercontent.com",
        "avatars.githubusercontent.com",
        "avatars0.githubusercontent.com",
        "avatars1.githubusercontent.com",
        "avatars2.githubusercontent.com",
        "avatars3.githubusercontent.com",
        "user-images.githubusercontent.com",
        "private-user-images.githubusercontent.com",
        "github-releases.githubusercontent.com",
        "analytics.githubassets.com",
        "desktop.githubusercontent.com",
        "lab.github.com",
        "assets-cdn.github.com",
        "www.github.io",
        "pages.github.com",
        "resources.github.com",
        "developer.github.com",
        "partner.github.com",
        "desktop.github.com",
        "guides.github.com",
        "github-releases.githubusercontent.com",
        "objects.githubusercontent.com"
    ];

    public Dictionary<string, string?> CustomSnis { get; set; } = new() {
        { "huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "www.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "datasets-server.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "cdn-thumbnails.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "transformer.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "transformers.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "cdn-avatars.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "cdn-lfs-us-1.huggingface.co", "d3tt2suyqs9zqv.cloudfront.net" },
        { "cdn-uploads.huggingface.co", "d1cylya5vv74ss.cloudfront.net" },
        { "cdn-lfs-eu-1.huggingface.co", "d1wmdo6fswuln8.cloudfront.net" },
        { "convai.huggingface.co", "d1cnjqbqjby1vq.cloudfront.net" },
        { "cdn-lfs.huggingface.co", "d2243ylfu57tc6.cloudfront.net" },
        { "cdn.huggingface.co", "d2ws9o8vfrpkyk.cloudfront.net" },
        { "cdn-datasets.huggingface.co", "d36easquyfvmrn.cloudfront.net" },
        { "discuss.huggingface.co", "hellohellohello.hosted-by-discourse.com" },
        { "status.huggingface.co", "statuspage.betteruptime.com" },
        { "ui.endpoints.huggingface.co", "cname.vercel-dns.com" },
        { "store.huggingface.co", "cname.vercel-dns.com" },
        { "dell.huggingface.co", "cname.vercel-dns.com" },
        { "neuralconvo.huggingface.co", "d3bh913krp35a5.cloudfront.net" },
        { "home.huggingface.co", "hugging-face.customdomains.okta.com" },
        { "api-inference.huggingface.co", "huggingface.co/docs/api-inference/index" },
        { "thumbnails.huggingface.co", "d3q5pwvs88w1av.cloudfront.net" },
        { "ui.autotrain.huggingface.co", "huggingface.co/autotrain" }
    };

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

        public string PlainDnsServer { get; init; } = "1.1.1.1";
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