using System.Collections.Concurrent;
using System.Text.Json;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.DoH;
using YASRP.Network.Dns.Models;

namespace YASRP.Network.Dns.Caching;

public class DnsCacheService : IDnsCacheService, IDisposable {
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(DnsCacheService));
    private readonly ConcurrentDictionary<string, DnsCacheItem> _cache;
    private readonly LinkedList<string> _lruOrder = new();
    private CancellationTokenSource _debounceCts = new();
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(50);
    private readonly object _syncRoot = new();
    private readonly int _maxSize;
    private readonly Timer _cleanupTimer;
    private readonly string _persistPath;

    public DnsCacheService(AppConfiguration config) {
        _maxSize = config.Dns.MaxCacheSize;
        _persistPath = Path.Combine(Environment.CurrentDirectory, "dns_cache.json");
        _cache = LoadPersistedCache();
        _logger.Info($"DNS cache initialized. Loaded {_cache.Count} entries from persistence");
        
        _cleanupTimer = new Timer(_ => Cleanup(), null,
            TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes), 
            TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes));
        
        _logger.Debug($"Cache cleanup scheduled every {config.Dns.CleanupIntervalMinutes} minutes");
    }

    public bool TryGet(string domain, out DnsRecord record) {
        if (_cache.TryGetValue(domain, out var item) && !item.IsExpired()) {
            lock (_syncRoot) {
                _lruOrder.Remove(item.LruNode);
                _lruOrder.AddFirst(item.LruNode);
            }
            _logger.Debug($"{domain}: Cache hit.");
            record = item.Record;
            return true;
        }

        _logger.Debug($"{domain}: Cache missed.");
        record = null!;
        return false;
    }

    public void AddOrUpdate(string domain, DnsRecord record) {
        var node = new LinkedListNode<string>(domain);
        var newItem = new DnsCacheItem(record, node);

        var operation = _cache.ContainsKey(domain) ? "Updating" : "Adding";
        _logger.Info($"{operation} cache entry for {domain} with {record.IpAddresses?.Count ?? 0} IPs");

        _cache.AddOrUpdate(domain, newItem, (_, existing) => {
            lock (_syncRoot) {
                _lruOrder.Remove(existing.LruNode);
            }
            return newItem;
        });

        lock (_syncRoot) {
            _lruOrder.AddFirst(node);
        }

        Persist();
        ScheduleSizeCheck();
    }

    private void EnforceSizeLimit() {
        lock (_syncRoot) {
            var initialCount = _cache.Count;
            while (_cache.Count > _maxSize && _lruOrder.Last != null) {
                var oldestDomain = _lruOrder.Last.Value;
                _cache.TryRemove(oldestDomain, out _);
                _lruOrder.RemoveLast();
            }
            var removedCount = initialCount - _cache.Count;
            if (removedCount > 0) {
                _logger.Info($"Enforced lru size limit - Removed {removedCount} old entries");
            }
        }
    }

    private void Cleanup() {
        var expiredKeys = _cache.Where(kv => kv.Value.IsExpired())
            .Select(kv => kv.Key).ToList();

        foreach (var key in expiredKeys) {
            if (!_cache.TryRemove(key, out var item)) continue;
            lock (_syncRoot) {
                _lruOrder.Remove(item.LruNode);
            }
        }

        _logger.Info($"Lru cache cleanup completed - Removed {expiredKeys.Count} expired entries");
        EnforceSizeLimit();
    }

    public void Persist() {
        var validRecords = _cache
            .Where(kv => !kv.Value.IsExpired())
            .ToDictionary(kv => kv.Key, kv => kv.Value.Record);

        try {
            var json = JsonSerializer.Serialize(validRecords);
            File.WriteAllText(_persistPath, json);
            _logger.Debug($"Persisted {validRecords.Count} DNS records to {_persistPath}");
        }
        catch (Exception ex) {
            _logger.Error($"Failed to persist DNS cache: {ex.Message}");
        }
    }

    private ConcurrentDictionary<string, DnsCacheItem> LoadPersistedCache() {
        try {
            if (!File.Exists(_persistPath)) {
                _logger.Debug("No existing cache file found");
                return new ConcurrentDictionary<string, DnsCacheItem>();
            }

            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                IncludeFields = false
            };

            var json = File.ReadAllText(_persistPath);
            var records = JsonSerializer.Deserialize<Dictionary<string, DnsRecord>>(json, options);

            var cache = new ConcurrentDictionary<string, DnsCacheItem>();
            if (records == null) return cache;
            
            foreach (var kv in records) {
                var node = new LinkedListNode<string>(kv.Key);
                var item = new DnsCacheItem(kv.Value, node);
                cache.TryAdd(kv.Key, item);
                _lruOrder.AddFirst(node);
            }

            _logger.Info($"Loaded {records.Count} entries from cache file");
            return cache;
        }
        catch (Exception ex) {
            _logger.Error($"Failed to load DNS cache: {ex.Message}");
            return new ConcurrentDictionary<string, DnsCacheItem>();
        }
    }

    public void Dispose() {
        _logger.Info("Disposing DNS cache service");
        _cleanupTimer.Dispose();
        Persist();
        _logger.Info("DNS cache service disposed");
    }

    private async void ScheduleSizeCheck() {
        try {
            await _debounceCts.CancelAsync();
            _debounceCts = new CancellationTokenSource();

            _logger.Debug("Scheduling debounced size check");
            await Task.Delay(_debounceInterval, _debounceCts.Token);
            _logger.Debug("Executing size check after debounce");
            EnforceSizeLimit();
        }
        catch (TaskCanceledException) {
            _logger.Debug("Size check was canceled due to new update");
        }
    }
}

public class DnsCacheItem(DnsRecord record, LinkedListNode<string> node) {
    public DnsRecord Record { get; } = record;
    public DateTime ExpiryTime { get; } = record.ExpiresAt;
    public LinkedListNode<string> LruNode { get; } = node;

    public bool IsExpired() {
        return DateTime.UtcNow >= ExpiryTime;
    }
}