using System;
using UnityEngine;
using Utility.Notifications;

namespace TSKHook;

public static class RuntimeController
{
    private const float EnforcementIntervalSeconds = 1.0f;

    private static bool _initialized;
    private static bool _gameSpeedApplied;
    private static float _originalGameSpeed;
    private static int _originalTargetFrameRate;
    private static float _nextEnforcementTime;

    public static float GameSpeed => NormalizeGameSpeed(Config.GameSpeed?.Value ?? 1.0f);

    public static int TargetFrameRate =>
        NormalizeTargetFrameRate(Config.TargetFrameRate?.Value ?? 60);

    public static float ZoomRatio => NormalizeZoomRatio(Config.ZoomRatio?.Value ?? 1.0f);

    public static void Initialize()
    {
        if (_initialized)
            return;

        _originalTargetFrameRate = Application.targetFrameRate;
        _initialized = true;
        ApplySettings(logChanges: true);
        Toast.Info(
            "运行设置",
            _gameSpeedApplied
                ? $"游戏速度 {GameSpeed:0.##}x，目标帧率 {TargetFrameRate}"
                : $"游戏速度修改已关闭，目标帧率 {TargetFrameRate}"
        );
    }

    public static void Update()
    {
        if (!_initialized || Time.realtimeSinceStartup < _nextEnforcementTime)
            return;

        _nextEnforcementTime = Time.realtimeSinceStartup + EnforcementIntervalSeconds;
        ApplySettings(logChanges: false);
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        RestoreGameSpeed();
        Application.targetFrameRate = _originalTargetFrameRate;
        _initialized = false;
        _nextEnforcementTime = 0;
    }

    private static void ApplySettings(bool logChanges)
    {
        int targetFrameRate = TargetFrameRate;

        if (Config.GameSpeedEnabled?.Value == true)
        {
            if (!_gameSpeedApplied)
            {
                _originalGameSpeed = Time.timeScale;
                _gameSpeedApplied = true;
            }

            float gameSpeed = GameSpeed;
            if (Math.Abs(Time.timeScale - gameSpeed) > 0.001f)
            {
                Time.timeScale = gameSpeed;
                Logger.Info($"Game speed applied: {gameSpeed:0.##}x");
            }
            else if (logChanges)
            {
                Logger.Info($"Game speed: {gameSpeed:0.##}x");
            }
        }
        else
        {
            RestoreGameSpeed();
            if (logChanges)
                Logger.Info("Game speed override disabled");
        }

        if (Application.targetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
            Logger.Info($"Target frame rate applied: {targetFrameRate}");
        }
        else if (logChanges)
        {
            Logger.Info($"Target frame rate: {targetFrameRate}");
        }
    }

    private static void RestoreGameSpeed()
    {
        if (!_gameSpeedApplied)
            return;

        Time.timeScale = _originalGameSpeed;
        _gameSpeedApplied = false;
        Logger.Info($"Game speed restored: {_originalGameSpeed:0.##}x");
    }

    private static float NormalizeGameSpeed(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0.1f, 10.0f) : 1.0f;

    private static int NormalizeTargetFrameRate(int value) => Math.Clamp(value, 30, 240);

    private static float NormalizeZoomRatio(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0.1f, 5.0f) : 1.0f;
}
