using System;
using MelonLoader;
using TSKHook.Patches;
using TSKHook.Services;
using Utility.Diagnostics;
using Utility.Notifications;

[assembly: MelonInfo(
    typeof(TSKHook.Core),
    TSKHook.ModInfo.Name,
    TSKHook.ModInfo.Version,
    TSKHook.ModInfo.Author
)]
[assembly: HarmonyDontPatchAll]

namespace TSKHook;

public sealed class Core : MelonMod
{
    public static MelonLogger.Instance Log { get; private set; }

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        try
        {
            InitializeUtility();
            Config.Initialize();
            RuntimeController.Initialize();
            Translation.Initialize();
            PatchManager.Initialize();

            Logger.Info($"{ModInfo.Name} loaded successfully");
            Toast.Success(ModInfo.Name, $"Mod 加载成功，版本: {ModInfo.Version}", duration: 7f);
        }
        catch (Exception exception)
        {
            Log.Error($"Initialization failed: {exception}");
            Shutdown();
            throw;
        }
    }

    public override void OnUpdate() => RuntimeController.Update();

    public override void OnDeinitializeMelon() => Shutdown();

    private static void InitializeUtility()
    {
        Logging.SetSink(entry =>
        {
            string text =
                entry.Exception == null
                    ? $"[{entry.Category}] {entry.Message}"
                    : $"[{entry.Category}] {entry.Message}\n{entry.Exception}";

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Log.Warning(text);
                    break;
                case LogLevel.Error:
                    Log.Error(text);
                    break;
                default:
                    Log.Msg(text);
                    break;
            }
        });
        Toast.Initialize($"{ModInfo.Name}.ToastManager");
    }

    private static void Shutdown()
    {
        try
        {
            PatchManager.Shutdown();
        }
        catch (Exception exception)
        {
            Log?.Error($"Patch shutdown failed: {exception}");
        }

        try
        {
            Translation.Shutdown();
        }
        catch (Exception exception)
        {
            Log?.Error($"Translation shutdown failed: {exception}");
        }

        try
        {
            RuntimeController.Shutdown();
        }
        catch (Exception exception)
        {
            Log?.Error($"Runtime settings shutdown failed: {exception}");
        }

        try
        {
            Toast.Shutdown();
        }
        catch (Exception exception)
        {
            Log?.Error($"Toast shutdown failed: {exception}");
        }

        Logging.SetSink(null);
    }
}
