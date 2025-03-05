using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Models;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Monitoring.Traffic;

namespace YASRP.Network.Proxy;

public class ProxyServer : IDisposable, IYasrp {
    private readonly ICertManager _certManager;
    private readonly IDoHResolver _dohResolver;
    private readonly List<string> _targetDomains;
    private readonly IPAddress _listenIp;
    private readonly int _listenPort;
    private readonly int _warmupDelay;
    private readonly bool _doWarmup;
    private IWebHost? _webHost;
    private readonly HttpClient _httpClient;
    private readonly ILogWrapper _logger = LogWrapperFactory.CreateLogger(nameof(ProxyServer));

    public ProxyServer(ICertManager certManager, IDoHResolver dohResolver, AppConfiguration config) {
        _certManager = certManager;
        _dohResolver = dohResolver;
        _targetDomains = config.TargetDomains;
        _listenIp = IPAddress.Parse(config.Kestrel.ListenAddress);
        _listenPort = config.Kestrel.ListenPort;
        _doWarmup = config.Dns.DnsWarmup;
        _warmupDelay = config.Dns.DnsWarmupDelayMs;
        var handler = new SocketsHttpHandler {
            UseProxy = false,
            AllowAutoRedirect = false,
            MaxConnectionsPerServer = 100,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes),
            PooledConnectionLifetime = TimeSpan.FromMinutes(config.Dns.CleanupIntervalMinutes + 15),
            AutomaticDecompression = DecompressionMethods.None,
            EnableMultipleHttp2Connections = true,
            ConnectCallback = async (context, cancellationToken) => {
                var targetIp = context.DnsEndPoint.Host;
                var targetHost = targetIp;

                if (context.InitialRequestMessage.Headers.Host != null &&
                    config.CustomSnis.TryGetValue(context.InitialRequestMessage.Headers.Host, out var value))
                    if (value != null)
                        targetHost = value;

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(IPAddress.Parse(targetIp), context.DnsEndPoint.Port, cancellationToken);

                var sslStream = new SslStream(new NetworkStream(socket, true));
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                    TargetHost = targetHost,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, cancellationToken);

                return sslStream;
            }
        };

        _httpClient = new HttpClient(handler);
        _logger.Info("ProxyServer initialized with configuration.");
    }

    public async Task StartAsync() {
        _logger.Info("Starting Kestrel server...");

        var cert = _certManager.GetOrCreateSiteCertificate(_targetDomains);
        _logger.Debug("Certificate retrieved or created for target domains.");

        _webHost = new WebHostBuilder()
            .UseKestrel(options => {
                options.ConfigureEndpointDefaults(listenOptions => {
                    options.Limits.MinRequestBodyDataRate = null;
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2; // 启用HTTP/2
                });
                options.Listen(_listenIp, _listenPort, listenOptions => {
                    listenOptions.UseHttps(new HttpsConnectionAdapterOptions {
                        ServerCertificate = cert,
                        ClientCertificateMode = ClientCertificateMode.NoCertificate,
                        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    });
                });
            })
            .Configure(app => {
                app.Run(async context => {
                    if (!_targetDomains.Contains(context.Request.Host.Host)) {
                        _logger.Warn($"Request to non-target domain: {context.Request.Host.Host}");
                        context.Response.StatusCode = 404;
                        return;
                    }

                    await HandleProxyRequest(context);
                });
            })
            .Build();

        if (_doWarmup) _ = DnsWarmupTask();

        await _webHost.StartAsync();
        _logger.Info("Kestrel server started successfully.");
    }

    private async Task HandleProxyRequest(HttpContext context) {
        var targetHost = context.Request.Host.Host;
        context.Request.EnableBuffering();

        _logger.Debug($"Handling proxy request for host: {targetHost}");

        var requestCountingStream = new CountingStream(context.Request.Body);
        requestCountingStream.Position = 0;
        var responseCountingStream = new CountingStream(context.Response.Body);
        context.Response.Body = responseCountingStream;

        var upstreamIps = await _dohResolver.QueryIpAddress(targetHost);

        if (upstreamIps == null || !upstreamIps.Any()) {
            _logger.Error($"No IP addresses found for host: {targetHost}");
            context.Response.StatusCode = 502;
            return;
        }

        var targetIp = upstreamIps.FirstOrDefault();
        var requestMessage = new HttpRequestMessage();

        requestMessage.Method = new HttpMethod(context.Request.Method);
        if (context.Request.ContentLength > 0) requestMessage.Content = new StreamContent(requestCountingStream);

        // Set up the request URI and headers
        var uriBuilder = new UriBuilder {
            Scheme = "https",
            Host = targetIp,
            Path = context.Request.Path,
            Query = context.Request.QueryString.ToString()
        };

        requestMessage.RequestUri = uriBuilder.Uri;
        requestMessage.Headers.Host = targetHost; // Keep original host for CDN

        foreach (var header in context.Request.Headers)
            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) {
                if (requestMessage.Content != null) requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            else if (!string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)) {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

        try {
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            await response.Content.CopyToAsync(responseCountingStream);

            _logger.Debug($"Request completed for {targetHost}: TX={requestCountingStream.BytesRead} bytes, RX={responseCountingStream.BytesWritten} bytes");
        }
        catch (Exception ex) {
            _logger.Error(ex);
            context.Response.StatusCode = 502;
        }
    }

    public async Task StopAsync() {
        if (_webHost != null) {
            _logger.Info("Stopping Kestrel server...");
            await _webHost.StopAsync();
            _webHost.Dispose();
            _logger.Info("Kestrel server stopped successfully.");
        }
    }

    public void Dispose() {
        _logger.Info("Disposing Kestrel...");
        _webHost?.Dispose();
        _httpClient.Dispose();
        _logger.Info("Kestrel disposed successfully.");
    }

    private async Task DnsWarmupTask() {
        _logger.Info("Performing DNS warmup...");
        Task? warmTask = null;
        foreach (var domain in _targetDomains) {
            await Task.Delay(_warmupDelay);
            warmTask = Task.Run(() => _dohResolver.QueryIpAddress(domain));
        }

        if (warmTask != null) {
            warmTask.Wait();
            _logger.Info("DNS warmup completed");
        }
    }
}