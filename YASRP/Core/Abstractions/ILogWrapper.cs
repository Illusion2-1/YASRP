namespace YASRP.Core.Abstractions;

public interface ILogWrapper {
    void Info(string message);
    void Debug(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception e);
}