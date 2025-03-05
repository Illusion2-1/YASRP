using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.Caching;
using YASRP.Network.Dns.Models;

namespace YASRP.Network.Dns.DoH;

public class DoHResolver(AppConfiguration config, IDnsCacheService cacheService, IFilteringStrategies ipFilter) : IDoHResolver {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(DoHResolver));
    private readonly DoHClient _dohClient = new(config);
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<List<string>?> QueryIpAddress(string domain) {
        // 检查缓存
        if (cacheService.TryGet(domain, out var cachedRecord)) {
            return cachedRecord.IpAddresses;
        }

        // 如果缓存不存在或已过期，进行查询
        await _semaphore.WaitAsync();
        try {
            // 双重检查，防止其他线程已经更新了缓存
            if (cacheService.TryGet(domain, out cachedRecord)) return cachedRecord.IpAddresses;
            List<string>? ipAddresses = null;
            Exception? lastException;

            try {
                _logger.Debug("Querying DoH server");
                // 尝试使用主DoH服务器
                ipAddresses = await _dohClient.QueryAsync(domain, config.Dns.PrimaryDohServer);
            }
            catch (Exception? primaryEx) {
                _logger.Warn($"Primary DoH server failed: {primaryEx.Message}");
                lastException = primaryEx;

                // 遍历备用服务器列表
                foreach (var fallbackServer in config.Dns.FallbackDohServers)
                    try {
                        _logger.Debug($"Querying fallback DoH server: {fallbackServer}");
                        ipAddresses = await _dohClient.QueryAsync(domain, fallbackServer);
                        break; // 如果成功获取IP地址，跳出循环
                    }
                    catch (Exception? fallbackEx) {
                        _logger.Warn($"Fallback DoH server failed: {fallbackServer}, Error: {fallbackEx.Message}");
                        lastException = fallbackEx;
                    }

                if (ipAddresses == null) {
                    _logger.Error($"All DoH servers failed. Last error: {lastException.Message}");
                    throw new AggregateException("All DoH servers failed", lastException);
                }
            }

            if (ipAddresses != null && !ipAddresses.Any()) throw new Exception($"No IP addresses found for {domain}");

            if (config.Logging.Level == LogLevel.Debug && ipAddresses != null)
                foreach (var address in ipAddresses)
                    _logger.Debug(address);
            // 更新缓存
            var record = new DnsRecord(
                domain,
                ipAddresses,
                TimeSpan.FromMinutes(config.IpSelection.CacheDurationMinutes)
            );

            cacheService.AddOrUpdate(domain, record);

            ipFilter.StartFiltering(domain);

            return ipAddresses;
        }

        finally {
            _semaphore.Release();
        }
    }
}