using log4net;
using Spectre.Console;
using YASRP.Core.Abstractions;

namespace YASRP.Diagnostics.Logging.Models;

public class Log4NetWrapper(string loggerName) : ILogWrapper {
    private readonly ILog _logger = LogManager.GetLogger(loggerName);

    public void Info(string message) {
        _logger.Info(message);
    }

    public void Debug(string message) {
        _logger.Debug(message);
    }

    public void Warn(string message) {
        _logger.Warn(message);
    }

    public void Error(string message) {
        _logger.Error($"\u001b[31m{message}\u001b[0m");
    }

    public void Error(Exception e) {
        _logger.Error(e.Message);
        AnsiConsole.WriteException(e, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes |
                                      ExceptionFormats.ShortenMethods | ExceptionFormats.ShowLinks);
    }
}