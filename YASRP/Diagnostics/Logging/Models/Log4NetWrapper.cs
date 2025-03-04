using log4net;
using log4net.Core;
using Spectre.Console;

namespace YASRP.Diagnostics.Logging.Models;

public class Log4NetWrapper(string loggerName) : ILogWrapper {
    private readonly ILog _logger = LogManager.GetLogger(loggerName);
    private static Level _currentLevel = Level.Debug;

    public void Info(string message) {
        if (_currentLevel <= Level.Info) _logger.Info(message);
    }

    public void Debug(string message) {
        if (_currentLevel == Level.Debug) _logger.Debug(message);
    }

    public void Warn(string message) {
        if (_currentLevel <= Level.Warn) _logger.Warn($"\u001b[33m{message}\u001b[0m");
    }

    public void Error(string message) {
        if (_currentLevel <= Level.Error) _logger.Error($"\u001b[31m{message}\u001b[0m");
    }

    public void Error(Exception e) {
        if (_currentLevel <= Level.Error) {
            _logger.Error(e.Message);
            AnsiConsole.WriteException(e, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes |
                                          ExceptionFormats.ShortenMethods | ExceptionFormats.ShowLinks);
        }
    }
}