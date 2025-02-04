namespace YASRP.Diagnostics.Logging.Models;

public interface ILogWrapper {
    void Info(string message);
    void Debug(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception e);
}