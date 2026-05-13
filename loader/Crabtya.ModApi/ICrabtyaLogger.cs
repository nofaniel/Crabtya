namespace Crabtya.ModApi;

public interface ICrabtyaLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}
