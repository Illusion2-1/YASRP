namespace YASRP.Core.Abstractions;

public interface IDoHResolver {
    Task<List<string>?> QueryIpAddress(string domain);
}