using System.Collections.Concurrent;
using System.Text.Json;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.DoH;
using YASRP.Network.Dns.Models;

namespace YASRP.Network.Dns.Caching;

public class DnsCacheService : IDnsCacheService, IDisposable {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(DoHResolver));
    private readonly ConcurrentDictionary<string, DnsCacheItem> _cache;
    private readonly LinkedList<string> _lruOrder = new();
    private readonly object _syncRoot = new();
    private readonly int _maxSize;
    private readonly Timer _cleanupTimer;
    private readonly string _persistPath;

    public DnsCacheService(AppConfiguration config) {
        _maxSize = config.Dns.MaxCacheSize;
        _persistPath = Path.Combine(Environment.CurrentDirectory, "dns_cache.json");
        _cache = LoadPersistedCache();
        _cleanupTimer = new Timer(_ => Cleanup(), null,
            TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes), TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes));
    }

    public bool TryGet(string domain, out DnsRecord record) {
        if (_cache.TryGetValue(domain, out var item) && !item.IsExpired()) {
            UpdateLruPosition(domain);
            record = item.Record;
            return true;
        }

        record = null;
        return false;
    }

    public void AddOrUpdate(string domain, DnsRecord record) {
        var newItem = new DnsCacheItem(record);
        _cache.AddOrUpdate(domain, newItem, (_, _) => newItem);
        UpdateLruPosition(domain);
        EnforceSizeLimit();
    }

    private void UpdateLruPosition(string domain) {
        lock (_syncRoot) {
            _lruOrder.Remove(domain);
            _lruOrder.AddFirst(domain);
        }
    }

    private void EnforceSizeLimit() {
        lock (_syncRoot) {
            while (_cache.Count > _maxSize && _lruOrder.Last != null) {
                var oldestDomain = _lruOrder.Last.Value;
                _cache.TryRemove(oldestDomain, out _);
                _lruOrder.RemoveLast();
            }
        }
    }

    private void Cleanup() {
        var expiredKeys = _cache.Where(kv => kv.Value.IsExpired())
            .Select(kv => kv.Key).ToList();
        foreach (var key in expiredKeys) {
            _cache.TryRemove(key, out _);
            lock (_syncRoot) {
                _lruOrder.Remove(key);
            }
        }

        EnforceSizeLimit();
    }

    public void Persist() {
        var validRecords = _cache.Where(kv => !kv.Value.IsExpired())
            .ToDictionary(kv => kv.Key, kv => kv.Value.Record);
        File.WriteAllText(_persistPath, JsonSerializer.Serialize(validRecords));
    }

    private ConcurrentDictionary<string, DnsCacheItem> LoadPersistedCache() {
        try {
            if (!File.Exists(_persistPath)) return new ConcurrentDictionary<string, DnsCacheItem>();

            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                IncludeFields = false
            };

            var json = File.ReadAllText(_persistPath);
            var records = JsonSerializer.Deserialize<Dictionary<string, DnsRecord>>(json, options);

            return new ConcurrentDictionary<string, DnsCacheItem>(records?
                                                                      .Where(r => new DnsCacheItem(r.Value).IsExpired() == false)
                                                                      .ToDictionary(r => r.Key, r => new DnsCacheItem(r.Value))
                                                                  ?? new Dictionary<string, DnsCacheItem>());
        }
        catch (Exception ex) {
            _logger.Error($"Failed to load DNS cache: {ex.Message}");
            return new ConcurrentDictionary<string, DnsCacheItem>();
        }
    }


    public void Dispose() {
        _cleanupTimer?.Dispose();
    }
}

public class DnsCacheItem {
    public DnsRecord Record { get; }
    public DateTime ExpiryTime { get; }

    public DnsCacheItem(DnsRecord record) {
        Record = record;
        ExpiryTime = record.ExpiresAt;
    }

    public bool IsExpired() {
        return DateTime.UtcNow >= ExpiryTime;
    }
}