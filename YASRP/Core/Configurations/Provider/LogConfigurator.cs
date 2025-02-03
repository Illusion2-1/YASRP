using System.Text.RegularExpressions;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;

namespace YASRP.Core.Configurations.Provider;

public static class LogConfigurator {
    public static void Configure() {
        var loggerRepository = LogManager.GetRepository();
        var layout = new PatternLayout("[%date{HH:mm:ss}] [THREAD-%thread/%level] [%logger]: %message%newline");
        layout.ActivateOptions();

        var appender = new ColoredConsoleAppender {
            Layout = layout
        };

        appender.AddMapping(new ColoredConsoleAppender.LevelColors {
            Level = Level.Info,
            ForeColor = ConsoleColor.Green
        });

        appender.AddMapping(new ColoredConsoleAppender.LevelColors {
            Level = Level.Warn,
            ForeColor = ConsoleColor.Yellow
        });

        appender.AddMapping(new ColoredConsoleAppender.LevelColors {
            Level = Level.Error,
            ForeColor = ConsoleColor.Red
        });

        appender.AddMapping(new ColoredConsoleAppender.LevelColors {
            Level = Level.Debug,
            ForeColor = ConsoleColor.Gray
        });

        appender.ActivateOptions();

        BasicConfigurator.Configure(loggerRepository, appender);
        loggerRepository.Threshold = Level.Debug;
    }
}

public class ColoredConsoleAppender : AppenderSkeleton {
    private PatternLayout? _layout;

    public new PatternLayout? Layout {
        get => _layout;
        set => _layout = value;
    }

    public class LevelColors {
        public Level? Level { get; set; }
        public ConsoleColor ForeColor { get; set; }
        public ConsoleColor BackColor { get; set; } = ConsoleColor.Black;
    }

    private readonly List<LevelColors> _levelColorMap = new();

    public void AddMapping(LevelColors levelColors) {
        _levelColorMap.Add(levelColors);
    }

    protected override void Append(LoggingEvent loggingEvent) {
        var message = _layout?.Format(loggingEvent);

        var originalForeground = Console.ForegroundColor;
        var originalBackground = Console.BackgroundColor;

        // Date part
        if (message != null) {
            var dateMatch = Regex.Match(message, @"^\[(.*?)\]");
            if (dateMatch.Success) {
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(dateMatch.Value);
                Console.ResetColor();
                message = message.Substring(dateMatch.Length);
            }
        }

        // Level part
        if (message != null) {
            var levelMatch = Regex.Match(message, @"\[(THREAD-.*?/(\w+))\]");
            if (levelMatch.Success) {
                Console.Write(message.Substring(0, levelMatch.Index));
                Console.Write("[" + levelMatch.Groups[1].Value.Substring(0, levelMatch.Groups[1].Value.LastIndexOf('/')));

                var level = levelMatch.Groups[2].Value;
                var levelColors = _levelColorMap.Find(lc => lc.Level != null && lc.Level.Name.Equals(level, StringComparison.OrdinalIgnoreCase));
                if (levelColors != null) {
                    Console.ForegroundColor = levelColors.ForeColor;
                    Console.BackgroundColor = levelColors.BackColor;
                }

                Console.Write("/" + level);
                Console.ResetColor();
                Console.Write("]");

                message = message.Substring(levelMatch.Index + levelMatch.Length);
            }
        }

        // Remaining part
        Console.Write(message);

        Console.ForegroundColor = originalForeground;
        Console.BackgroundColor = originalBackground;
    }
}