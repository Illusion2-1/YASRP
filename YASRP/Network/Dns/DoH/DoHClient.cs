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
        return await QueryInternalAsync(domain, dohServer);
    }

    private async Task<List<string>?> QueryInternalAsync(string domain, string dohServer, int cnameDepth = 0) {
        if (cnameDepth > _config.Dns.MaxCnameRecursion) {
            _logger.Warn($"CNAME recursion depth exceeded for {domain}");
            return null;
        }

        var maxRetries = _config.Dns.MaxRetries;
        var exceptions = new List<Exception>();

        for (var attempt = 0; attempt < maxRetries; attempt++)
            try {
                var query = BuildDnsQuery(domain);
                var request = new HttpRequestMessage {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{dohServer}?dns={query}&type=A"),
                    Headers = { { "accept", "application/dns-message" } }
                };

                _logger.Debug($"Attempt {attempt + 1}/{maxRetries}: Querying {domain}");
                var response = await _httpClient.SendAsync(request);

                if ((int)response.StatusCode >= 500 && response.StatusCode != HttpStatusCode.NotImplemented)
                    throw new HttpRequestException($"Server error: {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var dnsMessage = await response.Content.ReadAsByteArrayAsync();
                var (aRecords, cnames, hasSoa) = ParseDnsResponse(dnsMessage);

                if (aRecords.Count > 0) {
                    _logger.Debug($"Resolved {domain} to [{string.Join(", ", aRecords)}]");
                    return aRecords;
                }

                foreach (var cname in cnames.Distinct()) {
                    _logger.Debug($"Following CNAME {cname} for {domain}");
                    var results = await QueryInternalAsync(cname, dohServer, cnameDepth + 1);
                    if (results != null) return results;
                }

                if (hasSoa)
                    _logger.Debug($"SOA record encountered for {domain}, no results available");

                return null;
            }
            catch (Exception ex) when (IsTransientFailure(ex)) {
                exceptions.Add(ex);
                _logger.Warn($"Attempt {attempt + 1} failed: {ex.Message}");
                if (attempt == maxRetries - 1) break;
                await Task.Delay(CalculateBackoffDelay(attempt));
            }
            catch (Exception ex) {
                _logger.Error($"Critical error: {ex.Message}");
                exceptions.Add(ex);
                break;
            }

        _logger.Error($"DoH query failed after {maxRetries} attempts for {domain}");
        throw new AggregateException("All retry attempts failed", exceptions);
    }

    private string BuildDnsQuery(string domain) {
        var query = CreateDnsQuery(domain);
        return Convert.ToBase64String(query)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private byte[] CreateDnsQuery(string domain) {
        var random = new Random();
        var id = (ushort)random.Next(0, 65535);
        var message = new List<byte>();

        // Header
        message.AddRange(BitConverter.GetBytes(id).Reverse());
        message.AddRange([0x01, 0x00]); // Standard query
        message.AddRange([0x00, 0x01]); // Questions
        message.AddRange([0x00, 0x00]); // Answer RRs
        message.AddRange([0x00, 0x00]); // Authority RRs
        message.AddRange([0x00, 0x00]); // Additional RRs

        // Question
        foreach (var label in domain.Split('.')) {
            message.Add((byte)label.Length);
            message.AddRange(Encoding.ASCII.GetBytes(label));
        }

        message.Add(0x00); // Terminator
        message.AddRange([0x00, 0x01]); // Type A
        message.AddRange([0x00, 0x01]); // Class IN

        return message.ToArray();
    }

    public (List<string> ARecords, List<string> Cnames, bool HasSoa) ParseDnsResponse(byte[] response) {
        var aRecords = new List<string>();
        var cnames = new List<string>();
        var hasSoa = false;

        try {
            //  0-1: ID, 2-3: Flags, 4-5: QDCOUNT, 6-7: ANCOUNT, 8-9: NSCOUNT, 10-11: ARCOUNT
            var answerCount = (response[6] << 8) | response[7];
            var position = 12; // DNS 头部
            
            position = ReadDnsName(response, position, out _);
            //  QTYPE (2) 和 QCLASS (2)
            position += 4;

            // 遍历所有 Answer
            for (var i = 0; i < answerCount && position < response.Length; i++) {
                // 名（NAME）
                position = ReadDnsName(response, position, out _);

                // TYPE (2)、CLASS (2)、TTL (4)、RDLENGTH (2)
                var type = (response[position] << 8) | response[position + 1];
                position += 2;

                _ = (response[position] << 8) | response[position + 1];
                position += 2;

                // TTL（4字节）
                _ = ((uint)response[position] << 24) | ((uint)response[position + 1] << 16) |
                    ((uint)response[position + 2] << 8) | response[position + 3];
                position += 4;

                var dataLength = (response[position] << 8) | response[position + 1];
                position += 2;

                // 根据不同的 TYPE 处理 RDATA
                switch (type) {
                    case 1 when dataLength == 4: // A (IPv4)
                        aRecords.Add($"{response[position]}.{response[position + 1]}.{response[position + 2]}.{response[position + 3]}");
                        break;
                    case 5: // CNAME 
                        var cnamePos = position;
                        var cname = ReadDnsName(response, ref cnamePos);
                        cnames.Add(cname);
                        break;
                    case 6: // SOA，直接标记一下即可
                        hasSoa = true;
                        break;
                }

                // 跳过 RDATA 部分
                position += dataLength;
            }
        }
        catch (Exception ex) {
            _logger.Error($"DNS response parsing failed: {ex.Message}");
        }

        return (aRecords, cnames, hasSoa);
    }

    private int ReadDnsName(byte[] response, int position, out string name) {
        var labels = new List<string>();
        var recursionLimit = 0;

        while (true) {
            if (position >= response.Length || recursionLimit++ > 16)
                break;

            var labelLength = response[position++];
            if (labelLength == 0)
                break;

            // 压缩指针：高两位为1
            if ((labelLength & 0xC0) == 0xC0) {
                var pointer = ((labelLength & 0x3F) << 8) | response[position++];
                ReadDnsName(response, pointer, out var compressedName);
                labels.Add(compressedName);
                break;
            }
            
            if (position + labelLength > response.Length)
                break;
            var label = Encoding.ASCII.GetString(response, position, labelLength);
            labels.Add(label);
            position += labelLength;
        }

        name = string.Join(".", labels);
        return position;
    }

    private string ReadDnsName(byte[] response, ref int position) {
        var name = new StringBuilder();
        var recursionLimit = 0;

        while (true) {
            if (position >= response.Length || recursionLimit++ > 16)
                break;

            var labelLength = response[position++];
            if (labelLength == 0)
                break;

            if ((labelLength & 0xC0) == 0xC0) {
                var pointer = ((labelLength & 0x3F) << 8) | response[position++];
                var tempPos = pointer;
                name.Append(ReadDnsName(response, ref tempPos));
                break;
            }

            if (position + labelLength > response.Length)
                break;

            name.Append(Encoding.ASCII.GetString(response, position, labelLength));
            position += labelLength;
            name.Append('.');
        }

        if (name.Length > 0 && name[name.Length - 1] == '.')
            name.Length--; // 移除末尾的点

        return name.ToString();
    }

    private bool IsTransientFailure(Exception ex) {
        return ex is HttpRequestException or TimeoutException ||
               ex is TaskCanceledException { InnerException: TimeoutException };
    }

    private TimeSpan CalculateBackoffDelay(int attempt) {
        var baseDelay = _config.Dns.RetryBaseDelay;
        var maxDelay = _config.Dns.RetryMaxDelay;
        return TimeSpan.FromMilliseconds(Math.Min(baseDelay * Math.Pow(2, attempt), maxDelay));
    }
}