using Microsoft.Extensions.Hosting;
using YASRP.Core.Abstractions;

namespace YASRP.Network.Proxy;

public class ProxyServiceWrapper : IHostedService
{
    private readonly IYasrp _proxyServer;

    public ProxyServiceWrapper(IYasrp proxyServer)
    {
        _proxyServer = proxyServer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _proxyServer.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _proxyServer.StopAsync();
    }
}
