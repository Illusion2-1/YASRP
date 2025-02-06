using System.Net.NetworkInformation;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.Caching;
using YASRP.Network.Dns.Models;

namespace YASRP.Network.Dns.DoH;

public class FilteringStrategies(IDnsCacheService cacheService, AppConfiguration config) : IFilteringStrategies {
    private readonly AppConfiguration.IpSelectionSettings _settings = config.IpSelection;
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(FilteringStrategies));

    public void StartFiltering(string domain) {
        Task.Run(async () => await FilterAndUpdateAsync(domain));
    }

    private async Task FilterAndUpdateAsync(string domain) {
        if (!cacheService.TryGet(domain, out var originalRecord) ||
            originalRecord.IpAddresses == null ||
            originalRecord.IpAddresses.Count <= 1)
            return;

        var bestIp = await FindOptimalIpAsync(originalRecord.IpAddresses);
        if (bestIp == null) return;

        UpdateCache(domain, originalRecord, bestIp);
    }

    private async Task<string?> FindOptimalIpAsync(List<string> ips) {
        switch (_settings.Strategy) {
            case DnsSelectionStrategy.MinimumPing:
                return await FindFastestPingIpAsync(ips);
            // case DnsSelectionStrategy.FastestHandshake: 预留扩展
            default:
                return null;
        }
    }

    private async Task<string?> FindFastestPingIpAsync(List<string> ips) {
        var pingTasks = ips.Select(ip => MeasurePingAsync(ip)).ToList();
        var results = await Task.WhenAll(pingTasks);

        return results
            .Where(r => r.IsSuccess && r.ResponseTime <= _settings.MaxResponseTimeMs)
            .OrderBy(r => r.ResponseTime)
            .FirstOrDefault()
            .IpAddress;
    }

    private async Task<(string IpAddress, int ResponseTime, bool IsSuccess)> MeasurePingAsync(string ip) {
        try {
            using var ping = new Ping();
            _logger.Debug($"Measuring latency for {ip}");
            var reply = await ping.SendPingAsync(ip, _settings.MaxResponseTimeMs);
            return (ip, (int)reply.RoundtripTime, reply.Status == IPStatus.Success);
        }
        catch {
            return (ip, int.MaxValue, false);
        }
    }

    private void UpdateCache(string domain, DnsRecord originalRecord, string? bestIp) {
        try {
            if (bestIp != null) {
                var newRecord = new DnsRecord(
                    originalRecord.Domain,
                    new List<string> { bestIp },
                    originalRecord.LastUpdated,
                    originalRecord.ExpiresAt
                );

                cacheService.AddOrUpdate(domain, newRecord);
            }

            _logger.Debug($"Updated optimal IP for {domain}: {bestIp}");
        }
        catch (Exception ex) {
            _logger.Error($"Failed to update cache for {domain}: {ex.Message}");
        }
    }
}