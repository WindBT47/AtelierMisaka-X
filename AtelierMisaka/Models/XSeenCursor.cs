using SQLite;
using System;

namespace AtelierMisaka.Models
{
    public class XSeenCursor
    {
        private int _id = 0;
        private string _cursorKey = string.Empty;
        private string _seenIds = string.Empty;
        private DateTime _updatedAt = DateTime.Now;

        public XSeenCursor() { }

        public XSeenCursor(string cursorKey, string seenIds)
        {
            _cursorKey = cursorKey;
            _seenIds = seenIds;
            _updatedAt = DateTime.Now;
        }

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        [MaxLength(128)]
        public string CursorKey
        {
            get => _cursorKey;
            set
            {
                if (_cursorKey != value)
                {
                    _cursorKey = value;
                }
            }
        }

        [MaxLength(4096)]
        public string SeenIds
        {
            get => _seenIds;
            set
            {
                if (_seenIds != value)
                {
                    _seenIds = value;
                }
            }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (_updatedAt != value)
                {
                    _updatedAt = value;
                }
            }
        }
    }
}
