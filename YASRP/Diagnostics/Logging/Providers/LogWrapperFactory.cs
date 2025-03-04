using YASRP.Diagnostics.Logging.Models;

namespace YASRP.Diagnostics.Logging.Providers;

public static class LogWrapperFactory {
    public static ILogWrapper CreateLogger(string loggerName) {
        return new Log4NetWrapper(loggerName);
    }
}