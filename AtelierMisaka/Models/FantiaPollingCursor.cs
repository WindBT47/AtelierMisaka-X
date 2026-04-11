using SQLite;
using System;

namespace AtelierMisaka.Models
{
    public class FantiaPollingCursor
    {
        private int _id = 0;
        private string _cId = string.Empty;
        private string _lastPid = string.Empty;
        private DateTime _updatedAt = DateTime.Now;

        public FantiaPollingCursor() { }

        public FantiaPollingCursor(string cid, string lastPid)
        {
            _cId = cid;
            _lastPid = lastPid;
            _updatedAt = DateTime.Now;
        }

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        [MaxLength(20)]
        public string CId
        {
            get => _cId;
            set
            {
                if (_cId != value)
                {
                    _cId = value;
                }
            }
        }

        [MaxLength(512)]
        public string LastPid
        {
            get => _lastPid;
            set
            {
                if (_lastPid != value)
                {
                    _lastPid = value;
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
