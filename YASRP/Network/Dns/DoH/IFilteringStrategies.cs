namespace YASRP.Network.Dns.DoH;

public interface IFilteringStrategies {
    void StartFiltering(string domain);
}