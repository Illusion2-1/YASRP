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
        _logger.Info($"Starting IP filtering for domain: {domain}");
        Task.Run(async () => await FilterAndUpdateAsync(domain));
    }

    private async Task FilterAndUpdateAsync(string domain) {
        _logger.Debug($"Begin filtering process for: {domain}");
        
        if (!cacheService.TryGet(domain, out var originalRecord)) {
            _logger.Warn($"No DNS record found in cache for: {domain}");
            return;
        }

        if (originalRecord.IpAddresses == null || originalRecord.IpAddresses.Count <= 1) {
            _logger.Info($"Skipping filtering for {domain} - less than 2 IP addresses available");
            return;
        }

        _logger.Debug($"Found {originalRecord.IpAddresses.Count} IPs for filtering: {string.Join(", ", originalRecord.IpAddresses)}");
        
        var bestIp = await FindOptimalIpAsync(originalRecord.IpAddresses);
        if (bestIp == null) {
            _logger.Warn($"No optimal IP found for {domain} after evaluation");
            return;
        }

        _logger.Info($"Selected optimal IP for {domain}: {bestIp}");
        UpdateCache(domain, originalRecord, bestIp);
    }

    private async Task<string?> FindOptimalIpAsync(List<string> ips) {
        _logger.Debug($"Applying {_settings.Strategy} strategy with {ips.Count} IPs");
        
        switch (_settings.Strategy) {
            case DnsSelectionStrategy.MinimumPing:
                return await FindFastestPingIpAsync(ips);
            default:
                _logger.Error($"Unsupported selection strategy: {_settings.Strategy}");
                return null;
        }
    }

    private async Task<string?> FindFastestPingIpAsync(List<string> ips) {
        _logger.Info($"Starting latency measurement for {ips.Count} IPs (Max allowed: {_settings.MaxResponseTimeMs}ms)");
        
        var pingTasks = ips.Select(ip => MeasurePingAsync(ip)).ToList();
        var results = await Task.WhenAll(pingTasks);

        var validResults = results.Where(r => r.IsSuccess && r.ResponseTime <= _settings.MaxResponseTimeMs).ToList();
        
        if (validResults.Count == 0) {
            _logger.Warn($"No IPs met the latency requirement of {_settings.MaxResponseTimeMs}ms");
            return null;
        }

        var bestResult = validResults.OrderBy(r => r.ResponseTime).First();
        _logger.Info($"Best IP selected - {bestResult.IpAddress} with {bestResult.ResponseTime}ms latency");
        
        return bestResult.IpAddress;
    }

    private async Task<(string IpAddress, int ResponseTime, bool IsSuccess)> MeasurePingAsync(string ip) {
        try {
            using var ping = new Ping();
            _logger.Debug($"Sending ping to {ip}");
            var reply = await ping.SendPingAsync(ip, _settings.MaxResponseTimeMs);
            
            var result = (ip, (int)reply.RoundtripTime, reply.Status == IPStatus.Success);
            
            _logger.Debug($"Ping result for {ip}: " +
                         $"{(result.Item3 ? $"Success ({result.Item2}ms)" : "Failed")}");
            
            return result;
        }
        catch (Exception ex) {
            _logger.Error($"Ping test failed for {ip}: {ex.Message}");
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
                _logger.Info($"Cache updated for {domain} with optimized IP: {bestIp}");
            }
        }
        catch (Exception ex) {
            _logger.Error($"Cache update failed for {domain}: {ex.Message}");
            _logger.Debug($"Failed cache data - Domain: {domain}, IP: {bestIp}");
        }
    }
}