using Android.Content;
using Android.Database;
using Android.Database.Sqlite;

using System;
using System.Collections.Generic;
using System.Linq;

using TBL = rw_cos_mei.AppTable;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///DatabaseHelper
///DataSource
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    public class DatabaseHelper : SQLiteOpenHelper
    {

        public const string DATABASE_NAME = "db_rw_cos_mei_feedsave";
        public const int DATABASE_VERSION = 1;

        public const string FEED_TABLE = "db_feed";
        public const string FEED_TABLE_COL_ID = "_id";
        public const string FEED_TABLE_COL_KEY = "key";
        public const string FEED_TABLE_COL_TITLE = "title";
        public const string FEED_TABLE_COL_BODY = "body";
        public const string FEED_TABLE_COL_AUTHOR = "author";
        public const string FEED_TABLE_COL_DATE = "date";
        public const string FEED_TABLE_COL_READ = "read";

        public const string SHIFT_TABLE = "db_shifts";
        public const string SHIFT_TABLE_COL_ID = "_id";
        public const string SHIFT_TABLE_COL_KEY = "key";
        public const string SHIFT_TABLE_COL_MONTH = "month";
        public const string SHIFT_TABLE_COL_YEAR = "year";
        public const string SHIFT_TABLE_COL_TITLE = "title";
        public const string SHIFT_TABLE_COL_UPDATE = "lastUpdate";
        public const string SHIFT_TABLE_COL_VERSION = "lastVersion";
        public const string SHIFT_TABLE_COL_READ = "read";

        public const string ATTACH_TABLE = "db_attachments";
        public const string ATTACH_TABLE_COL_ID = "_id";
        public const string ATTACH_TABLE_COL_KEY = "key";
        public const string ATTACH_TABLE_COL_OWNER = "owner";
        public const string ATTACH_TABLE_COL_OWNERID = "ownerID";
        public const string ATTACH_TABLE_COL_FILENAME = "filename";
        public const string ATTACH_TABLE_COL_REMOTE = "remote";
        public const string ATTACH_TABLE_COL_LOCAL = "local";

        public const string BOOL_TRUE = "true";
        public const string BOOL_FALSE = "false";

        public const string OWNER_FEED = "feed";
        public const string OWNER_SHIFTS = "shifts";

        public enum TABLE
        {
            FEED,
            SHIFTS,
            ATTACHMENT
        }

        public enum OWNER
        {
            FEED = 1,
            SHIFTS = 2
        }

        //################################################################################

        public DatabaseHelper(Context context) : base(context, DATABASE_NAME, null, DATABASE_VERSION) { }

        //################################################################################

        private string SQL_BUILDER_TABLE(TABLE type)
        {

            switch (type)
            {
                case TABLE.FEED:

                    return "CREATE TABLE " + FEED_TABLE +
                           "(" + FEED_TABLE_COL_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                                 FEED_TABLE_COL_KEY + " TEXT NOT NULL, " +
                                 FEED_TABLE_COL_TITLE + " TEXT NOT NULL, " +
                                 FEED_TABLE_COL_DATE + " INTEGER NOT NULL, " +
                                 FEED_TABLE_COL_AUTHOR + " TEXT NOT NULL, " +
                                 FEED_TABLE_COL_BODY + " TEXT NOT NULL, " +
                                 FEED_TABLE_COL_READ + " TEXT NOT NULL);";

                case TABLE.SHIFTS:

                    return "CREATE TABLE " + SHIFT_TABLE +
                           "(" + SHIFT_TABLE_COL_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                                 SHIFT_TABLE_COL_KEY + " TEXT NOT NULL, " +
                                 SHIFT_TABLE_COL_TITLE + " TEXT NOT NULL, " +
                                 SHIFT_TABLE_COL_MONTH + " INTEGER NOT NULL, " +
                                 SHIFT_TABLE_COL_YEAR + " INTEGER NOT NULL, " +
                                 SHIFT_TABLE_COL_UPDATE + " INTEGER NOT NULL, " +
                                 SHIFT_TABLE_COL_VERSION + " TEXT NOT NULL, " +
                                 SHIFT_TABLE_COL_READ + " TEXT NOT NULL);";

                case TABLE.ATTACHMENT:

                    return "CREATE TABLE " + ATTACH_TABLE +
                           "(" + ATTACH_TABLE_COL_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                                 ATTACH_TABLE_COL_KEY + " TEXT NOT NULL, " +
                                 ATTACH_TABLE_COL_OWNER + " INTEGER NOT NULL, " +
                                 ATTACH_TABLE_COL_OWNERID + " TEXT NOT NULL, " +
                                 ATTACH_TABLE_COL_FILENAME + " TEXT NOT NULL, " +
                                 ATTACH_TABLE_COL_REMOTE + " TEXT NOT NULL, " +
                                 ATTACH_TABLE_COL_LOCAL + " TEXT);";

            }

            return "";

        }

        //################################################################################

        public override void OnCreate(SQLiteDatabase db)
        {

            db.ExecSQL(SQL_BUILDER_TABLE(TABLE.FEED));
            db.ExecSQL(SQL_BUILDER_TABLE(TABLE.SHIFTS));
            db.ExecSQL(SQL_BUILDER_TABLE(TABLE.ATTACHMENT));

        }
        public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion)
        {

            db.ExecSQL("DROP TABLE IF EXISTS " + FEED_TABLE);
            db.ExecSQL("DROP TABLE IF EXISTS " + SHIFT_TABLE);
            db.ExecSQL("DROP TABLE IF EXISTS " + ATTACH_TABLE);

            OnCreate(db);

        }

    }

    public class DataSource : IDisposable
    {

        private SQLiteDatabase database;
        private DatabaseHelper dbHelper;

        //###############################################################################

        private readonly string[] FEED_COLUMNS =
        {
            DatabaseHelper.FEED_TABLE_COL_ID,
            DatabaseHelper.FEED_TABLE_COL_KEY,
            DatabaseHelper.FEED_TABLE_COL_TITLE,
            DatabaseHelper.FEED_TABLE_COL_DATE,
            DatabaseHelper.FEED_TABLE_COL_AUTHOR,
            DatabaseHelper.FEED_TABLE_COL_BODY,
            DatabaseHelper.FEED_TABLE_COL_READ
        };
        private readonly string[] SHIFTS_COLUMNS =
        {
            DatabaseHelper.SHIFT_TABLE_COL_ID,
            DatabaseHelper.SHIFT_TABLE_COL_KEY,
            DatabaseHelper.SHIFT_TABLE_COL_MONTH,
            DatabaseHelper.SHIFT_TABLE_COL_YEAR,
            DatabaseHelper.SHIFT_TABLE_COL_TITLE,
            DatabaseHelper.SHIFT_TABLE_COL_UPDATE,
            DatabaseHelper.SHIFT_TABLE_COL_VERSION,
            DatabaseHelper.SHIFT_TABLE_COL_READ
        };
        private readonly string[] ATTACH_COLUMNS =
        {
            DatabaseHelper.ATTACH_TABLE_COL_ID,
            DatabaseHelper.ATTACH_TABLE_COL_KEY,
            DatabaseHelper.ATTACH_TABLE_COL_OWNER,
            DatabaseHelper.ATTACH_TABLE_COL_OWNERID,
            DatabaseHelper.ATTACH_TABLE_COL_FILENAME,
            DatabaseHelper.ATTACH_TABLE_COL_REMOTE,
            DatabaseHelper.ATTACH_TABLE_COL_LOCAL
        };

        private const int OLDOFFSET_DELETE_MONTHS = 12;

        //###############################################################################
        
        public void SaveFeedEntry(FeedEntry item, bool overwrite)
        {

            //Datensatz erstellen
            string read = DatabaseHelper.BOOL_FALSE;
            if (item.MarkedRead) { read = DatabaseHelper.BOOL_TRUE; }

            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.FEED_TABLE_COL_KEY, item.Key);
            values.Put(DatabaseHelper.FEED_TABLE_COL_TITLE, item.Title);
            values.Put(DatabaseHelper.FEED_TABLE_COL_DATE, item.Date.ToShortDateString());
            values.Put(DatabaseHelper.FEED_TABLE_COL_AUTHOR, item.Author);
            values.Put(DatabaseHelper.FEED_TABLE_COL_BODY, item.Body);
            values.Put(DatabaseHelper.FEED_TABLE_COL_READ, read);

            //Datenbank aktualisieren
            if (item.ID < 0)
            {

                //Neuen Eintrag anlegen
                int ID = (int)database.Insert(DatabaseHelper.FEED_TABLE, null, values);
                item.ID = ID;

            }
            else
            {

                if(overwrite) { 
                
                    //Bestehenden Eintrag überschreiben
                    string id_filter = DatabaseHelper.FEED_TABLE_COL_ID + "=" + item.ID;
                    database.Update(DatabaseHelper.FEED_TABLE, values, id_filter, null);

                }

            }

            //Feed-Anhänge aktualisieren
            foreach (var attachment in item.Attachments)
            {
                SaveAttachment(attachment, DatabaseHelper.OWNER_FEED, item.ID, overwrite);
            }
            
        }
        public void SaveShiftsEntry(ShiftsEntry item, bool overwrite)
        {

            //Datensatz erstellen
            string read = DatabaseHelper.BOOL_FALSE;
            if (item.MarkedRead) { read = DatabaseHelper.BOOL_TRUE; }

            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_KEY, item.Key);
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_TITLE, item.Title);
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_MONTH, item.Month);
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_YEAR, item.Year);
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_UPDATE, item.LastUpdate.ToString("s"));
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_VERSION, item.LastVersion);
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_READ, read);

            //Datenbank aktualisieren
            if (item.ID < 0)
            {

                //Neuen Eintrag anlegen
                int ID = (int)database.Insert(DatabaseHelper.SHIFT_TABLE, null, values);
                item.ID = ID;

            }
            else
            {

                if(overwrite) { 

                    //Bestehenden Eintrag überschreiben
                    string id_filter = DatabaseHelper.SHIFT_TABLE_COL_ID + "=" + item.ID;
                    database.Update(DatabaseHelper.SHIFT_TABLE, values, id_filter, null);

                }

            }

            //Feed-Anhänge aktualisieren
            SaveAttachment(item.ShiftAttachment, DatabaseHelper.OWNER_SHIFTS, item.ID, overwrite);

        }
        public void SaveAttachment(EntryAttachment item, string owner, int ownerID, bool overwrite)
        {

            //Datensatz erstellen
            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_KEY, item.Key);
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_OWNER, owner);
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_OWNERID, ownerID);
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_FILENAME, item.FileName);
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_REMOTE, item.FileRemoteUrl);
            values.Put(DatabaseHelper.ATTACH_TABLE_COL_LOCAL, item.FileLocalUrl);

            //Datenbank aktualisieren
            if (item.ID < 0)
            {

                //Neuen Eintrag anlegen
                int ID = (int)database.Insert(DatabaseHelper.ATTACH_TABLE, null, values);
                item.ID = ID;

            }
            else
            {

                if(overwrite) { 

                    //Bestehenden Eintrag überschreiben
                    string id_filter = DatabaseHelper.ATTACH_TABLE_COL_ID + "=" + item.ID;
                    database.Update(DatabaseHelper.ATTACH_TABLE, values, id_filter, null);

                }

            }

        }

        public void MarkReadFeedEntryAll()
        {

            if(!Open()) { return; }

            string read = DatabaseHelper.BOOL_TRUE;

            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.FEED_TABLE_COL_READ, read);

            database.Update(DatabaseHelper.FEED_TABLE, values, null, null);

            Close();

        }
        public void MarkReadFeedEntry(FeedEntry item)
        {

            string read = DatabaseHelper.BOOL_FALSE;
            if (item.MarkedRead) { read = DatabaseHelper.BOOL_TRUE; }

            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.FEED_TABLE_COL_READ, read);

            //Neuen Eintrag erstellen
            if (item.ID < 0)
            {

                SaveFeedEntry(item, false);

            }

            //Bestehenden Eintrag aktualisieren
            else
            {
                
                string id_filter = DatabaseHelper.FEED_TABLE_COL_ID + "=" + item.ID;
                database.Update(DatabaseHelper.FEED_TABLE, values, id_filter, null);

            }

        }
        public void MarkReadShiftsEntry(ShiftsEntry item)
        {

            string read = DatabaseHelper.BOOL_FALSE;
            if (item.MarkedRead) { read = DatabaseHelper.BOOL_TRUE; }

            ContentValues values = new ContentValues();
            values.Put(DatabaseHelper.SHIFT_TABLE_COL_READ, read);

            //Neuen Eintrag erstellen
            if (item.ID < 0)
            {

                SaveShiftsEntry(item, false);

            }

            //Bestehenden Eintrag aktualisieren
            else
            {

                string id_filter = DatabaseHelper.SHIFT_TABLE_COL_ID + "=" + item.ID;
                database.Update(DatabaseHelper.SHIFT_TABLE, values, id_filter, null);

            }

        }

        //###############################################################################

        public void LoadDatabase()
        {

            //Datenbank öffnen & Listen erstellen
            if (!Open()) { return; }

            List<FeedEntry> listFeed = new List<FeedEntry>();
            List<ShiftsEntry> listShifts = new List<ShiftsEntry>();

            List<int> oldFeed = new List<int>(); List<int> oldShift = new List<int>();
            DateTime oldOffset = DateTime.Now.AddMonths(-OLDOFFSET_DELETE_MONTHS);
            var dictAttachments = GetStoredAttachments();

            //Feed laden
            ICursor c = database.Query(DatabaseHelper.FEED_TABLE, FEED_COLUMNS, null, null, null, null, null); c.MoveToFirst();
            while (!c.IsAfterLast)
            {
                FeedEntry fE = GetStoredFeedEntry(c, dictAttachments);
                if (fE.Date <= oldOffset) { oldFeed.Add(fE.ID); }
                else { listFeed.Add(fE); }

                c.MoveToNext();
            }
            c.Close();

            //Shifts laden
            c = database.Query(DatabaseHelper.SHIFT_TABLE, SHIFTS_COLUMNS, null, null, null, null, null); c.MoveToFirst();
            while (!c.IsAfterLast)
            {
                ShiftsEntry sE = GetStoredShiftsEntry(c, dictAttachments);
                if (sE.LastUpdate <= oldOffset) { oldShift.Add(sE.ID); }
                else { listShifts.Add(sE); }

                c.MoveToNext();
            }
            c.Close();

            //Datenbank bereinigen
            RemoveOldEntries(oldFeed, oldShift);

            //Datenbank schließen
            Close();

            //Statischen Speicher befüllen
            TBL.UpdateEntries(listFeed, listShifts);

        }
        
        //###############################################################################

        private Dictionary<string, Dictionary<int, List<EntryAttachment>>> GetStoredAttachments()
        {

            Dictionary<string, Dictionary<int, List<EntryAttachment>>> dictAttachments = new Dictionary<string, Dictionary<int, List<EntryAttachment>>>();

            ICursor c = database.Query(DatabaseHelper.ATTACH_TABLE, ATTACH_COLUMNS, null, null, null, null, null);
            c.MoveToFirst();

            while (!c.IsAfterLast)
            {

                int ID_sql_id = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_ID);
                int ID_key = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_KEY);
                int ID_owner = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_OWNER);
                int ID_ownerID = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_OWNERID);
                int ID_filename = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_FILENAME);
                int ID_remotePath = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_REMOTE);
                int ID_localPath = c.GetColumnIndex(DatabaseHelper.ATTACH_TABLE_COL_LOCAL);

                int sql_id = c.GetInt(ID_sql_id);
                string key = c.GetString(ID_key);
                string owner = c.GetString(ID_owner);
                int ownerID = c.GetInt(ID_ownerID);
                string filename = c.GetString(ID_filename);
                string remotePath = c.GetString(ID_remotePath);
                string localPath = c.GetString(ID_localPath);

                EntryAttachment aE = new EntryAttachment(key, filename, remotePath, localPath) { ID = sql_id };

                if (!dictAttachments.ContainsKey(owner)) { dictAttachments.Add(owner, new Dictionary<int, List<EntryAttachment>>()); }
                if (!dictAttachments[owner].ContainsKey(ownerID)) { dictAttachments[owner].Add(ownerID, new List<EntryAttachment>()); }
                dictAttachments[owner][ownerID].Add(aE);

                c.MoveToNext();
            }

            c.Close();
            return dictAttachments;

        }
        private FeedEntry GetStoredFeedEntry(ICursor c, Dictionary<string, Dictionary<int, List<EntryAttachment>>> dictAttachments)
        {

            int ID_sql_id = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_ID);
            int ID_key = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_KEY);
            int ID_title = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_TITLE);
            int ID_date = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_DATE);
            int ID_author = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_AUTHOR);
            int ID_body = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_BODY);
            int ID_read = c.GetColumnIndex(DatabaseHelper.FEED_TABLE_COL_READ);

            int sql_id = c.GetInt(ID_sql_id);
            string key = c.GetString(ID_key);
            string title = c.GetString(ID_title);
            DateTime date = DateTime.Parse(c.GetString(ID_date));
            string author = c.GetString(ID_author);
            string body = c.GetString(ID_body);
            string read = c.GetString(ID_read);

            var listAttachments = new List<EntryAttachment>();
            if (dictAttachments.ContainsKey(DatabaseHelper.OWNER_FEED) && dictAttachments[DatabaseHelper.OWNER_FEED].ContainsKey(sql_id))
            {
                listAttachments = dictAttachments[DatabaseHelper.OWNER_FEED][sql_id];
            }

            FeedEntry result = new FeedEntry(key, title, body, date, author, listAttachments) { MarkedRead = (read == DatabaseHelper.BOOL_TRUE), ID = sql_id };
            return result;

        }
        private ShiftsEntry GetStoredShiftsEntry(ICursor c, Dictionary<string, Dictionary<int, List<EntryAttachment>>> dictAttachments)
        {

            int ID_sql_id = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_ID);
            int ID_key = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_KEY);
            int ID_month = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_MONTH);
            int ID_year = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_YEAR);
            int ID_title = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_TITLE);
            int ID_update = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_UPDATE);
            int ID_version = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_VERSION);
            int ID_read = c.GetColumnIndex(DatabaseHelper.SHIFT_TABLE_COL_READ);

            int sql_id = c.GetInt(ID_sql_id);
            string key = c.GetString(ID_key);
            int month = c.GetInt(ID_month);
            int year = c.GetInt(ID_year);
            string title = c.GetString(ID_title);
            DateTime update = DateTime.Parse(c.GetString(ID_update));
            string version = c.GetString(ID_version);
            string read = c.GetString(ID_read);

            var listAttachments = dictAttachments[DatabaseHelper.OWNER_SHIFTS][sql_id];

            ShiftsEntry result = new ShiftsEntry(month, year, update, version, listAttachments.First()) { MarkedRead = (read == DatabaseHelper.BOOL_TRUE), ID = sql_id };
            return result;

        }

        private void RemoveOldEntries(List<int> oldFeed, List<int> oldShifts)
        {

            string connector = " AND ";

            //Feed löschen
            if (oldFeed.Count > 0)
            {
                string feed_filter = "";
                string feed_attach = "";
                foreach (int sql_id in oldFeed)
                {
                    feed_filter += DatabaseHelper.FEED_TABLE_COL_ID + "=" + sql_id.ToString() + connector;
                    feed_attach += DatabaseHelper.ATTACH_TABLE_COL_OWNERID + "=" + sql_id.ToString() + connector;
                }
                feed_filter = feed_filter.Substring(0, feed_filter.Length - connector.Length);
                database.Delete(DatabaseHelper.FEED_TABLE, feed_filter, null);
                database.Delete(DatabaseHelper.ATTACH_TABLE, feed_attach + DatabaseHelper.ATTACH_TABLE_COL_OWNER + "=" + "\"" + DatabaseHelper.OWNER_FEED + "\"", null);
            }

            //Shifts löschen
            if (oldShifts.Count > 0)
            {
                string shifts_filter = "";
                string shifts_attach = "";
                foreach (int sql_id in oldShifts)
                {
                    shifts_filter += DatabaseHelper.SHIFT_TABLE_COL_ID + "=" + sql_id.ToString() + connector;
                    shifts_attach += DatabaseHelper.ATTACH_TABLE_COL_OWNERID + "=" + sql_id.ToString() + connector;
                }
                shifts_filter = shifts_filter.Substring(0, shifts_filter.Length - connector.Length);
                database.Delete(DatabaseHelper.SHIFT_TABLE, shifts_filter, null);
                database.Delete(DatabaseHelper.ATTACH_TABLE, shifts_attach + DatabaseHelper.ATTACH_TABLE_COL_OWNER + "=\"" + DatabaseHelper.OWNER_SHIFTS + "\"", null);
            }

        }

        //###############################################################################

        public DataSource(Context context)
        {
            dbHelper = new DatabaseHelper(context);
        }

        //###############################################################################

        public bool Open()
        {
            try
            {
                database = dbHelper.WritableDatabase;
                return database != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public void Close()
        {
            if (dbHelper == null) { return; }
            try
            {
                dbHelper.Close();
            }
            catch (Exception)
            { }

        }

        //###############################################################################

        #region IDisposable Support

        private bool disposedValue = false; // Dient zur Erkennung redundanter Aufrufe.

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                }
                
                disposedValue = true;
            }
        }
        
        public void Dispose()
        {

            Dispose(true);

        }

        #endregion

    }
    
}