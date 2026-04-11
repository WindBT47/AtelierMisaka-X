using AtelierMisaka.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AtelierMisaka
{
    public class SQLiteFantiaPollingCursorHelper
    {
        private SQLiteConnectionFactory _factory = SQLiteConnectionFactory.Instance;

        private static readonly object locker = new object();
        private static SQLiteFantiaPollingCursorHelper _instance;
        public static SQLiteFantiaPollingCursorHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (locker)
                    {
                        if (_instance == null)
                        {
                            _instance = new SQLiteFantiaPollingCursorHelper();
                        }
                    }
                }
                return _instance;
            }
        }

        public SQLiteFantiaPollingCursorHelper()
        {
            _factory.GetConnection().CreateTable<FantiaPollingCursor>();
        }

        public List<FantiaPollingCursor> GetAll()
        {
            try
            {
                using (var conn = _factory.GetConnection())
                {
                    return conn.Table<FantiaPollingCursor>().ToList();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public int Insert(FantiaPollingCursor cursor)
        {
            try
            {
                using (var conn = _factory.GetConnection())
                {
                    return conn.Insert(cursor);
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public int Update(FantiaPollingCursor cursor)
        {
            try
            {
                using (var conn = _factory.GetConnection())
                {
                    return conn.Update(cursor);
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
