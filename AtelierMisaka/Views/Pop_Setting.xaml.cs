using AtelierMisaka.Models;
using AtelierMisaka.Utils;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AtelierMisaka.Views
{
    /// <summary>
    /// Pop_Setting.xaml 的交互逻辑
    /// </summary>
    public partial class Pop_Setting : UserControl
    {
        private bool _isOverDropBtn = false;
        private BaseUtils _utils = null;
        private Storyboard _sbLoad = null;

        private ArtistInfo _tempArt = null;
        private SiteType _tempSite = SiteType.Fanbox;
        private string _tempProxy = string.Empty;
        private string _tempID = string.Empty;
        private string _tempDate = string.Empty;
        private string _tempDate_end = string.Empty;
        private string _tempSP = string.Empty;
        private bool _tempUP = false;
        private bool _tempUD = false;
        private bool _tempFantiaPollingMode = false;
        private bool _tempXNotificationMode = false;
        private bool _tempXHistorySearchMode = false;
        private bool _tempXIncludeRetweetMedia = false;
        private bool _tempXIncludeQuoteMedia = false;

        private bool _second = false;
        private bool _selectF = true;
        private CancellationTokenSource _fantiaPollCts = null;
        private Task _fantiaPollTask = null;
        private readonly SemaphoreSlim _fantiaRoundGate = new SemaphoreSlim(1, 1);
        private const int _fantiaPollIntervalMinSeconds = 20;
        private const int _fantiaPollIntervalMaxSeconds = 30;
        private readonly Random _fantiaPollRandom = new Random();
        private readonly object _fantiaPollRandomLock = new object();
        private CancellationTokenSource _xPollCts = null;
        private Task _xPollTask = null;
        private readonly SemaphoreSlim _xRoundGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _backgroundRoundGate = new SemaphoreSlim(1, 1);
        private const int _xPollIntervalMinSeconds = 45;
        private const int _xPollIntervalMaxSeconds = 75;
        private readonly Random _xPollRandom = new Random();
        private readonly object _xPollRandomLock = new object();
        private PollingRunContext _fantiaPollingContext = null;
        private PollingRunContext _xPollingContext = null;

        private IList<BaseItem> _tempBis = null;
        private IList<BaseItem> _fantiaTempBis = null;
        private IList<BaseItem> _xTempBis = null;

        private class PollingRunContext
        {
            public SiteType Site { get; set; }
            public ArtistInfo Artist { get; set; }
            public string IDName { get; set; }
            public string SavePath { get; set; }
            public string Proxy { get; set; }
            public bool UseProxy { get; set; }
            public bool UseDate { get; set; }
            public string DateStart { get; set; }
            public string DateEnd { get; set; }
            public DateTime StartTime { get; set; }
            public bool FantiaPollingMode { get; set; }
            public bool XNotificationMode { get; set; }
            public bool XHistorySearchMode { get; set; }
            public bool XIncludeRetweetMedia { get; set; }
            public bool XIncludeQuoteMedia { get; set; }
        }

        private class DownloaderRunContext
        {
            public string TempAI { get; set; }
            public string TempAN { get; set; }
            public SiteType TempSite { get; set; }
            public string SavePath { get; set; }
            public bool IsFantia { get; set; }
        }

        public Pop_Setting()
        {
            InitializeComponent();
            _sbLoad = (Storyboard)TryFindResource("sb_path");
            LoadSetting();
        }

        private static ArtistInfo CloneArtist(ArtistInfo artist)
        {
            if (artist == null)
            {
                return new ArtistInfo();
            }
            return new ArtistInfo
            {
                Id = artist.Id,
                Cid = artist.Cid,
                AName = artist.AName,
                PayLow = artist.PayLow,
                PayHigh = artist.PayHigh,
                PostUrl = artist.PostUrl,
                Twitter = artist.Twitter
            };
        }

        private PollingRunContext CapturePollingContext()
        {
            return new PollingRunContext
            {
                Site = GlobalData.VM_MA.Site,
                Artist = CloneArtist(GlobalData.VM_MA.Artist),
                IDName = GlobalData.VM_MA.IDName,
                SavePath = GlobalData.VM_MA.SavePath,
                Proxy = GlobalData.VM_MA.Proxy,
                UseProxy = GlobalData.VM_MA.UseProxy,
                UseDate = GlobalData.VM_MA.UseDate,
                DateStart = GlobalData.VM_MA.Date_Start,
                DateEnd = GlobalData.VM_MA.Date_End,
                StartTime = GlobalData.StartTime,
                FantiaPollingMode = GlobalData.VM_MA.FantiaPollingMode,
                XNotificationMode = GlobalData.VM_MA.XNotificationMode,
                XHistorySearchMode = GlobalData.VM_MA.XHistorySearchMode,
                XIncludeRetweetMedia = GlobalData.VM_MA.XIncludeRetweetMedia,
                XIncludeQuoteMedia = GlobalData.VM_MA.XIncludeQuoteMedia
            };
        }

        private void ApplyPollingContext(PollingRunContext context)
        {
            if (context == null)
            {
                return;
            }
            GlobalData.VM_MA.Site = context.Site;
            GlobalData.VM_MA.Artist = CloneArtist(context.Artist);
            GlobalData.VM_MA.IDName = context.IDName;
            GlobalData.VM_MA.SavePath = context.SavePath;
            GlobalData.VM_MA.Proxy = context.Proxy;
            GlobalData.VM_MA.UseProxy = context.UseProxy;
            GlobalData.VM_MA.UseDate = context.UseDate;
            GlobalData.VM_MA.Date_Start = context.DateStart;
            GlobalData.VM_MA.Date_End = context.DateEnd;
            GlobalData.StartTime = context.StartTime;
            if (context.Site == SiteType.Fantia)
            {
                GlobalData.VM_MA.FantiaPollingMode = context.FantiaPollingMode;
            }
            else if (context.Site == SiteType.X)
            {
                GlobalData.VM_MA.XHistorySearchMode = context.XHistorySearchMode;
                GlobalData.VM_MA.XNotificationMode = context.XNotificationMode;
                GlobalData.VM_MA.XIncludeRetweetMedia = context.XIncludeRetweetMedia;
                GlobalData.VM_MA.XIncludeQuoteMedia = context.XIncludeQuoteMedia;
            }
        }

        private void PrepareDownloaderForContext(PollingRunContext context)
        {
            if (context == null || GlobalData.VM_DL == null)
            {
                return;
            }
            GlobalData.VM_DL.TempAI = context.Artist?.Id ?? string.Empty;
            GlobalData.VM_DL.TempAN = context.Artist?.AName ?? string.Empty;
            GlobalData.VM_DL.TempSite = context.Site;
            GlobalData.VM_DL.SavePath = context.SavePath;
            GlobalData.VM_DL.IsFantia = context.Site == SiteType.Fantia;
        }

        private DownloaderRunContext CaptureDownloaderContext()
        {
            if (GlobalData.VM_DL == null)
            {
                return null;
            }
            return new DownloaderRunContext
            {
                TempAI = GlobalData.VM_DL.TempAI,
                TempAN = GlobalData.VM_DL.TempAN,
                TempSite = GlobalData.VM_DL.TempSite,
                SavePath = GlobalData.VM_DL.SavePath,
                IsFantia = GlobalData.VM_DL.IsFantia
            };
        }

        private void ApplyDownloaderContext(DownloaderRunContext context)
        {
            if (context == null || GlobalData.VM_DL == null)
            {
                return;
            }
            GlobalData.VM_DL.TempAI = context.TempAI;
            GlobalData.VM_DL.TempAN = context.TempAN;
            GlobalData.VM_DL.TempSite = context.TempSite;
            GlobalData.VM_DL.SavePath = context.SavePath;
            GlobalData.VM_DL.IsFantia = context.IsFantia;
        }

        private async Task RunWithPollingContextAsync(PollingRunContext context, CancellationToken token, Func<Task> action)
        {
            await _backgroundRoundGate.WaitAsync(token);
            var restoreContext = CapturePollingContext();
            var restoreDownloaderContext = CaptureDownloaderContext();
            try
            {
                ApplyPollingContext(context);
                await SetBrowserProxy();
                PrepareDownloaderForContext(context);
                if (context?.Artist != null)
                {
                    await Task.Run(() => GlobalData.DLLogs.LoadData(context.Artist.Id, context.Site), token);
                }
                await action();
            }
            finally
            {
                try
                {
                    ApplyPollingContext(restoreContext);
                    ApplyDownloaderContext(restoreDownloaderContext);
                    await SetBrowserProxy();
                }
                finally
                {
                    _backgroundRoundGate.Release();
                }
            }
        }

        private async void Btn_Star_Click(object sender, RoutedEventArgs e)
        {
            if (_second) return;
            _second = true;
            bool beginAttempted = false;
            bool beginSucceeded = false;

            if (await GetCheck(GlobalData.VM_MA.IsStarted ? GlobalLanguage.Msg_SecondConf : GlobalLanguage.Msg_StartConf))
            {
                if (GlobalData.CheckResult == false)
                {
                    _second = false;
                    return;
                }
            }

            bool fantiaPollingWillEnable = false;
            if (GlobalData.VM_MA.Site == SiteType.Fantia)
            {
                fantiaPollingWillEnable = !GlobalData.VM_MA.FantiaPollingMode && GlobalData.VM_MA.FantiaPollingModeDraft;
                GlobalData.VM_MA.ApplyFantiaRunModeDraft();
                if (fantiaPollingWillEnable)
                {
                    GlobalData.ForceFantiaPollingSeed = true;
                    GlobalMethord.ClearFantiaPollingPendingCursor();
                }
            }
            bool xNotificationWillEnable = false;
            if (GlobalData.VM_MA.Site == SiteType.X)
            {
                xNotificationWillEnable = !GlobalData.VM_MA.XNotificationMode && GlobalData.VM_MA.XNotificationModeDraft;
                GlobalData.VM_MA.ApplyXRunModeDraft();
                if (xNotificationWillEnable)
                {
                    GlobalData.ForceXNotificationSeed = true;
                    GlobalMethord.ClearXSeenPendingCursor();
                }
            }

            GlobalData.StartTime = DateTime.Now;
            if (string.IsNullOrEmpty(GlobalData.VM_MA.Date_End))
            {
                GlobalData.VM_MA.Date_End = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                //GlobalData.VM_MA.LastDate_End = DateTime.Now;
            }
            await SetBrowserProxy();

            if (!GlobalData.VM_MA.IsStarted)
            {
                ShowLoading(true);
                await Task.Delay(200);
                beginAttempted = true;
                if (await Begin())
                {
                    UpdateRunSnapshot();
                    GlobalData.VM_MA.IsStarted = true;
                    beginSucceeded = true;
                }
                ShowLoading(false);
            }
            else
            {
                ShowLoading(true);
                if (_tempSite != GlobalData.VM_MA.Site || _tempArt.PostUrl != GlobalData.VM_MA.Artist.PostUrl)
                {
                    if (GlobalData.VM_DL.IsDownloading)
                    {
                        await GetCheck(GlobalLanguage.Msg_IsDownload);
                        ShowLoading(false);
                        _second = false;
                        return;
                    }

                    beginAttempted = true;
                    if (await Begin())
                    {
                        UpdateRunSnapshot();
                        beginSucceeded = true;
                    }
                    ShowLoading(false);
                    if (beginSucceeded)
                    {
                        await EnsureFantiaPollingAsync();
                        await EnsureXPollingAsync();
                    }
                    _second = false;
                    return;
                }

                if (_tempArt.PayLow != GlobalData.VM_MA.Artist.PayLow || _tempArt.PayHigh != GlobalData.VM_MA.Artist.PayHigh)
                {
                    if (GlobalData.VM_MA.Site == SiteType.Fanbox)
                    {
                        await Task.Run(() =>
                        {
                            foreach (var bi in _tempBis)
                            {
                                bi.Skip = GlobalMethord.OverPayment(int.Parse(bi.Fee));
                            }
                        });
                        GlobalData.VM_MA.ItemList = _tempBis.Where(x => !x.Skip).ToList();
                        _tempArt.PayLow = GlobalData.VM_MA.Artist.PayLow;
                        _tempArt.PayHigh = GlobalData.VM_MA.Artist.PayHigh;
                    }
                }

                if (_tempSP != GlobalData.VM_MA.SavePath)
                {
                    await GetCheck(GlobalLanguage.Msg_ChangeSP);
                    _tempSP = GlobalData.VM_MA.SavePath;
                }

                if (_tempProxy != GlobalData.VM_MA.Proxy)
                {
                    GlobalData.VM_DL.DLClients.ForEach(x =>
                    {
                        GlobalData.VM_DL.ReStartCommand.Execute(x);
                    });
                    await SetBrowserProxy();
                    _tempProxy = GlobalData.VM_MA.Proxy;
                }

                if (_tempUP != GlobalData.VM_MA.UseProxy)
                {
                    GlobalData.VM_DL.DLClients.ForEach(x =>
                    {
                        GlobalData.VM_DL.ReStartCommand.Execute(x);
                    });
                    await SetBrowserProxy();
                    _tempUP = GlobalData.VM_MA.UseProxy;
                }

                if (_tempID != GlobalData.VM_MA.IDName)
                {
                    GlobalData.VM_DL.DLClients.ForEach(x =>
                    {
                        GlobalData.VM_DL.ReStartCommand.Execute(x);
                    });
                    _tempID = GlobalData.VM_MA.IDName;
                    GlobalData.VM_MA.NeedCookie = true;
                }

                if (_tempDate != GlobalData.VM_MA.Date_Start || _tempDate_end != GlobalData.VM_MA.Date_End || _tempUD != GlobalData.VM_MA.UseDate)
                {
                    //if (GlobalData.VM_MA.Site == SiteType.Fanbox || CompareDate(GlobalData.VM_MA.Date_Start, _tempDate) || CompareDate(GlobalData.VM_MA.Date_End, _tempDate_end))
                    //{
                    //    await Task.Run(() =>
                    //    {
                    //        foreach (var bi in _tempBis)
                    //        {
                    //            bi.Skip = GlobalMethord.OverTime(bi.UpdateDate);
                    //        }
                    //    });
                    //    GlobalData.VM_MA.ItemList = _tempBis.Where(x => !x.Skip).ToList();
                    //    _tempDate = GlobalData.VM_MA.Date_Start;
                    //    _tempDate_end = GlobalData.VM_MA.Date_End;
                    //    _tempUD = GlobalData.VM_MA.UseDate;
                    //}
                    //else
                    {
                        if (GlobalData.VM_DL.IsDownloading)
                        {
                            await GetCheck(GlobalLanguage.Msg_IsDownload);
                            ShowLoading(false);
                            _second = false;
                            return;
                        }

                        beginAttempted = true;
                        if (await Begin())
                        {
                            UpdateRunSnapshot();
                            beginSucceeded = true;
                        }
                        ShowLoading(false);
                        if (beginSucceeded)
                        {
                            await EnsureFantiaPollingAsync();
                            await EnsureXPollingAsync();
                        }
                        _second = false;
                        return;
                    }
                }
                ShowLoading(false);
                GlobalCommand.BackCommand.Execute(BackType.Pop);
            }
            if (beginAttempted)
            {
                if (beginSucceeded)
                {
                    await EnsureFantiaPollingAsync();
                    await EnsureXPollingAsync();
                }
            }
            else if (GlobalData.VM_MA.IsStarted)
            {
                await EnsureFantiaPollingAsync();
                await EnsureXPollingAsync();
                if (_tempFantiaPollingMode != GlobalData.VM_MA.FantiaPollingMode)
                {
                    _tempFantiaPollingMode = GlobalData.VM_MA.FantiaPollingMode;
                    _ = Task.Run(() => SaveSetting());
                }
                if (_tempXNotificationMode != GlobalData.VM_MA.XNotificationMode ||
                    _tempXHistorySearchMode != GlobalData.VM_MA.XHistorySearchMode ||
                    _tempXIncludeRetweetMedia != GlobalData.VM_MA.XIncludeRetweetMedia ||
                    _tempXIncludeQuoteMedia != GlobalData.VM_MA.XIncludeQuoteMedia)
                {
                    _tempXNotificationMode = GlobalData.VM_MA.XNotificationMode;
                    _tempXHistorySearchMode = GlobalData.VM_MA.XHistorySearchMode;
                    _tempXIncludeRetweetMedia = GlobalData.VM_MA.XIncludeRetweetMedia;
                    _tempXIncludeQuoteMedia = GlobalData.VM_MA.XIncludeQuoteMedia;
                    _ = Task.Run(() => SaveSetting());
                }
            }
            _second = false;
        }

        private async Task<bool> Begin()
        {
            await _backgroundRoundGate.WaitAsync();
            try
            {
                return await BeginCore();
            }
            finally
            {
                _backgroundRoundGate.Release();
            }
        }

        private async Task<bool> BeginCore()
        {
            if (GlobalData.VM_MA.Site == SiteType.Fantia)
            {
                await StopFantiaPollingAsync();
                _fantiaPollingContext = null;
            }
            else if (GlobalData.VM_MA.Site == SiteType.X)
            {
                await StopXPollingAsync();
                _xPollingContext = null;
            }
            if (string.IsNullOrEmpty(GlobalData.VM_MA.IDName))
            {
                await GetCheck(GlobalLanguage.Msg_CheckCk);
                return false;
            }

            if (string.IsNullOrEmpty(GlobalData.VM_MA.SavePath))
            {
                await GetCheck(GlobalLanguage.Msg_CheckSP);
                return false;
            }
            Directory.CreateDirectory(GlobalData.VM_MA.SavePath);
            if (!Directory.Exists(GlobalData.VM_MA.SavePath))
            {
                await GetCheck(GlobalLanguage.Msg_CreateSP);
                return false;
            }
            if (GlobalData.VM_MA.UseProxy)
            {
                GlobalMethord.GetSystemProxy();
            }

            if (null != GlobalData.DownLP)
            {
                GlobalData.DownLP.Reset();
            }
            GlobalData.VM_MA.PostCount = 0;
            GlobalData.VM_MA.PostTitle = string.Empty;
            ResultMessage _ret = null;
            _utils = GlobalData.CaptureUtil;
            if (GlobalData.VM_MA.NeedCookie)
            {
                _ret = await _utils.CheckCookies();
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    ShowLoading(false);
                    return false;
                }
                if (_ret.Result == null)
                {
                    await GetCheck(GlobalLanguage.Msg_NeedLogin);
                    ShowLoading(false);
                    GlobalCommand.OpBrowserCommand.Execute(null);
                    return false;
                }
                var flag = (bool)_ret.Result;
                if (!flag)
                {
                    await GetCheck(GlobalLanguage.Msg_ChangeAccount);
                    ShowLoading(false);
                    GlobalCommand.OpBrowserCommand.Execute(null);
                    return false;
                }
                GlobalData.VM_MA.NeedCookie = false;
            }
            _selectF = GlobalData.VM_MA.HasSelected;
            if (GlobalData.VM_MA.Site == SiteType.X && GlobalData.VM_MA.XNotificationMode && !_selectF)
            {
                var screenName = GlobalData.VM_MA.IDName.Trim().TrimStart('@');
                var ai = new ArtistInfo()
                {
                    Id = screenName,
                    Cid = screenName,
                    AName = GlobalMethord.RemoveLastDot(GlobalMethord.ReplacePath(screenName)),
                    PostUrl = $"https://x.com/{screenName}"
                };
                GlobalData.VM_MA.Artist = ai;
                if (!GlobalData.VM_MA.ArtistList.Contains(GlobalData.VM_MA.Artist))
                {
                    if (GlobalData.VM_MA.ArtistList.Count > 0)
                    {
                        GlobalData.VM_MA.ArtistList[GlobalData.VM_MA.ArtistList.Count - 1] = GlobalData.VM_MA.Artist;
                    }
                    else
                    {
                        GlobalData.VM_MA.ArtistList.Add(GlobalData.VM_MA.Artist);
                    }
                }
                _selectF = true;
            }
            if (!_selectF)
            {
                ArtistInfo ai = null;
                _ret = await _utils.GetArtistInfo(GlobalData.VM_MA.Artist.PostUrl);
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    return false;
                }
                ai = (ArtistInfo)_ret.Result;
                GlobalData.VM_MA.Artist = ai;
                if (!GlobalData.VM_MA.ArtistList.Contains(GlobalData.VM_MA.Artist))
                {
                    if (GlobalData.VM_MA.ArtistList.Count > 0)
                    {
                        GlobalData.VM_MA.ArtistList[GlobalData.VM_MA.ArtistList.Count - 1] = GlobalData.VM_MA.Artist;
                    }
                    else
                    {
                        GlobalData.VM_MA.ArtistList.Add(GlobalData.VM_MA.Artist);
                    }
                }
            }
            _ = Task.Run(() => SaveSetting());
            await Task.Run(() => GlobalData.DLLogs.LoadData(GlobalData.VM_MA.Artist.Id, GlobalData.VM_MA.Site));

            if (GlobalData.VM_MA.Site == SiteType.Fantia)
            {
                GlobalData.VM_MA.UseDate = true;
                await GlobalData.DownLP.LoadData(null, true);
                _ret = await RunFantiaScanRoundAsync(GlobalData.VM_MA.FantiaPollingMode);
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    return false;
                }
                _tempBis = ToBaseItemList(_ret.Result);
                _fantiaTempBis = _tempBis;
                GlobalData.VM_MA.MainSection = MainSection.Downloads;
                GlobalCommand.BackCommand.Execute(BackType.Pop);
            }
            else if (GlobalData.VM_MA.Site == SiteType.X && GlobalData.VM_MA.XNotificationMode)
            {
                await GlobalData.DownLP.LoadData(null);
                _ret = await RunXNotificationRoundAsync();
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    return false;
                }
                _tempBis = ToBaseItemList(_ret.Result);
                _xTempBis = _tempBis;
                GlobalData.VM_MA.MainSection = MainSection.Downloads;
                GlobalCommand.BackCommand.Execute(BackType.Pop);
            }
            else
            {
                _ret = await _utils.GetPostIDs(GlobalData.VM_MA.Artist.Cid);
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    return false;
                }
                _tempBis = ToBaseItemList(_ret.Result);
                if (GlobalData.VM_MA.Site == SiteType.X)
                {
                    _xTempBis = _tempBis;
                }
                if (GlobalData.VM_MA.Site == SiteType.X && !GlobalData.VM_MA.XHistorySearchMode)
                {
                    UpdateXSeenCursor(XUtils.TimelineWindowSize);
                    GlobalMethord.CommitXSeenPendingCursor();
                }
                if (_tempBis.Count == 0)
                {
                    await GetCheck(GlobalLanguage.Msg_NoPosts);
                    return false;
                }
                GlobalData.VM_MA.ItemList = _tempBis.Where(x => !x.Skip).ToList();
                await GlobalData.DownLP.LoadData(_tempBis);
                GlobalData.VM_MA.MainSection = MainSection.Downloads;
                GlobalCommand.BackCommand.Execute(BackType.Pop);
            }
            return true;
        }

        private void UpdateRunSnapshot()
        {
            _tempArt = new ArtistInfo()
            {
                Id = GlobalData.VM_MA.Artist.Id,
                AName = GlobalData.VM_MA.Artist.AName,
                PayLow = GlobalData.VM_MA.Artist.PayLow,
                PayHigh = GlobalData.VM_MA.Artist.PayHigh,
                PostUrl = GlobalData.VM_MA.Artist.PostUrl,
            };
            _tempSite = GlobalData.VM_MA.Site;
            _tempID = GlobalData.VM_MA.IDName;
            _tempProxy = GlobalData.VM_MA.Proxy;
            _tempDate = GlobalData.VM_MA.Date_Start;
            _tempDate_end = GlobalData.VM_MA.Date_End;
            _tempSP = GlobalData.VM_MA.SavePath;
            _tempUP = GlobalData.VM_MA.UseProxy;
            _tempUD = GlobalData.VM_MA.UseDate;
            _tempFantiaPollingMode = GlobalData.VM_MA.FantiaPollingMode;
            _tempXNotificationMode = GlobalData.VM_MA.XNotificationMode;
            _tempXHistorySearchMode = GlobalData.VM_MA.XHistorySearchMode;
            _tempXIncludeRetweetMedia = GlobalData.VM_MA.XIncludeRetweetMedia;
            _tempXIncludeQuoteMedia = GlobalData.VM_MA.XIncludeQuoteMedia;
            GlobalData.CurrentSite = _tempSite;
        }

        private static List<BaseItem> ToBaseItemList(object result)
        {
            if (result == null)
            {
                return new List<BaseItem>();
            }
            if (result is List<BaseItem> baseItems)
            {
                return baseItems;
            }
            if (result is IEnumerable<BaseItem> items)
            {
                return items.ToList();
            }
            throw new InvalidCastException($"Unexpected item list type: {result.GetType().FullName}");
        }

        private void SetFantiaPollingState(FantiaPollingState state, DateTime? nextCheck = null, DateTime? lastCheck = null)
        {
            GlobalData.VM_MA.FantiaPollingStatus = state;
            if (nextCheck.HasValue || state == FantiaPollingState.Stopped)
            {
                GlobalData.VM_MA.FantiaPollingNextCheck = nextCheck;
            }
            if (lastCheck.HasValue)
            {
                GlobalData.VM_MA.FantiaPollingLastCheck = lastCheck;
            }
        }

        private TimeSpan GetFantiaPollInterval()
        {
            lock (_fantiaPollRandomLock)
            {
                return TimeSpan.FromSeconds(_fantiaPollRandom.Next(_fantiaPollIntervalMinSeconds, _fantiaPollIntervalMaxSeconds + 1));
            }
        }

        private async Task EnsureFantiaPollingAsync()
        {
            if (!GlobalData.VM_MA.IsStarted)
            {
                _fantiaPollingContext = null;
                await StopFantiaPollingAsync();
                return;
            }
            if (GlobalData.VM_MA.Site == SiteType.Fantia)
            {
                if (!GlobalData.VM_MA.FantiaPollingMode)
                {
                    _fantiaPollingContext = null;
                    await StopFantiaPollingAsync();
                    return;
                }
                _fantiaPollingContext = CapturePollingContext();
            }
            if (_fantiaPollingContext == null || !_fantiaPollingContext.FantiaPollingMode)
            {
                return;
            }
            if (_fantiaPollTask != null)
            {
                if (!_fantiaPollTask.IsCompleted)
                {
                    return;
                }
                await StopFantiaPollingAsync();
            }
            _fantiaPollCts = new CancellationTokenSource();
            _fantiaPollTask = FantiaPollLoopAsync(_fantiaPollCts.Token);
            SetFantiaPollingState(FantiaPollingState.Running, DateTime.Now.Add(GetFantiaPollInterval()));
        }

        private async Task StopFantiaPollingAsync()
        {
            if (_fantiaPollCts == null)
            {
                SetFantiaPollingState(FantiaPollingState.Stopped, null);
                return;
            }
            try
            {
                _fantiaPollCts.Cancel();
                if (_fantiaPollTask != null)
                {
                    await _fantiaPollTask;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
            }
            finally
            {
                _fantiaPollTask = null;
                _fantiaPollCts.Dispose();
                _fantiaPollCts = null;
                if (_fantiaPollingContext == null)
                {
                    _fantiaTempBis = null;
                }
                SetFantiaPollingState(FantiaPollingState.Stopped, null);
            }
        }

        private async Task FantiaPollLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && GlobalData.VM_MA.IsStarted && _fantiaPollingContext != null && _fantiaPollingContext.FantiaPollingMode)
                {
                    var context = _fantiaPollingContext;
                    var pollInterval = GetFantiaPollInterval();
                    SetFantiaPollingState(FantiaPollingState.Running, DateTime.Now.Add(pollInterval));
                    await Task.Delay(pollInterval, token);
                    if (token.IsCancellationRequested || !GlobalData.VM_MA.IsStarted || _fantiaPollingContext == null || !_fantiaPollingContext.FantiaPollingMode)
                    {
                        break;
                    }
                    if (GlobalData.VM_DL != null && GlobalData.VM_DL.HasPendingDownloads)
                    {
                        SetFantiaPollingState(FantiaPollingState.WaitingDownloads, DateTime.Now.Add(GetFantiaPollInterval()));
                        continue;
                    }
                    await _fantiaRoundGate.WaitAsync(token);
                    try
                    {
                        await RunWithPollingContextAsync(context, token, async () =>
                        {
                            if (GlobalData.VM_DL != null && GlobalData.VM_DL.HasPendingDownloads)
                            {
                                SetFantiaPollingState(FantiaPollingState.WaitingDownloads, DateTime.Now.Add(GetFantiaPollInterval()));
                                return;
                            }
                            if (!await EnsureFantiaPollReadyAsync())
                            {
                                SetFantiaPollingState(FantiaPollingState.Running, DateTime.Now.Add(GetFantiaPollInterval()));
                                return;
                            }
                            SetFantiaPollingState(FantiaPollingState.Running, null, DateTime.Now);
                            await RunFantiaScanRoundAsync();
                        });
                    }
                    finally
                    {
                        _fantiaRoundGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
            }
        }

        private async Task<bool> EnsureFantiaPollReadyAsync()
        {
            if (GlobalData.VM_MA.Site != SiteType.Fantia || !GlobalData.VM_MA.FantiaPollingMode)
            {
                return false;
            }
            _utils = GlobalData.CaptureUtil;
            if (GlobalData.VM_MA.NeedCookie)
            {
                var ret = await _utils.CheckCookies();
                if (ret.Error != ErrorType.NoError || ret.Result == null || !(bool)ret.Result)
                {
                    return false;
                }
                GlobalData.VM_MA.NeedCookie = false;
            }
            return true;
        }

        private async Task<ResultMessage> RunFantiaScanRoundAsync(bool refreshDateEnd = true)
        {
            _utils = GlobalData.CaptureUtil;
            GlobalData.VM_MA.UseDate = true;
            if (GlobalData.VM_MA.Site == SiteType.Fantia && GlobalData.VM_MA.FantiaPollingMode)
            {
                SetFantiaPollingState(FantiaPollingState.Running, null, DateTime.Now);
            }
            if (refreshDateEnd || string.IsNullOrEmpty(GlobalData.VM_MA.Date_End))
            {
                var scanEnd = DateTime.Now;
                GlobalData.StartTime = scanEnd;
                GlobalData.VM_MA.Date_End = scanEnd.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                GlobalData.StartTime = DateTime.Now;
            }
            GlobalData.VM_MA.PostCount = 0;
            GlobalData.VM_MA.PostTitle = string.Empty;

            var ret = await _utils.GetPostIDs(GlobalData.VM_MA.Artist.Cid);
            if (ret.Error != ErrorType.NoError)
            {
                return ret;
            }

            var bis = ToBaseItemList(ret.Result);
            MergeFantiaItems(bis);
            UpdateFantiaPollingCursor();
            if (GlobalData.VM_DL != null && !GlobalData.VM_DL.HasPendingDownloads)
            {
                if (!GlobalMethord.CommitFantiaPollingPendingCursor())
                {
                    AdvanceFantiaLastDate();
                }
            }
            return ret;
        }

        private void UpdateFantiaPollingCursor()
        {
            if (!GlobalData.VM_MA.FantiaPollingMode || string.IsNullOrEmpty(GlobalData.VM_MA.Artist?.Cid))
            {
                return;
            }

            var fantiaUtils = _utils as FantiaUtils ?? GlobalData.CaptureUtil as FantiaUtils;
            if (fantiaUtils == null || fantiaUtils.LastScanHeadPids == null || fantiaUtils.LastScanHeadPids.Count == 0)
            {
                return;
            }

            GlobalMethord.SetFantiaPollingPendingCursor(GlobalData.VM_MA.Artist.Cid, fantiaUtils.LastScanHeadPids);
        }

        private void MergeFantiaItems(IList<BaseItem> bis)
        {
            var current = _fantiaTempBis?.ToList()
                ?? (_tempSite == SiteType.Fantia ? _tempBis?.ToList() : null)
                ?? GlobalData.VM_MA.ItemList?.ToList()
                ?? new List<BaseItem>();
            if (bis != null && bis.Count > 0)
            {
                current = bis.Concat(current.Where(x => !bis.Contains(x))).ToList();
            }
            _fantiaTempBis = current;
            if (_tempSite == SiteType.Fantia)
            {
                _tempBis = current;
            }
            GlobalData.VM_MA.ItemList = current.Where(x => !x.Skip).ToList();
        }

        private void AdvanceFantiaLastDate()
        {
            if (GlobalData.VM_MA.FantiaPollingMode)
            {
                return;
            }
            if (string.IsNullOrEmpty(GlobalData.VM_MA.Date_End))
            {
                GlobalData.VM_MA.Date_Start = GlobalData.StartTime.ToString("yyyy/MM/dd HH:mm:ss");
                GlobalData.LastDateDic.Update(GlobalData.VM_MA.LastDate);
            }
            else
            {
                GlobalData.VM_MA.LastDate = GlobalData.VM_MA.LastDate_End;
                GlobalData.VM_MA.Date_Start = GlobalData.VM_MA.Date_End;
                GlobalData.LastDateDic.Update(GlobalData.VM_MA.LastDate_End);
            }
            _tempDate = GlobalData.VM_MA.Date_Start;
            _tempDate_end = GlobalData.VM_MA.Date_End;
        }

        private void SetXPollingState(FantiaPollingState state, DateTime? nextCheck = null, DateTime? lastCheck = null)
        {
            GlobalData.VM_MA.XPollingStatus = state;
            if (nextCheck.HasValue || state == FantiaPollingState.Stopped)
            {
                GlobalData.VM_MA.XPollingNextCheck = nextCheck;
            }
            if (lastCheck.HasValue)
            {
                GlobalData.VM_MA.XPollingLastCheck = lastCheck;
            }
        }

        private TimeSpan GetXPollInterval()
        {
            lock (_xPollRandomLock)
            {
                return TimeSpan.FromSeconds(_xPollRandom.Next(_xPollIntervalMinSeconds, _xPollIntervalMaxSeconds + 1));
            }
        }

        private async Task EnsureXPollingAsync()
        {
            if (!GlobalData.VM_MA.IsStarted)
            {
                _xPollingContext = null;
                await StopXPollingAsync();
                return;
            }
            if (GlobalData.VM_MA.Site == SiteType.X)
            {
                if (!GlobalData.VM_MA.XNotificationMode)
                {
                    _xPollingContext = null;
                    await StopXPollingAsync();
                    return;
                }
                _xPollingContext = CapturePollingContext();
            }
            if (_xPollingContext == null || !_xPollingContext.XNotificationMode)
            {
                return;
            }
            if (_xPollTask != null)
            {
                if (!_xPollTask.IsCompleted)
                {
                    return;
                }
                await StopXPollingAsync();
            }
            _xPollCts = new CancellationTokenSource();
            _xPollTask = XPollLoopAsync(_xPollCts.Token);
        }

        private async Task StopXPollingAsync()
        {
            if (_xPollCts == null)
            {
                SetXPollingState(FantiaPollingState.Stopped, null);
                return;
            }
            try
            {
                _xPollCts.Cancel();
                if (_xPollTask != null)
                {
                    await _xPollTask;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
            }
            finally
            {
                _xPollTask = null;
                _xPollCts.Dispose();
                _xPollCts = null;
                if (_xPollingContext == null)
                {
                    _xTempBis = null;
                }
                SetXPollingState(FantiaPollingState.Stopped, null);
            }
        }

        private async Task XPollLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && GlobalData.VM_MA.IsStarted && _xPollingContext != null && _xPollingContext.XNotificationMode)
                {
                    var context = _xPollingContext;
                    var pollInterval = GetXPollInterval();
                    SetXPollingState(FantiaPollingState.Running, DateTime.Now.Add(pollInterval));
                    await Task.Delay(pollInterval, token);
                    if (token.IsCancellationRequested || !GlobalData.VM_MA.IsStarted || _xPollingContext == null || !_xPollingContext.XNotificationMode)
                    {
                        break;
                    }
                    if (GlobalData.VM_DL != null && GlobalData.VM_DL.HasPendingDownloads)
                    {
                        var nextSkipCheck = GetXPollInterval();
                        SetXPollingState(FantiaPollingState.WaitingDownloads, DateTime.Now.Add(nextSkipCheck));
                        await Task.Delay(nextSkipCheck, token);
                        continue;
                    }
                    await _xRoundGate.WaitAsync(token);
                    try
                    {
                        await RunWithPollingContextAsync(context, token, async () =>
                        {
                            if (GlobalData.VM_DL != null && GlobalData.VM_DL.HasPendingDownloads)
                            {
                                SetXPollingState(FantiaPollingState.WaitingDownloads, DateTime.Now.Add(GetXPollInterval()));
                                return;
                            }
                            if (!await EnsureXPollReadyAsync())
                            {
                                SetXPollingState(FantiaPollingState.Running, DateTime.Now.Add(GetXPollInterval()));
                                return;
                            }
                            SetXPollingState(FantiaPollingState.Running, null, DateTime.Now);
                            await RunXNotificationRoundAsync();
                        });
                    }
                    finally
                    {
                        _xRoundGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
            }
        }

        private async Task<bool> EnsureXPollReadyAsync()
        {
            if (GlobalData.VM_MA.Site != SiteType.X || !GlobalData.VM_MA.XNotificationMode)
            {
                return false;
            }
            _utils = GlobalData.CaptureUtil;
            if (GlobalData.VM_MA.NeedCookie)
            {
                var ret = await _utils.CheckCookies();
                if (ret.Error != ErrorType.NoError || ret.Result == null || !(bool)ret.Result)
                {
                    return false;
                }
                GlobalData.VM_MA.NeedCookie = false;
            }
            return true;
        }

        private async Task<ResultMessage> RunXNotificationRoundAsync()
        {
            _utils = GlobalData.CaptureUtil;
            SetXPollingState(FantiaPollingState.Running, null, DateTime.Now);
            GlobalData.VM_MA.PostCount = 0;
            GlobalData.VM_MA.PostTitle = string.Empty;

            var ret = await _utils.GetPostIDs(GlobalData.VM_MA.Artist.Cid);
            if (ret.Error != ErrorType.NoError)
            {
                return ret;
            }

            var items = ToBaseItemList(ret.Result);
            MergeXItems(items);
            UpdateXSeenCursor(XUtils.NotificationWindowSize);
            if (GlobalData.VM_DL != null && !GlobalData.VM_DL.HasPendingDownloads)
            {
                GlobalMethord.CommitXSeenPendingCursor();
            }
            return ret;
        }

        private void MergeXItems(IList<BaseItem> items)
        {
            var current = _xTempBis?.ToList()
                ?? (_tempSite == SiteType.X ? _tempBis?.ToList() : null)
                ?? GlobalData.VM_MA.ItemList?.ToList()
                ?? new List<BaseItem>();
            if (items != null && items.Count > 0)
            {
                var incomingIds = new HashSet<string>(items.Select(x => x.ID).Where(x => !string.IsNullOrEmpty(x)));
                current = items.Concat(current.Where(x => !incomingIds.Contains(x.ID))).ToList();
                foreach (var item in items.OfType<XItem>())
                {
                    GlobalData.VM_DL.AddXCommand.Execute(item);
                }
            }
            _xTempBis = current;
            if (_tempSite == SiteType.X)
            {
                _tempBis = current;
            }
            GlobalData.VM_MA.ItemList = current.Where(x => !x.Skip).ToList();
        }

        private void UpdateXSeenCursor(int windowSize)
        {
            var xUtils = _utils as XUtils ?? GlobalData.CaptureUtil as XUtils;
            if (xUtils == null || string.IsNullOrEmpty(xUtils.LastCursorKey) || xUtils.LastScanHeadIds == null || xUtils.LastScanHeadIds.Count == 0)
            {
                return;
            }

            GlobalMethord.SetXSeenPendingCursor(xUtils.LastCursorKey, xUtils.LastScanHeadIds, windowSize);
        }

        private async void Btn_GetList_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GlobalData.VM_MA.IDName))
            {
                await GetCheck(GlobalLanguage.Msg_CheckCk);
                return;
            }
            ShowLoading(true);
            await Task.Delay(100);
            ResultMessage _ret = null;
            if (GlobalData.VM_MA.UseProxy)
            {
                GlobalMethord.GetSystemProxy();
            }
            _utils = GlobalData.CaptureUtil;
            await SetBrowserProxy();
            if (GlobalData.VM_MA.NeedCookie)
            {
                _ret = await _utils.CheckCookies();
                if (_ret.Error != ErrorType.NoError)
                {
                    await GetCheck(_ret.Msgs);
                    ShowLoading(false);
                    return;
                }
                if (_ret.Result == null)
                {
                    await GetCheck(GlobalLanguage.Msg_NeedLogin);
                    ShowLoading(false);
                    GlobalCommand.OpBrowserCommand.Execute(null);
                    return;
                }
                var flag = (bool)_ret.Result;
                if (!flag)
                {
                    await GetCheck(GlobalLanguage.Msg_ChangeAccount);
                    ShowLoading(false);
                    GlobalCommand.OpBrowserCommand.Execute(null);
                    return;
                }
                GlobalData.VM_MA.NeedCookie = false;
            }
            _ret = await _utils.GetArtistList();
            if (_ret.Error != ErrorType.NoError)
            {
                await GetCheck(_ret.Msgs);
                ShowLoading(false);
                return;
            }
            List<ArtistInfo> ais = (List<ArtistInfo>)_ret.Result;
            GlobalData.VM_MA.ArtistList = new System.Collections.ObjectModel.ObservableCollection<ArtistInfo>();
            foreach (var ai in ais)
            {
                GlobalData.VM_MA.ArtistList.Add(ai);
            }
            if (GlobalData.VM_MA.ArtistList.Count > 0)
            {
                GlobalData.VM_MA.Artist = GlobalData.VM_MA.ArtistList.Last();
            }
            await Task.Run(() => SaveSetting());
            ShowLoading(false);
        }

        private void Btn_Back_Click(object sender, RoutedEventArgs e)
        {
            GlobalCommand.BackCommand.Execute(BackType.Pop);
        }

        private async void Btn_OpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            await SetBrowserProxy();
            GlobalCommand.OpBrowserCommand.Execute(null);
            GlobalData.VM_MA.NeedCookie = true;
        }

        private async Task SetBrowserProxy()
        {
            await CefHelper.SetProxy((ChromiumWebBrowser)GlobalData.VM_MA.PatreonCefBrowser, GlobalData.VM_MA.UseProxy ? GlobalData.VM_MA.Proxy : "");
            if (GlobalData.VM_MA.XCefBrowser is ChromiumWebBrowser xBrowser)
            {
                await CefHelper.SetProxy(xBrowser, GlobalData.VM_MA.UseProxy ? GlobalData.VM_MA.Proxy : "");
            }
        }

        private bool CompareDate(string d1, string d2)
        {
            if (DateTime.TryParse(d1, out DateTime dt1))
            {
                if (DateTime.TryParse(d2, out DateTime dt2))
                {
                    return dt1 > dt2;
                }
            }
            return false;
        }

        private async Task<bool> GetCheck(params string[] msgs)
        {
            string mss = msgs[0];
            for (int i = 1; i < msgs.Length; i++)
            {
                mss += Environment.NewLine;
                mss += msgs[i];
            }
            GlobalData.VM_MA.Messages = mss;

            GlobalData.VM_MA.MLeft = (ActualWidth - 550) / 2;
            GlobalData.VM_MA.MTop = (ActualHeight - 300) / 2;
            while (GlobalData.VM_MA.ShowCheck)
            {
                await Task.Delay(500);
            }
            return true;
        }

        private void ShowLoading(bool flag)
        {
            GlobalData.VM_MA.ShowLoad = flag;
            if (flag)
            {
                _sbLoad?.Begin();
            }
            else
            {
                _sbLoad?.Stop();
            }
        }

        private void SaveSetting()
        {
            try
            {
                string fn = $"Settings\\Artists_{GlobalData.VM_MA.Site}.json";
                File.WriteAllText(fn, JsonHelper.ConverToJson(GlobalData.VM_MA.ArtistList));
                fn = $"Settings\\Setting_{GlobalData.VM_MA.Site}.ini";
                if (GlobalData.VM_MA.Site == SiteType.X)
                {
                    File.WriteAllLines(fn, new string[]
                    {
                        GlobalData.VM_MA.IDName,
                        GlobalData.VM_MA.SavePath,
                        GlobalData.VM_MA.Proxy,
                        GlobalData.VM_MA.UseProxy.ToString(),
                        GlobalData.VM_MA.XNotificationMode.ToString(),
                        GlobalData.VM_MA.XIncludeRetweetMedia.ToString(),
                        GlobalData.VM_MA.XIncludeQuoteMedia.ToString(),
                        GlobalData.VM_MA.XHistorySearchMode.ToString()
                    });
                }
                else
                {
                    File.WriteAllLines(fn, new string[] { GlobalData.VM_MA.IDName, GlobalData.VM_MA.SavePath, GlobalData.VM_MA.Proxy, GlobalData.VM_MA.UseProxy.ToString() });
                }
                if (!string.IsNullOrEmpty(GlobalData.VM_MA.ArtistList.Last().Id))
                {
                    Dispatcher.Invoke(() => GlobalData.VM_MA.ArtistList.Add(new ArtistInfo()));
                }
            }
            catch { }
        }

        private void LoadSetting()
        {
            try
            {
                GlobalData.VM_MA.Site = SiteType.Patreon;
                var ais = GlobalMethord.ReadArtists("Settings\\Artists_Patreon.json");
                foreach (var ai in ais)
                {
                    GlobalData.VM_MA.ArtistList.Add(ai);
                }
                if (File.Exists("Settings\\Setting_Patreon.ini"))
                {
                    var ms = File.ReadAllLines("Settings\\Setting_Patreon.ini");
                    GlobalData.VM_MA.IDName = ms[0];
                    GlobalData.VM_MA.SavePath = ms[1];
                    GlobalData.VM_MA.Proxy = ms[2];
                    GlobalData.VM_MA.UseProxy = bool.Parse(ms[3]);
                }
                GlobalData.VM_MA.Site = SiteType.Fantia;
                ais = GlobalMethord.ReadArtists("Settings\\Artists_Fantia.json");
                foreach (var ai in ais)
                {
                    GlobalData.VM_MA.ArtistList.Add(ai);
                }
                if (File.Exists("Settings\\Setting_Fantia.ini"))
                {
                    var ms = File.ReadAllLines("Settings\\Setting_Fantia.ini");
                    GlobalData.VM_MA.IDName = ms[0];
                    GlobalData.VM_MA.SavePath = ms[1];
                    GlobalData.VM_MA.Proxy = ms[2];
                    GlobalData.VM_MA.UseProxy = bool.Parse(ms[3]);
                    GlobalData.VM_MA.FantiaPollingMode = false;
                }
                GlobalData.VM_MA.Site = SiteType.X;
                ais = GlobalMethord.ReadArtists("Settings\\Artists_X.json");
                foreach (var ai in ais)
                {
                    GlobalData.VM_MA.ArtistList.Add(ai);
                }
                if (File.Exists("Settings\\Setting_X.ini"))
                {
                    var ms = File.ReadAllLines("Settings\\Setting_X.ini");
                    GlobalData.VM_MA.IDName = ms.Length > 0 ? ms[0] : string.Empty;
                    GlobalData.VM_MA.SavePath = ms.Length > 1 ? ms[1] : string.Empty;
                    GlobalData.VM_MA.Proxy = ms.Length > 2 ? ms[2] : string.Empty;
                    GlobalData.VM_MA.UseProxy = ms.Length > 3 && bool.Parse(ms[3]);
                    GlobalData.VM_MA.XNotificationMode = ms.Length > 4 && bool.Parse(ms[4]);
                    GlobalData.VM_MA.XIncludeRetweetMedia = ms.Length > 5 && bool.Parse(ms[5]);
                    GlobalData.VM_MA.XIncludeQuoteMedia = ms.Length > 6 && bool.Parse(ms[6]);
                    GlobalData.VM_MA.XHistorySearchMode = !GlobalData.VM_MA.XNotificationMode && ms.Length > 7 && bool.Parse(ms[7]);
                    GlobalData.VM_MA.SyncXRunModeDraft();
                }
                GlobalData.VM_MA.Site = SiteType.Fanbox;
                ais = GlobalMethord.ReadArtists("Settings\\Artists_Fanbox.json");
                foreach (var ai in ais)
                {
                    GlobalData.VM_MA.ArtistList.Add(ai);
                }
                if (File.Exists("Settings\\Setting_Fanbox.ini"))
                {
                    var ms = File.ReadAllLines("Settings\\Setting_Fanbox.ini");
                    GlobalData.VM_MA.IDName = ms[0];
                    GlobalData.VM_MA.SavePath = ms[1];
                    GlobalData.VM_MA.Proxy = ms[2];
                    GlobalData.VM_MA.UseProxy = bool.Parse(ms[3]);
                }
                if (File.Exists("Settings\\Setting_Normal.ini"))
                {
                    var ms = File.ReadAllLines("Settings\\Setting_Normal.ini");
                    GlobalData.VM_MA.CheckFile = ms[0] == "1";
                }
            }
            catch { }
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (popup.IsOpen)
            {
                if (!popup.IsMouseCaptured && !_isOverDropBtn)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (popup.IsOpen)
            {
                if (!popup.IsKeyboardFocused)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private void Togb_MouseEnter(object sender, MouseEventArgs e)
        {
            _isOverDropBtn = true;
        }

        private void Togb_MouseLeave(object sender, MouseEventArgs e)
        {
            _isOverDropBtn = false;
        }

        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Interop.HwndSource source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
            System.Windows.Forms.IWin32Window win = new OldWindow(source.Handle);
            FolderSelector fs = new FolderSelector()
            {
                DirectoryPath = string.IsNullOrEmpty(GlobalData.VM_MA.SavePath) ? Environment.CurrentDirectory : GlobalData.VM_MA.SavePath
            };
            if (fs.ShowDialog(win) == System.Windows.Forms.DialogResult.OK)
            {
                GlobalData.VM_MA.SavePath = fs.DirectoryPath;
            }
        }

        private class OldWindow : System.Windows.Forms.IWin32Window
        {
            IntPtr _handle;
            public OldWindow(IntPtr handle)
            {
                _handle = handle;
            }

            #region IWin32Window Members
            IntPtr System.Windows.Forms.IWin32Window.Handle
            {
                get { return _handle; }
            }
            #endregion
        }
    }
}
