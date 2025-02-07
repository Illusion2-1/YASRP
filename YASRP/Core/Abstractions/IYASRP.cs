namespace YASRP.Core.Abstractions;

public interface IYasrp {
    Task StartAsync();
    Task StopAsync();
}