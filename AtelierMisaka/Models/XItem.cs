using System;

namespace AtelierMisaka.Models
{
    public class XItem : BaseItem
    {
        public string AuthorScreenName { get; set; } = string.Empty;
        public string TriggerAuthorScreenName { get; set; } = string.Empty;
        public DateTime? TriggerDate { get; set; }
        public XTweetType TweetType { get; set; } = XTweetType.Original;

        public string PID
        {
            set
            {
                if (_id != value)
                {
                    _id = value;
                    if (!string.IsNullOrEmpty(AuthorScreenName))
                    {
                        _link = $"https://x.com/{AuthorScreenName}/status/{_id}";
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public void SetDates(DateTime date)
        {
            _createDate = date;
            _updateDate = date;
            RaisePropertyChanged("CreateDate");
            RaisePropertyChanged("UpdateDate");
        }

        public string FileCount
        {
            get => string.Format(GlobalLanguage.Text_FileCou, _contentUrls.Count);
        }
    }
}
