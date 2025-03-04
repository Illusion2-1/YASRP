using System.Net;
using System.Text;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Network.Dns.DoH;

public class DoHClient {
    private readonly HttpClient _httpClient;
    private readonly ILogWrapper _logger;
    private readonly AppConfiguration _config;

    public DoHClient(AppConfiguration config) {
        _config = config;
        _httpClient = new HttpClient {
            DefaultRequestVersion = new Version(2, 0),
            Timeout = TimeSpan.FromMilliseconds(_config.Dns.MaxDnsTimeout)
        };
        _logger = LogWrapperFactory.CreateLogger(nameof(DoHClient));
    }

    public async Task<List<string>?> QueryAsync(string domain, string dohServer) {
        var maxRetries = _config.Dns.MaxRetries;
        var attempt = 0;
        var exceptions = new List<Exception>();

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        while (attempt < maxRetries)
            try {
                var query = Convert.ToBase64String(CreateDnsQuery(domain))
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

                _logger.Debug($"Attempt {attempt + 1}/{maxRetries}: Creating request");
                var request = new HttpRequestMessage {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{dohServer}?dns={query}"),
                    Headers = { { "accept", "application/dns-message" } }
                };

                _logger.Debug($"Sending HTTP request attempt {attempt + 1}");
                var response = await _httpClient.SendAsync(request);

                // 处理需要重试的HTTP状态码
                if ((int)response.StatusCode >= 500 && response.StatusCode != HttpStatusCode.NotImplemented)
                    throw new HttpRequestException($"Server error: {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var dnsMessage = await response.Content.ReadAsByteArrayAsync();
                _logger.Debug("Response received, parsing...");
                return ParseDnsResponse(dnsMessage);
            }
            catch (Exception ex) when (IsTransientFailure(ex)) {
                exceptions.Add(ex);
                attempt++;

                if (attempt >= maxRetries) break;

                _logger.Warn($"Attempt {attempt} failed: {ex.Message}. Retrying...");
            }
            catch (Exception ex) {
                _logger.Error($"Non-recoverable error: {ex.Message}");
                throw new AggregateException("Query failed with non-transient error", exceptions).Flatten();
            }

        _logger.Error($"DoH query failed after {maxRetries} attempts for {domain} using {dohServer}");
        throw new AggregateException("All retry attempts failed", exceptions);
    }

    private bool IsTransientFailure(Exception ex) {
        switch (ex) {
            case HttpRequestException _:
            case TimeoutException _:
                return true;
            case TaskCanceledException _ when ex.InnerException is TimeoutException:
                return true;
            default:
                return false;
        }
    }

    private byte[] CreateDnsQuery(string domain) {
        var random = new Random();
        var id = (ushort)random.Next(0, 65535);

        var message = new List<byte>();

        // Header
        message.AddRange(BitConverter.GetBytes(id).Reverse()); // ID
        message.AddRange(new byte[] { 0x01, 0x00 }); // Flags
        message.AddRange(new byte[] { 0x00, 0x01 }); // Questions
        message.AddRange(new byte[] { 0x00, 0x00 }); // Answer RRs
        message.AddRange(new byte[] { 0x00, 0x00 }); // Authority RRs
        message.AddRange(new byte[] { 0x00, 0x00 }); // Additional RRs

        // Query
        foreach (var label in domain.Split('.')) {
            message.Add((byte)label.Length);
            message.AddRange(Encoding.ASCII.GetBytes(label));
        }

        message.Add(0x00); // Root label

        // Type A
        message.AddRange([0x00, 0x01]);
        // Class IN
        message.AddRange([0x00, 0x01]);

        return message.ToArray();
    }

    private List<string> ParseDnsResponse(byte[] response) {
        var ipAddresses = new List<string>();

        // 头部12字节和查询部分
        var position = 12;

        // Query Name
        while (position < response.Length && response[position] != 0) position += response[position] + 1;
        position += 5; // 结束符(0x00)和Type与Class

        // Query Section
        int answerCount = BitConverter.ToUInt16(new[] { response[7], response[6] }, 0);

        for (var i = 0; i < answerCount; i++) {
            // 压缩指针/名称
            if ((response[position] & 0xC0) == 0xC0) {
                position += 2;
            }
            else {
                while (position < response.Length && response[position] != 0) position += response[position] + 1;
                position++;
            }

            // Type
            var type = BitConverter.ToUInt16(new[] { response[position + 1], response[position] }, 0);
            position += 2;

            // Class
            position += 2;

            // TTL
            position += 4;

            // 数据长度
            var dataLength = BitConverter.ToUInt16(new[] { response[position + 1], response[position] }, 0);
            position += 2;

            // A记录解析IP地址
            if (type == 1 && dataLength == 4) {
                var ip = $"{response[position]}.{response[position + 1]}.{response[position + 2]}.{response[position + 3]}";
                ipAddresses.Add(ip);
            }

            position += dataLength;
        }

        return ipAddresses;
    }
}