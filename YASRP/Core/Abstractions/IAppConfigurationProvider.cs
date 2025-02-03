using YASRP.Core.Configurations.Models;

namespace YASRP.Core.Abstractions;

public interface IConfigurationProvider
{
    AppConfiguration LoadConfiguration();
    void SaveConfiguration(AppConfiguration config);
}
