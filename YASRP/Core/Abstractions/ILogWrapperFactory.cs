using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Models;

namespace YASRP.Core.Abstractions;

public interface ILogWrapperFactory {
    ILogWrapper CreateLogger(string loggerName);
}