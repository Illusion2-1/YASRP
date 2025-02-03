using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Diagnostics.Logging.Providers;

namespace YASRP.Core.Configurations.Provider;

public class AppConfigurationProvider : IConfigurationProvider {
    private const string DefaultFileName = "config.yaml";
    private readonly string _configPath;
    private readonly ILogWrapper _logger;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public AppConfigurationProvider() : this(Path.Combine(AppContext.BaseDirectory, DefaultFileName)) { }

    public AppConfigurationProvider(string configPath) {
        _configPath = configPath;
        _logger = LogWrapperFactory.CreateLogger(nameof(AppConfiguration));

        var namingConvention = CamelCaseNamingConvention.Instance;
        _serializer = new SerializerBuilder()
            .WithNamingConvention(namingConvention)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(namingConvention)
            .Build();

        CheckConfigPresence();
    }

    public AppConfiguration LoadConfiguration() {
        try {
            if (!File.Exists(_configPath)) {
                _logger.Warn($"Config file not found at {_configPath}, creating default configuration");
                return CreateDefaultConfig();
            }

            var yaml = File.ReadAllText(_configPath);
            var config = _deserializer.Deserialize<AppConfiguration>(yaml);

            _logger.Info("Configuration loaded successfully");
            return config ?? CreateDefaultConfig();
        }
        catch (Exception ex) {
            _logger.Error($"Failed to load configuration: {ex.Message}");
            return CreateDefaultConfig();
        }
    }

    public void SaveConfiguration(AppConfiguration config) {
        try {
            var yaml = _serializer.Serialize(config);
            File.WriteAllText(_configPath, yaml);
            _logger.Info("Configuration saved successfully");
        }
        catch (Exception ex) {
            _logger.Error($"Failed to save configuration: {ex.Message}");
        }
    }

    private AppConfiguration CreateDefaultConfig() {
        var defaultConfig = new AppConfiguration();
        SaveConfiguration(defaultConfig); // Auto-save default config
        return defaultConfig;
    }

    private void CheckConfigPresence() {
        try {
            var dir = Path.GetDirectoryName(_configPath);
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
                _logger.Info($"Created configuration directory: {dir}");
            }
        }
        catch (Exception ex) {
            _logger.Error($"Failed to create config directory: {ex.Message}");
        }
    }
}