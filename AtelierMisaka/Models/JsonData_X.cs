namespace AtelierMisaka.Models
{
    public enum XTweetType
    {
        Original,
        Retweet,
        Quote
    }

    public class XMediaInfo
    {
        public string Url { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
    }
}
