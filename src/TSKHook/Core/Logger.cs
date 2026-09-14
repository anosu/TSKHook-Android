namespace TSKHook;

public static class Logger
{
    public static void Info(string message) => Core.Log.Msg(message);

    public static void Warn(string message) => Core.Log.Warning(message);

    public static void Error(string message) => Core.Log.Error(message);
}
