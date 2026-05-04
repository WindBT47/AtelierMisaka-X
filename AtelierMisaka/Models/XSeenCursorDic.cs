using System;
using System.Collections.Generic;
using System.Linq;

namespace AtelierMisaka.Models
{
    public class XSeenCursorDic : Dictionary<string, XSeenCursor>
    {
        private static readonly char[] Splitter = new char[] { '|' };
        private SQLiteXSeenCursorHelper _sqlite = SQLiteXSeenCursorHelper.Instance;

        public XSeenCursorDic() : base()
        {
            LoadData();
        }

        public void LoadData()
        {
            var all = _sqlite.GetAll();
            if (all == null)
            {
                return;
            }
            Clear();
            foreach (var item in all)
            {
                this[item.CursorKey] = item;
            }
        }

        public bool TryGetWindow(string cursorKey, int windowSize, out List<string> seenIds)
        {
            if (base.TryGetValue(cursorKey, out XSeenCursor cursor))
            {
                seenIds = Parse(cursor.SeenIds, windowSize);
                return seenIds.Count > 0;
            }
            seenIds = new List<string>();
            return false;
        }

        public bool AddOrUpdate(string cursorKey, IEnumerable<string> seenIds, int windowSize)
        {
            if (string.IsNullOrEmpty(cursorKey))
            {
                return false;
            }

            var serialized = Serialize(seenIds, windowSize);
            if (string.IsNullOrEmpty(serialized))
            {
                return false;
            }

            if (base.TryGetValue(cursorKey, out XSeenCursor cursor))
            {
                cursor.SeenIds = serialized;
                cursor.UpdatedAt = DateTime.Now;
                return _sqlite.Update(cursor) > 0;
            }

            var item = new XSeenCursor(cursorKey, serialized);
            if (_sqlite.Insert(item) > 0)
            {
                this[cursorKey] = item;
                return true;
            }
            return false;
        }

        private static List<string> Parse(string text, int windowSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            return text.Split(Splitter, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .Take(windowSize)
                .ToList();
        }

        private static string Serialize(IEnumerable<string> seenIds, int windowSize)
        {
            if (seenIds == null)
            {
                return string.Empty;
            }

            return string.Join("|", seenIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(windowSize));
        }
    }
}
