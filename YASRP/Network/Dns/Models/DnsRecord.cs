using System.Text.Json.Serialization;

namespace YASRP.Network.Dns.Models;

public class DnsRecord {
    [JsonPropertyName("domain")] public string Domain { get; init; }

    [JsonPropertyName("ipAddresses")] public List<string>? IpAddresses { get; init; }

    [JsonPropertyName("lastUpdated")] public DateTime LastUpdated { get; init; }

    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }

    [JsonConstructor]
    public DnsRecord(
        string domain,
        List<string>? ipAddresses,
        DateTime lastUpdated,
        DateTime expiresAt) {
        Domain = domain;
        IpAddresses = ipAddresses;
        LastUpdated = lastUpdated;
        ExpiresAt = expiresAt;
    }

    public DnsRecord(string domain, List<string>? ipAddresses, TimeSpan cacheDuration)
        : this(domain, ipAddresses, DateTime.UtcNow, DateTime.UtcNow.Add(cacheDuration)) { }

    public bool IsExpired() {
        return DateTime.UtcNow >= ExpiresAt;
    }
}