using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Utility.Notifications;

namespace TSKHook;

public static class Config
{
    public static readonly string FilePath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        $"{ModInfo.Name}.cfg"
    );

    /// <summary>Gets the opt-in switch for overriding game speed.</summary>
    public static MelonPreferences_Entry<bool> GameSpeedEnabled { get; private set; }
    public static MelonPreferences_Entry<float> GameSpeed { get; private set; }
    public static MelonPreferences_Entry<int> TargetFrameRate { get; private set; }
    public static MelonPreferences_Entry<float> ZoomRatio { get; private set; }

    public static MelonPreferences_Entry<bool> TranslationEnabled { get; private set; }
    public static MelonPreferences_Entry<string> TranslationCdnUrl { get; private set; }
    public static MelonPreferences_Entry<string> TranslationLanguage { get; private set; }
    public static MelonPreferences_Entry<string> TranslationCacheDirectory { get; private set; }
    public static MelonPreferences_Entry<bool> TranslationPreferLocalFiles { get; private set; }
    public static MelonPreferences_Entry<string> FontBundlePath { get; private set; }
    public static MelonPreferences_Entry<string> LegacyFontAssetName { get; private set; }
    public static MelonPreferences_Entry<string> TmpFontAssetName { get; private set; }

    private static bool _initializing;
    private static bool _entriesBound;
    private static MelonPreferences_Category _preferenceCategory;
    private static readonly List<MelonPreferences_Category> PreferenceCategories = new();

    public static void Initialize()
    {
        _initializing = true;
        try
        {
            if (!_entriesBound)
            {
                BindAllEntries();
                _entriesBound = true;
            }

            _preferenceCategory.LoadFromFile(false);
            foreach (var category in PreferenceCategories)
                category.SaveToFile(false);
        }
        finally
        {
            _initializing = false;
        }
    }

    private static void BindAllEntries()
    {
        var general = CreateCategory("General");
        GameSpeedEnabled = CreateEntry(
            general,
            "GameSpeedEnabled",
            false,
            "是否修改游戏速度，默认关闭，修改后自动生效；关闭时恢复启用前的速度"
        );
        GameSpeed = CreateEntry(
            general,
            "GameSpeed",
            1.0f,
            "游戏速度倍率，允许范围 0.1 到 10，开启 GameSpeedEnabled 后生效"
        );
        TargetFrameRate = CreateEntry(
            general,
            "TargetFrameRate",
            60,
            "目标帧率，允许范围 30 到 240，修改后自动生效"
        );
        ZoomRatio = CreateEntry(general, "ZoomRatio", 1.0f, "图鉴角色缩放倍率，允许范围 0.1 到 5");

        var translation = CreateCategory("Translation");
        TranslationEnabled = CreateEntry(
            translation,
            "Enable",
            true,
            "是否开启繁体中文翻译；修改后重启生效"
        );
        TranslationCdnUrl = CreateEntry(
            translation,
            "CDN",
            "https://translation.lolida.best/download/tsk",
            "翻译内容的 CDN；修改后重启生效"
        );
        TranslationLanguage = CreateEntry(
            translation,
            "Language",
            "zh_Hant",
            "翻译语言，目前支持 zh_Hant；修改后重启生效"
        );

        var cache = CreateCategory("Translation.Cache");
        TranslationCacheDirectory = CreateEntry(
            cache,
            "Directory",
            $"{ModInfo.Name}/translations",
            "翻译缓存目录，默认相对于 UserData，也可使用绝对路径；修改后重启生效"
        );
        TranslationPreferLocalFiles = CreateEntry(
            cache,
            "PreferLocalFiles",
            false,
            "本地翻译存在时是否跳过下载并优先使用本地文件；修改后重启生效"
        );

        var font = CreateCategory("Translation.Font");
        FontBundlePath = CreateEntry(
            font,
            "AssetBundlePath",
            $"{ModInfo.Name}/notosanscjktc",
            $"字体 AssetBundle 路径，默认取 MelonLoader/UserData/{ModInfo.Name}/notosanscjktc；修改后重启生效"
        );
        LegacyFontAssetName = CreateEntry(
            font,
            "LegacyAssetName",
            "notosanscjktc",
            "Utage 旧字体资源名称；修改后重启生效"
        );
        TmpFontAssetName = CreateEntry(
            font,
            "TmpAssetName",
            "notosanscjktc SDF",
            "TextMeshPro 字体资源名称；修改后重启生效"
        );
    }

    private static MelonPreferences_Category CreateCategory(string name)
    {
        var category = MelonPreferences.CreateCategory(name);
        category.SetFilePath(FilePath, false, false);
        _preferenceCategory ??= category;
        PreferenceCategories.Add(category);
        return category;
    }

    private static MelonPreferences_Entry<T> CreateEntry<T>(
        MelonPreferences_Category category,
        string key,
        T defaultValue,
        string description
    )
    {
        var entry = category.CreateEntry(key, defaultValue, description, description);
        entry.OnEntryValueChanged.Subscribe(
            (_, newValue) =>
            {
                if (_initializing)
                    return;

                category.SaveToFile(false);
                Logger.Info($"[{category.Identifier}] {key} => {newValue}");
                Toast.Info($"[{category.Identifier}]", $"{key} => {newValue}");
            }
        );
        return entry;
    }
}
