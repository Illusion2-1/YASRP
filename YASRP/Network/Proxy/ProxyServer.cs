using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;

namespace YASRP.Network.Proxy;

public class ProxyServer : IDisposable, IYasrp {
    private readonly ICertManager _certManager;
    private readonly IDoHResolver _dohResolver;
    private readonly List<string> _targetDomains;
    private IWebHost? _webHost;
    private readonly HttpClient _httpClient;

    public ProxyServer(ICertManager certManager, IDoHResolver dohResolver, AppConfiguration config) {
        _certManager = certManager;
        _dohResolver = dohResolver;
        _targetDomains = config.TargetDomains;

        var handler = new SocketsHttpHandler {
            UseProxy = false,
            AllowAutoRedirect = false,
            SslOptions = new SslClientAuthenticationOptions {
                RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };

        _httpClient = new HttpClient(handler);
    }

    public async Task StartAsync() {
        var cert = _certManager.GetOrCreateSiteCertificate(_targetDomains.FirstOrDefault() ?? throw new InvalidOperationException("No target domain defined."));

        _webHost = new WebHostBuilder()
            .UseKestrel(options => {
                options.Listen(IPAddress.Any, 443, listenOptions => {
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
                        context.Response.StatusCode = 404;
                        return;
                    }

                    await HandleProxyRequest(context);
                });
            })
            .Build();

        await _webHost.StartAsync();
    }

    private async Task HandleProxyRequest(HttpContext context) {
        var targetHost = context.Request.Host.Host;
        var upstreamIps = await _dohResolver.QueryIpAddress(targetHost);

        if (upstreamIps == null || !upstreamIps.Any()) {
            context.Response.StatusCode = 502;
            return;
        }

        var targetIp = upstreamIps.FirstOrDefault();
        var requestMessage = new HttpRequestMessage();

        // Copy the request method and content
        requestMessage.Method = new HttpMethod(context.Request.Method);
        if (context.Request.ContentLength > 0) {
            requestMessage.Content = new StreamContent(context.Request.Body);
            foreach (var header in context.Request.Headers) {
                if (!header.Key.StartsWith("Content-"))
                    continue;
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Set up the request URI and headers
        var uriBuilder = new UriBuilder {
            Scheme = "https",
            Host = targetIp,
            Path = context.Request.Path,
            Query = context.Request.QueryString.ToString()
        };

        requestMessage.RequestUri = uriBuilder.Uri;
        requestMessage.Headers.Host = targetHost; // Keep original host for CDN

        // Copy remaining headers
        foreach (var header in context.Request.Headers) {
            if (header.Key.StartsWith("Content-") || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        try {
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();

            foreach (var header in response.Content.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();

            await response.Content.CopyToAsync(context.Response.Body);
        }
        catch (Exception) {
            context.Response.StatusCode = 502;
        }
    }

    public async Task StopAsync() {
        if (_webHost != null) {
            await _webHost.StopAsync();
            _webHost.Dispose();
        }
    }

    public void Dispose() {
        _webHost?.Dispose();
        _httpClient.Dispose();
    }
}