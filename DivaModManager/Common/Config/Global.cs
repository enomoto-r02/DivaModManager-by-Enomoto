using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using DivaModManager.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DivaModManager;

public static class Global
{
    public static readonly string GAME_NAME = "Hatsune Miku: Project DIVA Mega Mix+";
    public static readonly string GAME_NAME_EXE = "DivaMegaMix.exe";
    public static readonly DateTime STARTED_DATETIME = DateTime.Now;

    public static readonly HttpClientHandler GBhandler = new HttpClientHandler { UseCookies = true };
    public static readonly HttpClient GBclient = new(GBhandler);
    public static readonly HttpClientHandler DMAhandler = new HttpClientHandler { UseCookies = true };
    public static readonly HttpClient DMAclient = new(DMAhandler);
    public static readonly HttpClientHandler GitHubhandler = new HttpClientHandler { UseCookies = true };
    public static readonly HttpClient GitHubclient = new(GitHubhandler);

    public static ConfigJson ConfigJson { get; set; } = new();
    public static ConfigTomlDmm ConfigToml { get; set; } = new();
    public static WindowLogger? WindowLogger { get; set; }
    public static readonly char s = Path.DirectorySeparatorChar;
    // Example : ".../DivaModManager/"
    public static string assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string textLogLocation = $@"{assemblyLocation}{Process.GetCurrentProcess().ProcessName}.log";
    public static readonly string textLogBackgroundLocation = $@"{Global.assemblyLocation}{Process.GetCurrentProcess().ProcessName}_Download.log";
    public static readonly string downloadBaseLocation = $@"{assemblyLocation}Downloads{s}";
    public static readonly string screenshotBaseLocation = $@"{assemblyLocation}ScreenShots{s}";
    public static readonly string temporaryWarningFilePath = $"{downloadBaseLocation}{s}DivaModManager uses this folder as a temporary folder, so do not place files under this folder";
    public static readonly string temporaryLocationDML = $"{downloadBaseLocation}DML{s}";
    public static readonly string temporaryLocationDMM = $"{downloadBaseLocation}DMM{s}";

    // GameBoxのTextBlockのテキスト値(ハードコーディング)
    // todo: この辺りはMainWindowが保持でよくない？
    public static List<string>? games;
    public static string? selected_game;
    public static bool SearchModListFlg;
    public static ObservableCollection<string>? LoadoutItems;
    public static ObservableCollection<string>? CategoryItems;
    public static string DMA_HOMEPAGE_URL_POSTS = "https://divamodarchive.com/posts/";
    public static string DMA_API_URL_POSTS = "https://divamodarchive.com/api/v1/posts/";
    public static string DMA_PAGE_URL_BASE = "https://divamodarchive.com/post/";

    public static readonly int BIT_OS = Environment.Is64BitOperatingSystem ? 64 : 32;
    public static readonly string BIT_OS_DIR_NAME = Environment.Is64BitOperatingSystem ? "x64" : "x86";
    public static bool IsMainWindowLoaded { get; set; } = false;
    public static bool IsModGridLoaded { get; set; } = false;

    public static string ModsFolder
    {
        get
        {
            var modsFolder = ConfigJson?.CurrentConfig?.ModsFolder ?? string.Empty;
            if (!string.IsNullOrEmpty(modsFolder)) { return modsFolder; }
            return Path.GetFullPath(modsFolder);
        }
        private set { _ModsFolder = value; }
    }
    private static string _ModsFolder { get; set; } = string.Empty;

    private static ObservableCollection<Mod>? _ModList { get; set; } = new();
    public static ObservableCollection<Mod> ModList
    {
        get => _ModList;
        set
        {
            // まとめて置き換えた場合はOnCollectionChangedを発火させない
            var notCollenctionChanged = (_ModList != null || _ModList.Count != 0 || !Global.IsModGridLoaded);
            if (notCollenctionChanged)
                _ModList.CollectionChanged -= default;
            _ModList = value;
            if (notCollenctionChanged)
                _ModList.CollectionChanged += OnCollectionChanged;
        }
    }
    private static ObservableCollection<Mod>? _ModList_All { get; set; } = new();
    public static ObservableCollection<Mod> ModList_All
    {
        get => _ModList_All;
        set
        {
            // まとめて置き換えた場合はOnCollectionChangedを発火させない
            var notCollenctionChanged = (_ModList_All != null || _ModList_All.Count != 0 || !Global.IsModGridLoaded);
            if (notCollenctionChanged)
                _ModList_All.CollectionChanged -= default;
            _ModList_All = value;
            if (notCollenctionChanged)
                _ModList_All.CollectionChanged += OnCollectionChanged;
        }
    }

    // たぶん効率悪い
    public static void ModListInitError()
    {
        foreach (var mod in ModList_All)
        {
            mod.Errors.Clear();
        }
        foreach (var mod in ModList)
        {
            mod.Errors.Clear();
        }
    }

    // 項目が追加または削除されたとき、またはリスト全体が更新されたとき
    // ドラッグによるソート時に呼ばれる
    private static async void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var test = e.NewItems as IList;
        var mod = test?[0] as Mod;
        if (mod == null) { return; }

        // 変更があったときに一度だけ遅延実行
        await ScheduleSettingsUpdateAsync(mod.enabled);
    }

    // 遅延更新はいったん見送る？
    private static int DelayMilliseconds = 0;
    private static CancellationTokenSource? _updateCts;

    // 設定ファイル更新を遅延実行し更新
    private static async Task<bool> ScheduleSettingsUpdateAsync(bool isDMLConfigUpdate)
    {
        var ret = false;

        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        try
        {
            // 他の変更がなければ設定ファイル更新
            await Task.Delay(DelayMilliseconds, token);
            if (!token.IsCancellationRequested)
            {
                ret = await ConfigUpdatesAsync(isDMLConfigUpdate);
            }
        }
        catch (TaskCanceledException) { }
        return ret;
    }

    /// <summary>
    /// 明示的にConfig.json(DMM)とconfig.toml(DML)を更新する
    /// </summary>
    /// <param name="isDMLConfigUpdate">DMLのconfig.tomlを更新するか</param>
    /// <param name="caller"></param>
    /// <returns>両方の設定ファイルの更新可否(AND)</returns>
    public static async Task<bool> ConfigUpdatesAsync(bool isDMLConfigUpdate = true, [CallerMemberName] string caller = "")
    {
        string MeInfo = Logger.GetMeInfo(new StackFrame());
        string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
        //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Developer, param: ParamInfo);

        var ret = false;
        if (Global.IsMainWindowLoaded && Global.IsModGridLoaded)
        {
            var retJson = await ConfigJson.UpdateConfigAsync();
            var retToml = true;
            if (isDMLConfigUpdate && Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                retToml = await ModLoader.BuildAsync();
            }
            ret = retJson && retToml;
            Logger.WriteLine(string.Join(" ", MeInfo, $"ConfigUpdatesAsync Complete. retJson:{retJson}, retToml:{retToml}"), LoggerType.Debug, param: ParamInfo);
        }
        else
        {
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}"), LoggerType.Developer, param: ParamInfo);
        }
        //Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Return:{ret}"), LoggerType.Developer, param: ParamInfo);
        return ret;
    }
}