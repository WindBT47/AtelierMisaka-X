using AtelierMisaka.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AtelierMisaka.Views
{
    /// <summary>
    /// Downloader.xaml 的交互逻辑//Alchemy Cauldron
    /// </summary>
    public partial class Downloader : UserControl
    {
        private IList<BaseItem> _baseItems = null;
        private bool _mouseD = false;
        private Point _mouM = new Point(0, 0);

        public Downloader()
        {
            InitializeComponent();
            GlobalData.VM_DL = VM_DD;
            Reset();
        }

        public void Reset()
        {
            _baseItems = null;
            VM_DD.DLClients = new List<DownloadItem>();
            VM_DD.CompletedList = new ObservableCollection<DownloadItem>();
            VM_DD.DownLoadItemList = new ObservableCollection<DownloadItem>();
            VM_DD.IsDownloading = false;
            VM_DD.ShowCheck = false;
            VM_DD.IsFantia = false;
        }

        private void PrepareSession(IList<BaseItem> bis, bool flag)
        {
            _baseItems = !flag && bis != null ? bis.Reverse().ToList() : bis;
            VM_DD.TempAI = GlobalData.VM_MA.Artist.Id;
            VM_DD.TempAN = GlobalData.VM_MA.Artist.AName;
            VM_DD.TempSite = GlobalData.VM_MA.Site;
            VM_DD.SavePath = GlobalData.VM_MA.SavePath;
            VM_DD.DLClients = new List<DownloadItem>();
            VM_DD.CompletedList = new ObservableCollection<DownloadItem>();
            VM_DD.DownLoadItemList = new ObservableCollection<DownloadItem>();
            VM_DD.IsDownloading = false;
            VM_DD.ShowCheck = false;
            VM_DD.IsFantia = false;
            VM_DD.IsFantia = flag;
            GlobalData.LastDateDic.Add(new CLastDate(VM_DD.TempAI, VM_DD.TempSite, GlobalData.VM_MA.LastDate));
        }

        public async void LoadData(IList<BaseItem> bis = null, bool flag = false)
        {
            PrepareSession(bis, flag);
            var downLoadItemList = new ObservableCollection<DownloadItem>();
            if (_baseItems == null && GlobalData.VM_MA.Site != SiteType.Fantia)
            {
                VM_DD.DownLoadItemList = downLoadItemList;
                return;
            }

            await Task.Run(() =>
            {
                DownloadItem di = null;
                var linkfile = Path.Combine(VM_DD.SavePath, GlobalData.VM_MA.Artist.AName, $"link_{DateTime.Now:yyyyMMdd_HHmm}.html");
                switch (GlobalData.VM_MA.Site)
                {
                    case SiteType.Fanbox:
                        {
                            foreach (var bi in _baseItems)
                            {
                                if (bi.Skip)
                                {
                                    continue;
                                }
                                string sp = Path.Combine(VM_DD.SavePath, GlobalData.VM_MA.Artist.AName, $"{bi.CreateDate:yyyyMM\\\\dd_HHmm}_${bi.Fee}_{bi.Title}");
                                try
                                {
                                    Directory.CreateDirectory(sp);
                                }
                                catch
                                {
                                    if (!Directory.Exists(sp))
                                    {
                                        sp = GlobalMethord.RemoveLastDot(GlobalMethord.ReplacePath(sp));
                                        Directory.CreateDirectory(sp);
                                    }
                                }
                                GlobalData.DLLogs.SetPId(bi.ID);
                                if (!string.IsNullOrEmpty(bi.CoverPic))
                                {
                                    if (!GlobalData.DLLogs.HasLog(bi.CoverPic, sp, bi.CoverPicName))
                                    {
                                        di = new DownloadItem
                                        {
                                            FileName = bi.CoverPicName,
                                            Link = bi.CoverPic,
                                            SavePath = sp,
                                            CTime = bi.CreateDate,
                                            SourceDocu = bi,
                                            AId = VM_DD.TempAI
                                        };
                                        di.CheckTempFile();
                                        downLoadItemList.Add(di);
                                    }
                                }
                                for (int i = 0; i < bi.ContentUrls.Count; i++)
                                {
                                    if (!GlobalData.DLLogs.HasLog(bi.ContentUrls[i], sp, bi.FileNames[i]))
                                    {
                                        di = new DownloadItem
                                        {
                                            FileName = bi.FileNames[i],
                                            Link = bi.ContentUrls[i],
                                            SavePath = sp,
                                            CTime = bi.CreateDate,
                                            SourceDocu = bi,
                                            AId = VM_DD.TempAI
                                        };
                                        di.CheckTempFile();
                                        downLoadItemList.Add(di);
                                    }
                                }
                                for (int i = 0; i < bi.MediaUrls.Count; i++)
                                {
                                    if (!GlobalData.DLLogs.HasLog(bi.MediaUrls[i], sp, bi.MediaNames[i]))
                                    {
                                        di = new DownloadItem
                                        {
                                            FileName = bi.MediaNames[i],
                                            Link = bi.MediaUrls[i],
                                            SavePath = sp,
                                            CTime = bi.CreateDate,
                                            SourceDocu = bi,
                                            AId = VM_DD.TempAI
                                        };
                                        di.CheckTempFile();
                                        downLoadItemList.Add(di);
                                    }
                                }
                                if (bi.Comments.Count > 0)
                                {
                                    try
                                    {
                                        var commentContent = string.Join("<br/>", bi.Comments);
                                        var fp = Path.Combine(sp, "Comment.html");
                                        if (File.Exists(fp))
                                        {
                                            var cms = File.ReadAllText(fp);
                                            if (cms == commentContent)
                                            {
                                                continue;
                                            }
                                        }
                                        File.WriteAllText(Path.Combine(sp, "Comment.html"), commentContent);
                                        bi.Comments.ForEach(x =>
                                        {
                                            var ma = GlobalRegex.Regex_Url.Match(x);
                                            if (ma.Success)
                                            {
                                                File.AppendAllText(linkfile, $"({bi.CreateDate:yyyy-MM-dd}) {bi.Title}:<br/>");
                                            }
                                            while (ma.Success)
                                            {
                                                var tar = ma.Groups[0].Value;
                                                File.AppendAllText(linkfile, $"<a target=\"_blank\" href=\"{tar}\">{tar}</a><br/>");
                                                ma = ma.NextMatch();
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        GlobalMethord.ErrorLog(ex.Message + linkfile);
                                    }
                                }
                            }
                        }
                        break;
                    case SiteType.Fantia:
                        break;
                    default:
                        {
                            foreach (var bi in _baseItems)
                            {
                                string sp = Path.Combine(VM_DD.SavePath, GlobalData.VM_MA.Artist.AName, $"{bi.CreateDate:yyyyMM\\\\dd_HHmm}_{bi.Title}");
                                try
                                {
                                    Directory.CreateDirectory(sp);
                                }
                                catch
                                {
                                    if (!Directory.Exists(sp))
                                    {
                                        sp = GlobalMethord.RemoveLastDot(GlobalMethord.ReplacePath(sp));
                                        Directory.CreateDirectory(sp);
                                    }
                                }
                                GlobalData.DLLogs.SetPId(bi.ID);
                                for (int i = 0; i < bi.ContentUrls.Count; i++)
                                {
                                    if (!GlobalData.DLLogs.HasLog(bi.ContentUrls[i], sp, bi.FileNames[i]))
                                    {
                                        di = new DownloadItem
                                        {
                                            FileName = bi.FileNames[i],
                                            Link = bi.ContentUrls[i],
                                            SavePath = sp,
                                            CTime = bi.CreateDate,
                                            SourceDocu = bi,
                                            AId = VM_DD.TempAI
                                        };
                                        di.CheckTempFile();
                                        downLoadItemList.Add(di);
                                    }
                                }
                                if (bi.Comments.Count > 0)
                                {
                                    try
                                    {
                                        var fp = Path.Combine(sp, "Comment.html");
                                        if (File.Exists(fp))
                                        {
                                            var cms = File.ReadAllLines(fp);
                                            if (cms.Except(bi.Comments).Count() == 0)
                                            {
                                                continue;
                                            }
                                        }
                                        File.WriteAllLines(Path.Combine(sp, "Comment.html"), bi.Comments);
                                        bi.Comments.ForEach(x =>
                                        {
                                            var ma = GlobalRegex.Regex_Url.Match(x);
                                            if (ma.Success)
                                            {
                                                File.AppendAllText(linkfile, $"({bi.CreateDate:yyyy-MM-dd}) {bi.Title}:<br/>");
                                            }
                                            while (ma.Success)
                                            {
                                                var tar = ma.Groups[0].Value;
                                                File.AppendAllText(linkfile, $"<a href=\"{tar}\">{tar}</a><br/>{Environment.NewLine}");
                                                ma = ma.NextMatch();
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        GlobalMethord.ErrorLog(ex.Message + linkfile);
                                    }
                                }
                            }
                        }
                        break;
                }
            });
            VM_DD.DownLoadItemList = downLoadItemList;
        }

        private void Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            GlobalData.VM_MA.MainSection = MainSection.Documents;
            VM_DD.ShowCheck = false;
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            VM_DD.ShowCheck = false;
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseD = true;
            _mouM = e.GetPosition(cas);
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseD)
            {
                var mm = e.GetPosition(cas);
                VM_DD.MLeft += (mm.X - _mouM.X);
                VM_DD.MTop += (mm.Y - _mouM.Y);
                _mouM = mm;
            }
        }

        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _mouseD = false;
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            _mouseD = false;
        }
    }
}
