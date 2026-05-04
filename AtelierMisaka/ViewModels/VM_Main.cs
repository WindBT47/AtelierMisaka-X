using AtelierMisaka.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace AtelierMisaka.ViewModels
{
    public class VM_Main : NotifyModel
    {
        #region MainWindow

        private double _mLeft = 0d;
        private double _mTop = 0d;
        private int _lZindex = 3;
        private ShowType _showList = ShowType.All;
        private MainSection _mainSection = MainSection.Downloads;
        private IList<BaseItem> _itemList = null;
        private IList<BaseItem> _itemList_Zero = null;
        private IList<BaseItem> _itemList_Link = null;
        private BaseItem _selectedDocument = null;
        private System.Windows.Controls.UserControl _popPage = null;
        private System.Windows.Controls.UserControl _downloadPage = null;

        public ShowType ShowList
        {
            get => _showList;
            set
            {
                if (_showList != value)
                {
                    _showList = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("ItemList");
                }
            }
        }

        public int LZindex
        {
            get => _lZindex;
            set
            {
                if (_lZindex != value)
                {
                    _lZindex = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IList<BaseItem> ItemList
        {
            get
            {
                switch (_showList)
                {
                    case ShowType.OnlyZero:
                        return _itemList_Zero;
                    case ShowType.OnlyHttp:
                        return _itemList_Link;
                    default:
                        return _itemList;
                }
            }
            set
            {
                if (_itemList != value)
                {
                    _itemList = value;
                    _itemList_Zero = _itemList.Where(x => x.IsZero).ToList();
                    _itemList_Link = _itemList.Where(x => x.HasLink).ToList();
                    RaisePropertyChanged();
                }
            }
        }

        public BaseItem SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (_selectedDocument != value)
                {
                    _selectedDocument = value;
                    RaisePropertyChanged();
                }
            }
        }

        public System.Windows.Controls.UserControl PopPage
        {
            get => _popPage;
            set
            {
                if (_popPage != value)
                {
                    _popPage = value;
                    RaisePropertyChanged();
                }
            }
        }

        public System.Windows.Controls.UserControl DownloadPage
        {
            get => _downloadPage;
            set
            {
                if (_downloadPage != value)
                {
                    _downloadPage = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ShowDocumentSection => _mainSection == MainSection.Documents;

        public bool ShowDownloadSection => _mainSection == MainSection.Downloads;

        public double MLeft
        {
            get => _mLeft;
            set
            {
                if (_mLeft != value)
                {
                    _mLeft = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double MTop
        {
            get => _mTop;
            set
            {
                if (_mTop != value)
                {
                    _mTop = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion

        #region Setting

        private SiteType _site = SiteType.Fanbox;

        private ArtistInfo _artistFanbox = new ArtistInfo();
        private string _cookiesFanbox = string.Empty;
        private string _idFanbox = string.Empty;
        private string _proxyFanbox = string.Empty;
        private string _savePathFanbox = string.Empty;
        private bool _useProxyFanbox = false;
        private bool _needCookieFanbox = true;

        private ArtistInfo _artistPatreon = new ArtistInfo();
        private string _cookiesPatreon = string.Empty;
        private string _idPatreon = string.Empty;
        private string _proxyPatreon = string.Empty;
        private string _savePathPatreon = string.Empty;
        private bool _useProxyPatreon = false;
        private bool _needCookiePatreon = true;

        private ArtistInfo _artistFantia = new ArtistInfo();
        private string _cookiesFantia = string.Empty;
        private string _idFantia = string.Empty;
        private string _proxyFantia = string.Empty;
        private string _savePathFantia = string.Empty;
        private bool _useProxyFantia = false;
        private bool _needCookieFantia = true;
        private bool _fantiaPollingMode = false;
        private bool _fantiaPollingModeDraft = false;
        private FantiaPollingState _fantiaPollingState = FantiaPollingState.Stopped;
        private DateTime? _fantiaPollingNextCheck = null;
        private DateTime? _fantiaPollingLastCheck = null;

        private ArtistInfo _artistX = new ArtistInfo();
        private string _cookiesX = string.Empty;
        private string _idX = string.Empty;
        private string _proxyX = string.Empty;
        private string _savePathX = string.Empty;
        private bool _useProxyX = false;
        private bool _needCookieX = true;
        private bool _xNotificationMode = false;
        private bool _xNotificationModeDraft = false;
        private bool _xHistorySearchMode = false;
        private bool _xHistorySearchModeDraft = false;
        private bool _xIncludeRetweetMedia = false;
        private bool _xIncludeQuoteMedia = false;
        private FantiaPollingState _xPollingStatus = FantiaPollingState.Stopped;
        private DateTime? _xPollingNextCheck = null;
        private DateTime? _xPollingLastCheck = null;

        private string _dateS = string.Empty;
        private string _dateE = string.Empty;
        private string _messages = string.Empty;
        private bool _checkFile = false;
        private bool _useDate = false;
        private bool _showCheck = false;
        private bool _showLoad = false;
        private bool _showLogin = false;
        private bool _isStarted = false;
        private bool _canChangeLang = true;
        private Regex _regex = new Regex(@"^\d+\.\d+\.\d+\.\d+:\d+$");
        private IList<ArtistInfo> _artistListFanbox = new ObservableCollection<ArtistInfo>();
        private IList<ArtistInfo> _artistListPatreon = new ObservableCollection<ArtistInfo>();
        private IList<ArtistInfo> _artistListFantia = new ObservableCollection<ArtistInfo>();
        private IList<ArtistInfo> _artistListX = new ObservableCollection<ArtistInfo>();

        private WebProxy _myProxyFanbox = null;
        private WebProxy _myProxyPatreon = null;
        private WebProxy _myProxyFantia = null;
        private WebProxy _myProxyX = null;
        private WebProxy _myProxySystem = null;
        private string _proxySystem = string.Empty;

        private object _patreonBrowser = null;
        private object _xBrowser = null;
        private bool _isInitialized = false;
        private bool _isOpen = false;
        private bool _isProxyError = false;

        private int _postCount = 0;
        private string _postTitle = string.Empty;

        private string _latestVer = "Check";

        public DateTime LastDate = DateTime.Parse("2010/01/01");
        public DateTime LastDate_End = DateTime.Now;

        public string CurrVersion { get; } = System.Windows.Application.ResourceAssembly.GetName().Version.ToString();

        public string VersionLink = "https://github.com/EndlessMISAKA/AtelierMisaka/releases/latest";
        //public string VersionDL = "https://github.com/EndlessMISAKA/AtelierMisaka/releases/download/V{0}/AtelierMisaka_Soft_{1}.zip";

        public string MainPage
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return "https://www.fanbox.cc";
                    case SiteType.Fantia:
                        return "https://fantia.jp";
                    case SiteType.Patreon:
                        return "https://www.patreon.com";
                    default:
                        return "https://x.com";
                }
            }
        }

        public string LatestVersion
        {
            get => _latestVer;
            set
            {
                if (_latestVer != value)
                {
                    _latestVer = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string PostTitle
        {
            get => _postTitle;
            set
            {
                if (_postTitle != value)
                {
                    _postTitle = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int PostCount
        {
            get => _postCount;
            set
            {
                if (_postCount != value)
                {
                    _postCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        public object PatreonCefBrowser
        {
            get => _patreonBrowser;
            set
            {
                if (_patreonBrowser != value)
                {
                    _patreonBrowser = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsInitialized
        {
            get => _isInitialized;
            set
            {
                if (_isInitialized != value)
                {
                    _isInitialized = value;
                }
            }
        }

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsProxyError
        {
            get => _isProxyError;
            set
            {
                if (_isProxyError != value)
                {
                    _isProxyError = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool CheckFile
        {
            get => _checkFile;
            set
            {
                if (_checkFile != value)
                {
                    _checkFile = value;
                    RaisePropertyChanged();
                    GlobalMethord.SaveNorSet();
                }
            }
        }

        public bool ShowLogin
        {
            get => _showLogin;
            set
            {
                if (_showLogin != value)
                {
                    _showLogin = value;
                    RaisePropertyChanged();
                }
            }
        }

        public SiteType Site
        {
            get => _site;
            set
            {
                if (_site != value)
                {
                    _site = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("ArtistList");
                    RaisePropertyChanged("IDName");
                    RaisePropertyChanged("UseProxy");
                    RaisePropertyChanged("Proxy");
                    RaisePropertyChanged("SavePath");
                    RaisePropertyChanged("CookieTag");
                    RaisePropertyChanged("PostUrlTag");
                    RaisePropertyChanged("HasSelected");
                    RaisePropertyChanged("Artist");
                    RaisePropertyChanged("FantiaPollingMode");
                    RaisePropertyChanged("ShowFantiaRunModeTabs");
                    RaisePropertyChanged("FantiaTimeRangeModeDraft");
                    RaisePropertyChanged("FantiaPollingSubscriptionModeDraft");
                    RaisePropertyChanged("ShowStandardDateSection");
                    RaisePropertyChanged("ShowFantiaTimeRangeSection");
                    RaisePropertyChanged("ShowFantiaPollingSection");
                    RaisePropertyChanged("XNotificationMode");
                    RaisePropertyChanged("XHistorySearchMode");
                    RaisePropertyChanged("XMediaScanModeDraft");
                    RaisePropertyChanged("XHistorySearchModeDraft");
                    RaisePropertyChanged("XNotificationListenModeDraft");
                    RaisePropertyChanged("ShowXRunModeTabs");
                    RaisePropertyChanged("ShowCreatorUrlSection");
                    RaisePropertyChanged("ShowPaymentSection");
                    RaisePropertyChanged("ShowXOptionSection");
                    RaisePropertyChanged("ShowXHistorySearchSection");
                    RaisePropertyChanged("ShowXNotificationSection");
                    RaisePropertyChanged("CanEditDateEnd");
                }
            }
        }

        public object XCefBrowser
        {
            get => _xBrowser;
            set
            {
                if (_xBrowser != value)
                {
                    _xBrowser = value;
                    RaisePropertyChanged();
                }
            }
        }

        public MainSection MainSection
        {
            get => _mainSection;
            set
            {
                if (_mainSection != value)
                {
                    _mainSection = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("ShowDocumentSection");
                    RaisePropertyChanged("ShowDownloadSection");
                }
            }
        }

        public ArtistInfo Artist
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _artistFanbox;
                    case SiteType.Fantia:
                        return _artistFantia;
                    case SiteType.Patreon:
                        return _artistPatreon;
                    default:
                        return _artistX;
                }
            }
            set
            {
                if (null != value)
                {
                    switch (_site)
                    {
                        case SiteType.Fanbox:
                            if (_artistFanbox != value)
                            {
                                _artistFanbox = value;
                            }
                            break;
                        case SiteType.Fantia:
                            if (_artistFantia != value)
                            {
                                _artistFantia = value;
                            }
                            break;
                        case SiteType.Patreon:
                            if (_artistPatreon != value)
                            {
                                _artistPatreon = value;
                            }
                            break;
                        default:
                            if (_artistX != value)
                            {
                                _artistX = value;
                            }
                            break;
                    }
                    RaisePropertyChanged();
                    RaisePropertyChanged("HasSelected");
                    if (GlobalData.LastDateDic.TryGetValue(GlobalData.VM_MA.Site, value.Id, out DateTime dt))
                    {
                        Date_Start = dt.ToString("yyyy/MM/dd HH:mm:ss");
                        UseDate = true;
                    }
                    else
                    {
                        if (!ShowLoad)
                        {
                            Date_Start = string.Empty;
                            UseDate = false;
                        }
                    }
                }
            }
        }

        public IList<ArtistInfo> ArtistList
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _artistListFanbox;
                    case SiteType.Fantia:
                        return _artistListFantia;
                    case SiteType.Patreon:
                        return _artistListPatreon;
                    default:
                        return _artistListX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_artistListFanbox != value)
                        {
                            _artistListFanbox = value;
                        }
                        break;
                    case SiteType.Fantia:
                        if (_artistListFantia != value)
                        {
                            _artistListFantia = value;
                        }
                        break;
                    case SiteType.Patreon:
                        if (_artistListPatreon != value)
                        {
                            _artistListPatreon = value;
                        }
                        break;
                    default:
                        if (_artistListX != value)
                        {
                            _artistListX = value;
                        }
                        break;
                }
                RaisePropertyChanged();
            }
        }

        public int SelectedLang
        {
            get => GlobalData.CurrentCulStr;
            set
            {
                if (!GlobalData.CurrentCulStr.Equals(value))
                {
                    CanChangeLang = false;

                    GlobalData.CurrentCulStr = value;
                    GlobalLanguage.SetCulture(GlobalData.CurrentCulStr);

                    CanChangeLang = true;
                }
            }
        }

        public bool CanChangeLang
        {
            get => _canChangeLang;
            set
            {
                if (_canChangeLang != value)
                {
                    _canChangeLang = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UpdateCul
        {
            set
            {
                RaisePropertyChanged("CookieTag");
                RaisePropertyChanged("PostUrlTag");
                RaisePropertyChanged("FantiaPollingStatusText");
                RaisePropertyChanged("XPollingStatusText");
            }
        }

        public string CookieTag
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return GlobalLanguage.Text_CookiesFanbox;
                    case SiteType.Fantia:
                        return GlobalLanguage.Text_CookiesFantia;
                    case SiteType.Patreon:
                        return GlobalLanguage.Text_CookiesPatreon;
                    default:
                        return GlobalLanguage.Text_CookiesX;
                }
            }
        }

        public string PostUrlTag
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return GlobalLanguage.Text_CreatorUrlFanbox;
                    case SiteType.Fantia:
                        return GlobalLanguage.Text_CreatorUrlFantia;
                    case SiteType.Patreon:
                        return GlobalLanguage.Text_CreatorUrlPatreon;
                    default:
                        return GlobalLanguage.Text_CreatorUrlX;
                }
            }
        }

        public bool HasSelected
        {
            get => (Artist != null) && !string.IsNullOrEmpty(Artist.Id);
        }

        public string Messages
        {
            get => _messages;
            set
            {
                if (_messages != value)
                {
                    _messages = value;
                    RaisePropertyChanged();
                }
                ShowCheck = true;
            }
        }

        public bool ShowCheck
        {
            get => _showCheck;
            set
            {
                if (_showCheck != value)
                {
                    _showCheck = value;
                    if (value)
                    {
                        GlobalData.CheckResult = null;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public string SavePath
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _savePathFanbox;
                    case SiteType.Fantia:
                        return _savePathFantia;
                    case SiteType.Patreon:
                        return _savePathPatreon;
                    default:
                        return _savePathX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_savePathFanbox != value)
                        {
                            _savePathFanbox = value;
                        }
                        break;
                    case SiteType.Fantia:
                        if (_savePathFantia != value)
                        {
                            _savePathFantia = value;
                        }
                        break;
                    case SiteType.Patreon:
                        if (_savePathPatreon != value)
                        {
                            _savePathPatreon = value;
                        }
                        break;
                    default:
                        if (_savePathX != value)
                        {
                            _savePathX = value;
                        }
                        break;
                }
                RaisePropertyChanged();
            }
        }

        public bool NeedCookie
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _needCookieFanbox;
                    case SiteType.Fantia:
                        return _needCookieFantia;
                    case SiteType.Patreon:
                        return _needCookiePatreon;
                    default:
                        return _needCookieX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        _needCookieFanbox = value;
                        break;
                    case SiteType.Fantia:
                        _needCookieFantia = value;
                        break;
                    case SiteType.Patreon:
                        _needCookiePatreon = value;
                        break;
                    default:
                        _needCookieX = value;
                        break;
                }
            }
        }

        public string IDName
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _idFanbox;
                    case SiteType.Fantia:
                        return _idFantia;
                    case SiteType.Patreon:
                        return _idPatreon;
                    default:
                        return _idX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_idFanbox != value)
                        {
                            _idFanbox = value;
                        }
                        break;
                    case SiteType.Fantia:
                        if (_idFantia != value)
                        {
                            _idFantia = value;
                        }
                        break;
                    case SiteType.Patreon:
                        if (_idPatreon != value)
                        {
                            _idPatreon = value;
                        }
                        break;
                    default:
                        if (_idX != value)
                        {
                            _idX = value;
                        }
                        break;
                }
            }
        }

        public string Cookies
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _cookiesFanbox;
                    case SiteType.Fantia:
                        return _cookiesFantia;
                    case SiteType.Patreon:
                        return _cookiesPatreon;
                    default:
                        return _cookiesX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_cookiesFanbox != value)
                        {
                            if (!value.StartsWith("FANBOXSESSID="))
                            {
                                _cookiesFanbox = $"FANBOXSESSID={value}";
                            }
                            else
                            {
                                _cookiesFanbox = value;
                            }
                        }
                        break;
                    case SiteType.Fantia:
                        if (_cookiesFantia != value)
                        {
                            if (!value.StartsWith("_session_id="))
                            {
                                _cookiesFantia = $"_session_id={value}";
                            }
                            else
                            {
                                _cookiesFantia = value;
                            }
                        }
                        break;
                    case SiteType.Patreon:
                        if (_cookiesPatreon != value)
                        {
                            _cookiesPatreon = value;
                        }
                        break;
                    default:
                        if (_cookiesX != value)
                        {
                            _cookiesX = value;
                        }
                        break;
                }
                RaisePropertyChanged();
            }
        }

        public string Date_Start
        {
            get => _dateS;
            set
            {
                if (_dateS != value)
                {
                    _dateS = value;
                    RaisePropertyChanged();
                    if (string.IsNullOrEmpty(_dateS))
                    {
                        LastDate = DateTime.Parse("2010/01/01");
                    }
                    else
                    {
                        if (DateTime.TryParse(_dateS, out DateTime dt))
                        {
                            LastDate = dt;
                        }
                    }
                }
            }
        }

        public string Date_End
        {
            get => _dateE;
            set
            {
                if (_dateE != value)
                {
                    _dateE = value;
                    RaisePropertyChanged();
                    if (string.IsNullOrEmpty(_dateE))
                    {
                        LastDate_End = DateTime.Now;
                    }
                    else
                    {
                        if (DateTime.TryParse(_dateE, out DateTime dt))
                        {
                            LastDate_End = dt;
                        }
                    }
                }
            }
        }

        public string Proxy
        {
            get
            {
                var res = string.Empty;
                switch (_site)
                {
                    case SiteType.Fanbox:
                        res = _proxyFanbox;
                        break;
                    case SiteType.Fantia:
                        res = _proxyFantia;
                        break;
                    case SiteType.Patreon:
                        res = _proxyPatreon;
                        break;
                    default:
                        res = _proxyX;
                        break;
                }
                if (string.IsNullOrEmpty(res))
                {
                    return _proxySystem;
                }
                else
                {
                    return res;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_proxyFanbox != value)
                        {
                            _proxyFanbox = value;
                            if (_regex.IsMatch(_proxyFanbox))
                            {
                                try
                                {
                                    _myProxyFanbox = new WebProxy(_proxyFanbox);
                                }
                                catch { _myProxyFanbox = null; }
                            }
                            else
                            {
                                _myProxyFanbox = null;
                            }
                            IsProxyError = _myProxyFanbox == null;
                        }
                        break;
                    case SiteType.Fantia:
                        if (_proxyFantia != value)
                        {
                            _proxyFantia = value;
                            if (_regex.IsMatch(_proxyFantia))
                            {
                                try
                                {
                                    _myProxyFantia = new WebProxy(_proxyFantia);
                                }
                                catch { _myProxyFantia = null; }
                            }
                            else
                            {
                                _myProxyFantia = null;
                            }
                            IsProxyError = _myProxyFantia == null;
                        }
                        break;
                    case SiteType.Patreon:
                        if (_proxyPatreon != value)
                        {
                            _proxyPatreon = value;
                            if (_regex.IsMatch(_proxyPatreon))
                            {
                                try
                                {
                                    _myProxyPatreon = new WebProxy(_proxyPatreon);
                                }
                                catch { _myProxyPatreon = null; }
                            }
                            else
                            {
                                _myProxyPatreon = null;
                            }
                            IsProxyError = _myProxyPatreon == null;
                        }
                        break;
                    default:
                        if (_proxyX != value)
                        {
                            _proxyX = value;
                            if (_regex.IsMatch(_proxyX))
                            {
                                try
                                {
                                    _myProxyX = new WebProxy(_proxyX);
                                }
                                catch { _myProxyX = null; }
                            }
                            else
                            {
                                _myProxyX = null;
                            }
                            IsProxyError = _myProxyX == null;
                        }
                        break;
                }
                RaisePropertyChanged();
            }
        }

        public string ProxySystem
        {
            set
            {
                if (_proxySystem != value)
                {
                    _proxySystem = value;
                    if (_regex.IsMatch(_proxySystem))
                    {
                        try
                        {
                            _myProxySystem = new WebProxy(_proxySystem);
                        }
                        catch { _myProxySystem = null; }
                    }
                    else
                    {
                        _myProxySystem = null;
                    }
                    RaisePropertyChanged("Proxy");
                }
            }
        }

        public WebProxy MyProxy
        {
            get
            {
                if (string.IsNullOrEmpty(Proxy) || _isProxyError)
                {
                    return _myProxySystem;
                }
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _myProxyFanbox;
                    case SiteType.Fantia:
                        return _myProxyFantia;
                    case SiteType.Patreon:
                        return _myProxyPatreon;
                    default:
                        return _myProxyX;
                }
            }
        }

        public bool UseProxy
        {
            get
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        return _useProxyFanbox;
                    case SiteType.Fantia:
                        return _useProxyFantia;
                    case SiteType.Patreon:
                        return _useProxyPatreon;
                    default:
                        return _useProxyX;
                }
            }
            set
            {
                switch (_site)
                {
                    case SiteType.Fanbox:
                        if (_useProxyFanbox != value)
                        {
                            _useProxyFanbox = value;
                        }
                        break;
                    case SiteType.Fantia:
                        if (_useProxyFantia != value)
                        {
                            _useProxyFantia = value;
                        }
                        break;
                    case SiteType.Patreon:
                        if (_useProxyPatreon != value)
                        {
                            _useProxyPatreon = value;
                        }
                        break;
                    default:
                        if (_useProxyX != value)
                        {
                            _useProxyX = value;
                        }
                        break;
                }
                RaisePropertyChanged();
            }
        }

        public bool FantiaPollingMode
        {
            get => _fantiaPollingMode;
            set
            {
                if (_fantiaPollingMode != value)
                {
                    _fantiaPollingMode = value;
                    if (value)
                    {
                        UseDate = true;
                    }
                    else
                    {
                        FantiaPollingStatus = FantiaPollingState.Stopped;
                        FantiaPollingNextCheck = null;
                    }
                    _fantiaPollingModeDraft = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("FantiaTimeRangeModeDraft");
                    RaisePropertyChanged("FantiaPollingSubscriptionModeDraft");
                    RaisePropertyChanged("ShowStandardDateSection");
                    RaisePropertyChanged("ShowFantiaTimeRangeSection");
                    RaisePropertyChanged("ShowFantiaPollingSection");
                    RaisePropertyChanged("CanEditDateEnd");
                }
            }
        }

        public bool FantiaPollingModeDraft
        {
            get => _fantiaPollingModeDraft;
            set
            {
                if (_fantiaPollingModeDraft != value)
                {
                    _fantiaPollingModeDraft = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("FantiaTimeRangeModeDraft");
                    RaisePropertyChanged("FantiaPollingSubscriptionModeDraft");
                    RaisePropertyChanged("ShowFantiaTimeRangeSection");
                    RaisePropertyChanged("ShowFantiaPollingSection");
                }
            }
        }

        public bool FantiaTimeRangeModeDraft
        {
            get => !_fantiaPollingModeDraft;
            set
            {
                if (value)
                {
                    FantiaPollingModeDraft = false;
                    if (_site == SiteType.Fantia)
                    {
                        UseDate = true;
                    }
                }
            }
        }

        public bool FantiaPollingSubscriptionModeDraft
        {
            get => _fantiaPollingModeDraft;
            set
            {
                FantiaPollingModeDraft = value;
            }
        }

        public void SyncFantiaRunModeDraft()
        {
            FantiaPollingModeDraft = FantiaPollingMode;
        }

        public void ApplyFantiaRunModeDraft()
        {
            FantiaPollingMode = FantiaPollingModeDraft;
        }

        public FantiaPollingState FantiaPollingStatus
        {
            get => _fantiaPollingState;
            set
            {
                if (_fantiaPollingState != value)
                {
                    _fantiaPollingState = value;
                    RaisePropertyChanged("FantiaPollingStatusText");
                }
            }
        }

        public DateTime? FantiaPollingNextCheck
        {
            get => _fantiaPollingNextCheck;
            set
            {
                if (_fantiaPollingNextCheck != value)
                {
                    _fantiaPollingNextCheck = value;
                    RaisePropertyChanged("FantiaPollingNextCheckText");
                }
            }
        }

        public DateTime? FantiaPollingLastCheck
        {
            get => _fantiaPollingLastCheck;
            set
            {
                if (_fantiaPollingLastCheck != value)
                {
                    _fantiaPollingLastCheck = value;
                    RaisePropertyChanged("FantiaPollingLastCheckText");
                }
            }
        }

        public string FantiaPollingStatusText
        {
            get
            {
                switch (_fantiaPollingState)
                {
                    case FantiaPollingState.Running:
                        return GlobalLanguage.Text_PollStateRunning;
                    case FantiaPollingState.WaitingDownloads:
                        return GlobalLanguage.Text_PollStateWaiting;
                    default:
                        return GlobalLanguage.Text_PollStateStopped;
                }
            }
        }

        public string FantiaPollingNextCheckText => _fantiaPollingNextCheck.HasValue ? _fantiaPollingNextCheck.Value.ToString("HH:mm:ss") : "--:--:--";

        public string FantiaPollingLastCheckText => _fantiaPollingLastCheck.HasValue ? _fantiaPollingLastCheck.Value.ToString("HH:mm:ss") : "--:--:--";

        public bool IsStarted
        {
            get => _isStarted;
            set
            {
                if (_isStarted != value)
                {
                    _isStarted = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseDate
        {
            get => _useDate;
            set
            {
                if (_useDate != value)
                {
                    _useDate = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("CanEditDateEnd");
                }
            }
        }

        public bool ShowFantiaRunModeTabs => _site == SiteType.Fantia;

        public bool ShowStandardDateSection => _site != SiteType.Fantia && !(_site == SiteType.X && _xNotificationModeDraft);

        public bool ShowFantiaTimeRangeSection => _site == SiteType.Fantia && !_fantiaPollingModeDraft;

        public bool ShowFantiaPollingSection => _site == SiteType.Fantia && _fantiaPollingModeDraft;

        public bool CanEditDateEnd => _useDate && !(_site == SiteType.Fantia && _fantiaPollingMode);

        public bool XNotificationMode
        {
            get => _xNotificationMode;
            set
            {
                if (_xNotificationMode != value)
                {
                    _xNotificationMode = value;
                    if (value)
                    {
                        _xHistorySearchMode = false;
                        _xHistorySearchModeDraft = false;
                    }
                    if (!value)
                    {
                        XPollingStatus = FantiaPollingState.Stopped;
                        XPollingNextCheck = null;
                    }
                    _xNotificationModeDraft = value;
                    RaiseXRunModeProperties();
                }
            }
        }

        public bool XNotificationModeDraft
        {
            get => _xNotificationModeDraft;
            set
            {
                if (_xNotificationModeDraft != value)
                {
                    _xNotificationModeDraft = value;
                    if (value)
                    {
                        _xHistorySearchModeDraft = false;
                    }
                    RaiseXRunModeProperties();
                }
            }
        }

        public bool XMediaScanModeDraft
        {
            get => !_xHistorySearchModeDraft && !_xNotificationModeDraft;
            set
            {
                if (value)
                {
                    XNotificationModeDraft = false;
                    XHistorySearchModeDraft = false;
                }
            }
        }

        public bool XHistorySearchMode
        {
            get => _xHistorySearchMode;
            set
            {
                if (_xHistorySearchMode != value)
                {
                    _xHistorySearchMode = value;
                    if (value)
                    {
                        _xNotificationMode = false;
                        _xNotificationModeDraft = false;
                        XPollingStatus = FantiaPollingState.Stopped;
                        XPollingNextCheck = null;
                        UseDate = true;
                    }
                    _xHistorySearchModeDraft = value;
                    RaiseXRunModeProperties();
                }
            }
        }

        public bool XHistorySearchModeDraft
        {
            get => _xHistorySearchModeDraft;
            set
            {
                if (_xHistorySearchModeDraft != value)
                {
                    _xHistorySearchModeDraft = value;
                    if (value)
                    {
                        _xNotificationModeDraft = false;
                        UseDate = true;
                    }
                    RaiseXRunModeProperties();
                }
            }
        }

        public bool XNotificationListenModeDraft
        {
            get => _xNotificationModeDraft;
            set
            {
                XNotificationModeDraft = value;
            }
        }

        public void SyncXRunModeDraft()
        {
            XNotificationModeDraft = XNotificationMode;
            XHistorySearchModeDraft = XHistorySearchMode;
        }

        public void ApplyXRunModeDraft()
        {
            XHistorySearchMode = XHistorySearchModeDraft;
            XNotificationMode = XNotificationModeDraft;
            if (!XHistorySearchModeDraft && !XNotificationModeDraft)
            {
                XHistorySearchMode = false;
                XNotificationMode = false;
            }
        }

        private void RaiseXRunModeProperties()
        {
            RaisePropertyChanged("XNotificationMode");
            RaisePropertyChanged("XHistorySearchMode");
            RaisePropertyChanged("XMediaScanModeDraft");
            RaisePropertyChanged("XHistorySearchModeDraft");
            RaisePropertyChanged("XNotificationListenModeDraft");
            RaisePropertyChanged("ShowCreatorUrlSection");
            RaisePropertyChanged("ShowStandardDateSection");
            RaisePropertyChanged("ShowXHistorySearchSection");
            RaisePropertyChanged("ShowXNotificationSection");
        }

        public bool XIncludeRetweetMedia
        {
            get => _xIncludeRetweetMedia;
            set
            {
                if (_xIncludeRetweetMedia != value)
                {
                    _xIncludeRetweetMedia = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool XIncludeQuoteMedia
        {
            get => _xIncludeQuoteMedia;
            set
            {
                if (_xIncludeQuoteMedia != value)
                {
                    _xIncludeQuoteMedia = value;
                    RaisePropertyChanged();
                }
            }
        }

        public FantiaPollingState XPollingStatus
        {
            get => _xPollingStatus;
            set
            {
                if (_xPollingStatus != value)
                {
                    _xPollingStatus = value;
                    RaisePropertyChanged("XPollingStatusText");
                }
            }
        }

        public DateTime? XPollingNextCheck
        {
            get => _xPollingNextCheck;
            set
            {
                if (_xPollingNextCheck != value)
                {
                    _xPollingNextCheck = value;
                    RaisePropertyChanged("XPollingNextCheckText");
                }
            }
        }

        public DateTime? XPollingLastCheck
        {
            get => _xPollingLastCheck;
            set
            {
                if (_xPollingLastCheck != value)
                {
                    _xPollingLastCheck = value;
                    RaisePropertyChanged("XPollingLastCheckText");
                }
            }
        }

        public string XPollingStatusText
        {
            get
            {
                switch (_xPollingStatus)
                {
                    case FantiaPollingState.Running:
                        return GlobalLanguage.Text_PollStateRunning;
                    case FantiaPollingState.WaitingDownloads:
                        return GlobalLanguage.Text_PollStateWaiting;
                    default:
                        return GlobalLanguage.Text_PollStateStopped;
                }
            }
        }

        public string XPollingNextCheckText => _xPollingNextCheck.HasValue ? _xPollingNextCheck.Value.ToString("HH:mm:ss") : "--:--:--";

        public string XPollingLastCheckText => _xPollingLastCheck.HasValue ? _xPollingLastCheck.Value.ToString("HH:mm:ss") : "--:--:--";

        public bool ShowXRunModeTabs => _site == SiteType.X;

        public bool ShowCreatorUrlSection => !(_site == SiteType.X && _xNotificationModeDraft);

        public bool ShowPaymentSection => _site != SiteType.X;

        public bool ShowXOptionSection => _site == SiteType.X;

        public bool ShowXHistorySearchSection => _site == SiteType.X && _xHistorySearchModeDraft;

        public bool ShowXNotificationSection => _site == SiteType.X && _xNotificationModeDraft;

        public bool ShowLoad
        {
            get => _showLoad;
            set
            {
                if (_showLoad != value)
                {
                    _showLoad = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion

        #region Document

        public bool _isShowDocument = false;

        public bool IsShowDocument
        {
            get => _isShowDocument;
            set
            {
                if (_isShowDocument != value)
                {
                    _isShowDocument = value;
                    RaisePropertyChanged();
                }
                RaisePropertyChanged("Img_Document");
                RaisePropertyChanged("Title_Document");
                RaisePropertyChanged("Date_Document");
                RaisePropertyChanged("IsLiked_Document");
            }
        }

        public byte[] Img_Document
        {
            get
            {
                return IsShowDocument ? _selectedDocument.ImgData : null;
            }
        }

        public string Title_Document
        {
            get
            {
                return IsShowDocument ? _selectedDocument.Title : string.Empty;
            }
        }

        public string Date_Document
        {
            get
            {
                return IsShowDocument ? _selectedDocument.CreateDate.ToString("yyyy/MM/dd HH:mm") : string.Empty;
            }
        }

        public string UpDate_Document
        {
            get
            {
                return IsShowDocument ? _selectedDocument.UpdateDate.ToString("yyyy/MM/dd HH:mm") : string.Empty;
            }
        }

        public string DeadDate_Document
        {
            get
            {
                return IsShowDocument ? ((_selectedDocument is FantiaItem) ? (_selectedDocument as FantiaItem).DeadDate : string.Empty) : string.Empty;
            }
        }

        public System.Windows.Visibility Site_Document
        {
            get
            {
                return IsShowDocument ? ((_site == SiteType.Fantia) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed) : System.Windows.Visibility.Collapsed;
            }
        }

        public bool IsLiked_Document
        {
            get
            {
                if (null != _selectedDocument)
                {
                    return _selectedDocument.IsLiked;
                }
                return false;
            }
            set
            {
                if (_selectedDocument.IsLiked != value)
                {
                    _selectedDocument.IsLiked = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion
    }
}
