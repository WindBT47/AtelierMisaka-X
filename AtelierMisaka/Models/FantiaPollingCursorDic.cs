using System;
using System.Collections.Generic;
using System.Linq;

namespace AtelierMisaka.Models
{
    public class FantiaPollingCursorDic : Dictionary<string, FantiaPollingCursor>
    {
        public const int WindowSize = 15;
        private static readonly char[] Splitter = new char[] { '|' };
        private SQLiteFantiaPollingCursorHelper _sqlite = SQLiteFantiaPollingCursorHelper.Instance;

        public FantiaPollingCursorDic() : base()
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
                this[item.CId] = item;
            }
        }

        public bool TryGetWindow(string cid, out List<string> pids)
        {
            if (base.TryGetValue(cid, out FantiaPollingCursor cursor))
            {
                pids = Parse(cursor.LastPid);
                return pids.Count > 0;
            }
            pids = new List<string>();
            return false;
        }

        public bool TryGetUpdatedAt(string cid, out DateTime updatedAt)
        {
            if (base.TryGetValue(cid, out FantiaPollingCursor cursor))
            {
                updatedAt = cursor.UpdatedAt;
                return true;
            }
            updatedAt = DateTime.MinValue;
            return false;
        }

        public bool AddOrUpdate(string cid, IEnumerable<string> pids)
        {
            if (string.IsNullOrEmpty(cid))
            {
                return false;
            }

            var serialized = Serialize(pids);
            if (string.IsNullOrEmpty(serialized))
            {
                return false;
            }

            if (base.TryGetValue(cid, out FantiaPollingCursor cursor))
            {
                cursor.LastPid = serialized;
                cursor.UpdatedAt = DateTime.Now;
                return _sqlite.Update(cursor) > 0;
            }

            var item = new FantiaPollingCursor(cid, serialized);
            if (_sqlite.Insert(item) > 0)
            {
                this[cid] = item;
                return true;
            }
            return false;
        }

        private static List<string> Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            return text.Split(Splitter, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .Take(WindowSize)
                .ToList();
        }

        private static string Serialize(IEnumerable<string> pids)
        {
            if (pids == null)
            {
                return string.Empty;
            }

            return string.Join("|", pids.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(WindowSize));
        }
    }
}
