using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using Utility.Assets;
using Utility.Caching;
using Utility.Notifications;

namespace TSKHook.Services;

public static class Translation
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;

    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> Chapters =
        new();
    private static readonly ConcurrentDictionary<string, Lazy<Task>> PendingChapterLoads = new();
    private static JsonResourceCache _resources;
    private static readonly object NamesLoadLock = new();

    private static Dictionary<string, string> _names = new();
    private static Task _namesLoadTask;
    private static volatile bool _namesLoaded;
    private static volatile bool _shutdown;
    private static AssetBundleLoader<Font> _legacyFontLoader;
    private static AssetBundleLoader<TMP_FontAsset> _tmpFontLoader;
    private static object _fontLoadCoroutine;
    private static HttpClient _client;
    private static CancellationTokenSource _shutdownTokenSource;
    private static string _cacheDirectory;
    private static string _cdn;
    private static string _language;

    public static Font LegacyFont => _legacyFontLoader?.Asset;

    public static TMP_FontAsset TmpFontAsset => _tmpFontLoader?.Asset;

    public static void Initialize()
    {
        _shutdown = false;
        _cdn = Config.TranslationCdnUrl.Value.TrimEnd('/');
        _language = ValidateLanguage(Config.TranslationLanguage.Value);
        _cacheDirectory = ResolveUserDataPath(Config.TranslationCacheDirectory.Value);
        _client = CreateHttpClient();
        _resources = new JsonResourceCache(
            _client,
            message => Logger.Info(message),
            message => Logger.Warn(message),
            downloadError: exception =>
            {
                if (!_shutdown)
                    Toast.Error("网络错误", exception.Message);
            }
        );
        _shutdownTokenSource = new CancellationTokenSource();

        string fontBundlePath = ResolveFontBundlePath(Config.FontBundlePath.Value);
        _legacyFontLoader = new AssetBundleLoader<Font>(
            fontBundlePath,
            Config.LegacyFontAssetName.Value
        );
        _tmpFontLoader = new AssetBundleLoader<TMP_FontAsset>(
            fontBundlePath,
            Config.TmpFontAssetName.Value
        );

        Logger.Info($"Translation cache directory: {_cacheDirectory}");
        if (!Config.TranslationEnabled.Value)
            return;

        StartFontLoad();
        _ = EnsureNamesLoadedAsync();
    }

    public static void Shutdown()
    {
        _shutdown = true;
        _shutdownTokenSource?.Cancel();

        if (_fontLoadCoroutine != null)
        {
            MelonCoroutines.Stop(_fontLoadCoroutine);
            _fontLoadCoroutine = null;
        }

        _client?.Dispose();
        _shutdownTokenSource?.Dispose();
        _client = null;
        _shutdownTokenSource = null;
        _legacyFontLoader = null;
        _tmpFontLoader = null;

        PendingChapterLoads.Clear();
        Chapters.Clear();
        _resources = null;

        lock (NamesLoadLock)
        {
            _names = new Dictionary<string, string>();
            _namesLoadTask = null;
            _namesLoaded = false;
        }
    }

    public static async Task GetChapterTranslationAsync(string chapterId)
    {
        if (
            _shutdown
            || !Config.TranslationEnabled.Value
            || !TryNormalizeChapterLabel(chapterId, out string normalizedChapterId)
            || Chapters.ContainsKey(normalizedChapterId)
        )
            return;

        var cancellationTokenSource = _shutdownTokenSource;
        if (cancellationTokenSource == null)
            return;

        var pendingLoad = PendingChapterLoads.GetOrAdd(
            normalizedChapterId,
            id => new Lazy<Task>(
                () => LoadChapterAsync(id, cancellationTokenSource.Token),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        try
        {
            await Task.WhenAll(pendingLoad.Value, EnsureNamesLoadedAsync()).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        { }
        catch (Exception exception)
        {
            Logger.Error($"Chapter translation load failed [{normalizedChapterId}]: {exception}");
            if (!_shutdown)
                Toast.Error("翻译加载失败", normalizedChapterId);
        }
        finally
        {
            PendingChapterLoads.TryRemove(normalizedChapterId, out _);
        }
    }

    public static bool TryGetChapterTranslation(
        string chapterId,
        string source,
        out string translated
    )
    {
        translated = null;
        return TryNormalizeChapterLabel(chapterId, out string normalizedChapterId)
            && Chapters.TryGetValue(normalizedChapterId, out var translations)
            && translations.TryGetValue(source, out translated)
            && !string.IsNullOrEmpty(translated);
    }

    public static bool TryGetNameTranslation(string source, out string translated)
    {
        translated = null;
        return !string.IsNullOrEmpty(source)
            && Volatile.Read(ref _names).TryGetValue(source, out translated)
            && !string.IsNullOrEmpty(translated);
    }

    public static bool TryNormalizeChapterLabel(string label, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        string candidate = label.Trim().ToLowerInvariant();
        foreach (char character in candidate)
        {
            if (
                !((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
                && character != '-'
                && character != '_'
            )
                return false;
        }

        normalized = candidate;
        return true;
    }

    private static Task EnsureNamesLoadedAsync()
    {
        if (_shutdown || !Config.TranslationEnabled.Value)
            return Task.CompletedTask;

        var cancellationTokenSource = _shutdownTokenSource;
        if (cancellationTokenSource == null)
            return Task.CompletedTask;

        lock (NamesLoadLock)
        {
            if (
                _namesLoadTask == null
                || _namesLoadTask.IsCanceled
                || _namesLoadTask.IsFaulted
                || (_namesLoadTask.IsCompleted && !_namesLoaded)
            )
                _namesLoadTask = LoadNamesSafelyAsync(cancellationTokenSource.Token);

            return _namesLoadTask;
        }
    }

    private static async Task LoadChapterAsync(
        string chapterId,
        CancellationToken cancellationToken
    )
    {
        var translations = await LoadResourceAsync<Dictionary<string, string>>(
                $"chapters/{chapterId}/{_language}.json",
                $"{chapterId}/{_language}/?format=json",
                cancellationToken
            )
            .ConfigureAwait(false);

        if (translations != null)
        {
            Chapters[chapterId] = translations;
            Logger.Info($"Chapter translation loaded [{chapterId}]. Total: {translations.Count}");
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            Chapters.TryAdd(chapterId, new Dictionary<string, string>());
            Logger.Warn($"Chapter translation unavailable: {chapterId}");
            Toast.Warning("翻译不可用", chapterId);
        }
    }

    private static async Task LoadNamesAsync(CancellationToken cancellationToken)
    {
        var mainNamesTask = LoadResourceAsync<Dictionary<string, string>>(
            $"names/tsk_name/{_language}.json",
            $"tsk_name/{_language}/?format=json",
            cancellationToken
        );
        var subNamesTask = LoadResourceAsync<Dictionary<string, string>>(
            $"names/tsk_subname/{_language}.json",
            $"tsk_subname/{_language}/?format=json",
            cancellationToken
        );

        await Task.WhenAll(mainNamesTask, subNamesTask).ConfigureAwait(false);
        var mainNames = await mainNamesTask.ConfigureAwait(false);
        var subNames = await subNamesTask.ConfigureAwait(false);

        if (mainNames == null && subNames == null)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Logger.Warn("Character name translations are unavailable");
                Toast.Warning("翻译不可用", "角色名称翻译加载失败");
            }
            return;
        }

        var names = new Dictionary<string, string>();
        if (mainNames != null)
        {
            foreach (var pair in mainNames)
                names[pair.Key] = pair.Value;
        }
        if (subNames != null)
        {
            foreach (var pair in subNames)
                names[pair.Key] = pair.Value;
        }

        Volatile.Write(ref _names, names);
        _namesLoaded = true;
        Logger.Info($"Character name translations loaded. Total: {names.Count}");
    }

    private static async Task LoadNamesSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadNamesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.Error($"Character names translation load failed: {exception}");
            if (!_shutdown)
                Toast.Error("翻译加载失败", "角色名称翻译加载异常");
        }
    }

    private static async Task<T> LoadResourceAsync<T>(
        string cacheRelativePath,
        string remoteRelativePath,
        CancellationToken cancellationToken
    )
        where T : class
    {
        string cachePath = Path.Combine(
            _cacheDirectory,
            cacheRelativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        var resources = _resources;
        if (resources == null)
            return null;
        return await resources
            .RefreshAsync<T>(
                cacheRelativePath,
                cachePath,
                $"{_cdn}/{remoteRelativePath}",
                Config.TranslationPreferLocalFiles.Value
                    ? JsonCachePolicy.PreferLocal
                    : JsonCachePolicy.Refresh,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static void StartFontLoad()
    {
        if (
            _shutdown
            || !Config.TranslationEnabled.Value
            || _fontLoadCoroutine != null
            || (_legacyFontLoader?.IsLoaded == true && _tmpFontLoader?.IsLoaded == true)
        )
            return;

        if (_legacyFontLoader?.IsLoaded != true)
        {
            var loader = _legacyFontLoader;
            _fontLoadCoroutine = MelonCoroutines.Start(
                loader.Load(
                    () =>
                    {
                        _fontLoadCoroutine = null;
                        if (!_shutdown)
                        {
                            Logger.Info($"Legacy font loaded: {loader.Asset.name}");
                            StartFontLoad();
                        }
                    },
                    exception =>
                    {
                        _fontLoadCoroutine = null;
                        if (!_shutdown)
                        {
                            Logger.Warn($"Legacy font load failed: {exception.Message}");
                            StartTmpFontLoad();
                        }
                    }
                )
            );
            return;
        }

        StartTmpFontLoad();
    }

    private static void StartTmpFontLoad()
    {
        if (
            _shutdown
            || _fontLoadCoroutine != null
            || _tmpFontLoader == null
            || _tmpFontLoader.IsLoading
            || _tmpFontLoader.IsLoaded
        )
            return;

        var loader = _tmpFontLoader;
        _fontLoadCoroutine = MelonCoroutines.Start(
            loader.Load(
                () =>
                {
                    _fontLoadCoroutine = null;
                    if (!_shutdown)
                    {
                        Logger.Info($"TMP font loaded: {loader.Asset.name}");
                    }
                },
                exception =>
                {
                    _fontLoadCoroutine = null;
                    if (!_shutdown)
                        Toast.Error("字体加载失败", exception.Message);
                }
            )
        );
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ModInfo.Name}/{ModInfo.Version}");
        return client;
    }

    private static string ResolveFontBundlePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Configured font path cannot be empty", nameof(path));

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        string userDataPath = Path.GetFullPath(
            Path.Combine(MelonEnvironment.UserDataDirectory, path)
        );
        if (File.Exists(userDataPath))
            return userDataPath;

        string legacyModsPath = Path.GetFullPath(
            Path.Combine(MelonEnvironment.ModsDirectory, path)
        );
        if (File.Exists(legacyModsPath))
        {
            Logger.Info($"Using legacy Mods font bundle: {legacyModsPath}");
            return legacyModsPath;
        }

        return userDataPath;
    }

    private static string ResolveUserDataPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Configured path cannot be empty", nameof(path));

        return Path.GetFullPath(
            Path.IsPathRooted(path) ? path : Path.Combine(MelonEnvironment.UserDataDirectory, path)
        );
    }

    private static string ValidateLanguage(string language)
    {
        if (
            string.IsNullOrWhiteSpace(language)
            || language.Contains('/')
            || language.Contains('\\')
        )
            throw new ArgumentException(
                "Configured translation language is invalid",
                nameof(language)
            );

        return language.Trim();
    }
}
