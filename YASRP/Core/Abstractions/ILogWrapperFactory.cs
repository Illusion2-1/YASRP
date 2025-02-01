namespace YASRP.Core.Abstractions;

public interface ILogWrapperFactory {
    ILogWrapper CreateLogger(string loggerName);
}