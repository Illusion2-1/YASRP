using YASRP.Network.Dns.Models;

namespace YASRP.Network.Dns.Caching;

public interface IDnsCacheService {
    bool TryGet(string domain, out DnsRecord record);
    void AddOrUpdate(string domain, DnsRecord record);
    void Persist();
}