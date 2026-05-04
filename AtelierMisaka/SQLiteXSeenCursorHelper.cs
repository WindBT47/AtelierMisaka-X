using AtelierMisaka.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AtelierMisaka
{
    public class SQLiteXSeenCursorHelper
    {
        private SQLiteConnectionFactory _factory = SQLiteConnectionFactory.Instance;

        private static readonly object locker = new object();
        private static SQLiteXSeenCursorHelper _instance;
        public static SQLiteXSeenCursorHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (locker)
                    {
                        if (_instance == null)
                        {
                            _instance = new SQLiteXSeenCursorHelper();
                        }
                    }
                }
                return _instance;
            }
        }

        public SQLiteXSeenCursorHelper()
        {
            _factory.GetConnection().CreateTable<XSeenCursor>();
        }

        public List<XSeenCursor> GetAll()
        {
            try
            {
                using (var conn = _factory.GetConnection())
                {
                    return conn.Table<XSeenCursor>().ToList();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public int Insert(XSeenCursor cursor)
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

        public int Update(XSeenCursor cursor)
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
