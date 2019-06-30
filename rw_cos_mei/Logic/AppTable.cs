using Android.Content;
using Android.Preferences;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///AppTable (TBL)
///>OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    public class AppTable
    {
        
        public const string SETTINGS_USERNAME = "settings_username";
        public const string SETTINGS_PASSWORD = "settings_password";

        public const string SETTINGS_SYNCINTERVAL = "settings_sync_interval";
        public const string SETTINGS_SYNCNOTIFY = "settings_sync_notification";

        public const string SETTINGS_BOTTOMNAV_ID = "settings_bottomnav_id";

        //###################################################################################

        public static string Username { get; private set; }
        public static string Password { get; private set; }

        public static void UpdateCredentials(string username, string password)
        {
            Username = username;
            Password = password;

            SP_Object.SetCredentials(username, password);
        }

        //###################################################################################

        public enum SyncIntervalSetting
        {
            TWO_A_DAY,
            ONE_A_DAY,
            ONE_IN_THREE_DAYS
        }

        //############################################################

        public static Notification.NotifySettings.NotifySettingsType NotificationType { get; private set; }
        public static SyncIntervalSetting SyncInterval { get; private set; }
        public static bool IsSyncBlocked { get; private set; } = false;
        
        public static string GetSyncIntervalSettingDescription(Context c, SyncIntervalSetting interval)
        {

            switch (interval)
            {
                case SyncIntervalSetting.TWO_A_DAY:
                    return c.GetString(Resource.String.settings_sync_time_op1);

                case SyncIntervalSetting.ONE_A_DAY:
                    return c.GetString(Resource.String.settings_sync_time_op2);

                case SyncIntervalSetting.ONE_IN_THREE_DAYS:
                    return c.GetString(Resource.String.settings_sync_time_op3);

                default:
                    return c.GetString(Resource.String.settings_sync_time_op2);
            }
            
        }
        public static int GetSyncIntervalSettingTiming(SyncIntervalSetting interval)
        {

            switch (interval)
            {
                case SyncIntervalSetting.TWO_A_DAY:
                    return 12 * 60 * 60 * 1000;

                case SyncIntervalSetting.ONE_A_DAY:
                    return 24 * 60 * 60 * 1000;

                case SyncIntervalSetting.ONE_IN_THREE_DAYS:
                    return 72 * 60 * 60 * 1000;

                default:
                    return 24 * 60 * 60 * 1000;
            }

        }
        
        public static void UpdateSyncInterval(SyncIntervalSetting interval)
        {
            SyncInterval = interval;
        }
        public static void UpdateSyncNotification(Notification.NotifySettings.NotifySettingsType type)
        {
            NotificationType = type;
        }

        public static void BlockSyncService() { IsSyncBlocked = true; }
        public static void UnBlockSyncService() { IsSyncBlocked = false; }
        
        //###################################################################################

        public static int BottomNavigationSelectedId { get; private set; }
        public static void UpdateBottomNavigationSelectedId(int id)
        {
            BottomNavigationSelectedId = id;
        }

        //###################################################################################

        private static Dictionary<string, FeedEntry> _tableFeed;
        private static Dictionary<string, ShiftsEntry> _tableShifts;

        public static void UpdateEntries(List<FeedEntry> list, bool reportNew = false)
        {

            if(!DB_Object.Open()) { return; }

            //Benachrichtungen bei neuen Sachen
            var notify = new Notification.NotifySettings(Notify_Object.NotificationContext, NotificationType);

            //Table erstellen
            if(_tableFeed == null) { _tableFeed = new Dictionary<string, FeedEntry>(); }
            if(_tableShifts == null) { _tableShifts = new Dictionary<string, ShiftsEntry>(); }

            //Rohentries verarbeiten
            foreach (var item in list)
            {

                if(IsShiftsEntry(item, out ShiftsEntry shiftsItem))
                {

                    //ShiftsEntry
                    if(!_tableShifts.ContainsKey(shiftsItem.Key))
                    {

                        _tableShifts.Add(shiftsItem.Key, shiftsItem);

                        notify.AddNewShifts(shiftsItem);

                        DB_Object.SaveShiftsEntry(shiftsItem, false);

                    }
                    else
                    {
                        DateTime oldTime = _tableShifts[shiftsItem.Key].LastUpdate;
                        if(oldTime < shiftsItem.LastUpdate)
                        {

                            int oldID = _tableShifts[shiftsItem.Key].ID;
                            _tableShifts[shiftsItem.Key] = shiftsItem;
                            shiftsItem.ID = oldID;

                            notify.AddNewShiftsVersion(shiftsItem);

                            DB_Object.SaveShiftsEntry(shiftsItem, true);

                        }
                    }

                }
                else
                {

                    //FeedEntry
                    if(!_tableFeed.ContainsKey(item.Key))
                    {

                        _tableFeed.Add(item.Key, item);
                        notify.AddFeedEntry();

                        DB_Object.SaveFeedEntry(item, false);

                    }

                }

            }

            if (reportNew && NotificationType != Notification.NotifySettings.NotifySettingsType.NO_NOTIFICATION)
            {
                Notify_Object.CreateNotification(notify);
            }
                
            DB_Object.Close();

        }
        public static void UpdateEntries(List<FeedEntry> feed, List<ShiftsEntry> shifts)
        {

            //AppTable wird aus der lokalen Datenbank befüllt

            //Table erstellen
            if (_tableFeed == null) { _tableFeed = new Dictionary<string, FeedEntry>(); }
            if (_tableShifts == null) { _tableShifts = new Dictionary<string, ShiftsEntry>(); }

            //Rohentries verarbeiten
            foreach (var item in feed)
            {

                //FeedEntry
                if (!_tableFeed.ContainsKey(item.Key))
                {
                    _tableFeed.Add(item.Key, item);
                }
                
            }
            foreach (var item in shifts)
            {

                //ShiftsEntry
                if (!_tableShifts.ContainsKey(item.Key))
                {
                    _tableShifts.Add(item.Key, item);
                }
                else
                {
                    DateTime oldTime = _tableShifts[item.Key].LastUpdate;
                    if (oldTime < item.LastUpdate)
                    {
                        _tableShifts[item.Key] = item;
                    }
                }

            }

        }

        //###################################################################################

        public static FeedEntry GetFeedEntry(string key)
        {
            if (_tableFeed == null || !_tableFeed.ContainsKey(key)) { return null; }
            return _tableFeed[key];
        }
        public static ShiftsEntry GetShiftsEntry(string key)
        {
            if (_tableShifts == null || !_tableShifts.ContainsKey(key)) { return null; }
            return _tableShifts[key];
        }

        public static List<FeedEntry> FeedEntries { get { return _tableFeed.Values.OrderByDescending(x => x.Date).ToList(); } }
        public static List<ShiftsEntry> ShiftsEntries { get { return _tableShifts.Values.OrderByDescending(x => x.Key).ToList(); } }

        //###################################################################################
        
        public static void MarkReadFeedEntryAll()
        {
            if (_tableFeed == null) { return; }
            
            //Alle FeedEntries als gelesen markieren
            foreach (var key in new List<string>(_tableFeed.Keys.ToList()))
            {
                if(!_tableFeed.ContainsKey(key)) { return; }

                var item = _tableFeed[key];
                item.MarkedRead = true;
                _tableFeed[key] = item;
                
            }

            //Die Einträge in der Datenbank ändern
            if(!DB_Object.Open()) { return; }
            DB_Object.MarkReadFeedEntryAll();
            DB_Object.Close();
            
        }
        public static void MarkReadFeedEntry(string key)
        {
            if (_tableFeed == null || !_tableFeed.ContainsKey(key)) { return; }

            //Einzelnes FeedItem als gelesen markieren
            var item = _tableFeed[key];
            item.MarkedRead = true;
            _tableFeed[key] = item;
            
            //Einzelnes Item in der Datenbank ändern
            if(!DB_Object.Open()) { return; }
            DB_Object.MarkReadFeedEntry(item);
            DB_Object.Close();

        }
        public static void MarkReadShiftsEntry(string key)
        {
            if (_tableShifts == null || !_tableShifts.ContainsKey(key)) { return; }

            //Einzelnes SchichtItem als gelesen markieren
            var item = _tableShifts[key];
            item.MarkedRead = true;
            _tableShifts[key] = item;

            //Datenbank ändern
            if(!DB_Object.Open()) { return; }
            DB_Object.MarkReadShiftsEntry(item);
            DB_Object.Close();

        }

        //###################################################################################

        public static SharepointAPI SP_Object;
        public static DataSource DB_Object;
        public static Notification Notify_Object;

        //###################################################################################

        public static void Init(Context context)
        {
            
            //Datenbank
            DB_Object = new DataSource(context);

            ////Benachrichtungen
            Notify_Object = new Notification(context);

            //Einstellungen laden
            LoadSettings(context);

            //Sharepoint-API
            SP_Object = new SharepointAPI(context);
            SP_Object.SetCredentials(Username, Password);

        }

        //###################################################################################

        public static void LoadSettings(Context context)
        {
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);

            //Anmeldung
            string get_username = prefs.GetString(SETTINGS_USERNAME, string.Empty);
            string get_password = prefs.GetString(SETTINGS_PASSWORD, string.Empty);

            var cs = new CredentialStore();
            cs.DecryptCredentials(get_username, get_password);

            Username = cs.Username;
            Password = cs.Password;
            
            //Listen
            _tableFeed = new Dictionary<string, FeedEntry>();
            _tableShifts = new Dictionary<string, ShiftsEntry>();

            //Sync-Einstellungen
            SyncInterval = (SyncIntervalSetting)prefs.GetInt(SETTINGS_SYNCINTERVAL, (int)SyncIntervalSetting.ONE_A_DAY);

            //Benachrichtigung-Einstellungen
            NotificationType = (Notification.NotifySettings.NotifySettingsType)prefs.GetInt(SETTINGS_SYNCNOTIFY, (int)Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS);

            //MainActivity
            BottomNavigationSelectedId = prefs.GetInt(SETTINGS_BOTTOMNAV_ID, Resource.Id.menu_feed);

        }
        public static void SaveSettings(Context context)
        {
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            ISharedPreferencesEditor editor = prefs.Edit();

            var cs = new CredentialStore();
            cs.EncryptCredentials(Username, Password);
            
            editor.PutString(SETTINGS_USERNAME, cs.EncryptedUsername); 
            editor.PutString(SETTINGS_PASSWORD, cs.EncryptedPassword);

            editor.PutInt(SETTINGS_SYNCINTERVAL, (int)SyncInterval);
            editor.PutInt(SETTINGS_SYNCNOTIFY, (int)NotificationType);

            editor.PutInt(SETTINGS_BOTTOMNAV_ID, BottomNavigationSelectedId);

            editor.Apply();
        }

        //###################################################################################

        private static bool IsShiftsEntry(FeedEntry item, out ShiftsEntry shiftEntry)
        {

            shiftEntry = null;
            if (item.Attachments.Count != 1) { return false; }

            string title = item.Title.ToLower().Trim();
            string[] split = title.Split(' ');
            if (split.Count() == 5 && (split[0] == "dp" || split[0] == "dienstplan") && split[3].StartsWith("ver"))  //DP/MONAT/JAHR/VERSION/NUMMER
            {

                //Monat
                int month = -1;
                switch (split[1].Substring(0, 3))
                {
                    case "jan":
                        month = 1;
                        break;

                    case "feb":
                        month = 2;
                        break;

                    case "mär":
                    case "mae":
                        month = 3;
                        break;

                    case "apr":
                        month = 4;
                        break;

                    case "mai":
                        month = 5;
                        break;

                    case "jun":
                        month = 6;
                        break;

                    case "jul":
                        month = 7;
                        break;

                    case "aug":
                        month = 8;
                        break;

                    case "sep":
                        month = 9;
                        break;

                    case "okt":
                        month = 10;
                        break;

                    case "nov":
                        month = 11;
                        break;

                    case "dez":
                        month = 12;
                        break;
                }
                if (month <= 0) { return false; }

                //Jahr
                if (Helper.IsTextInteger(split[2]) && split[2].Length == 4)
                {
                    int year = int.Parse(split[2]);
                    if (month == item.Date.Month && year != item.Date.Year) { year = item.Date.Year; } //Tippfehler ausmerzen

                    string version = split[4].Trim('.').Trim(' ');

                    shiftEntry = new ShiftsEntry(month, year, item.Date, version, item.Attachments.First());
                    return true;
                }

            }
            else if (split.Count() == 4 && (split[0] == "dp" || split[0] == "dienstplan") && split[2].StartsWith("ver") && split[3].Contains(".")) {

                //Monat
                int month = -1;
                switch (split[1].Substring(0, 3))
                {
                    case "jan":
                        month = 1;
                        break;

                    case "feb":
                        month = 2;
                        break;

                    case "mär":
                    case "mae":
                        month = 3;
                        break;

                    case "apr":
                        month = 4;
                        break;

                    case "mai":
                        month = 5;
                        break;

                    case "jun":
                        month = 6;
                        break;

                    case "jul":
                        month = 7;
                        break;

                    case "aug":
                        month = 8;
                        break;

                    case "sep":
                        month = 9;
                        break;

                    case "okt":
                        month = 10;
                        break;

                    case "nov":
                        month = 11;
                        break;

                    case "dez":
                        month = 12;
                        break;
                }
                if (month <= 0) { return false; }

                //Jahr
                int year = item.Date.Year;
                if((item.Date.AddMonths(3) < new DateTime(year, month, item.Date.Day))) { year -= 1; }

                string version = split[3].Trim('.').Trim(' ');

                shiftEntry = new ShiftsEntry(month, year, item.Date, version, item.Attachments.First());
                return true;

            }

            return false;
        }

        //###################################################################################

        private class CredentialStore
        {

            public CredentialStore()
            {

                Username = "";
                Password = "";
                EncryptedUsername = "";
                EncryptedPassword = "";

                try
                {
                    enc_salt = Encoding.ASCII.GetBytes(GetSalt);
                    enc_pass = GetPassword;
                }
                catch (Exception)
                {
                    enc_salt = Encoding.ASCII.GetBytes("salt_rw_cos_mei");
                    enc_pass = "com.steiiin.rw_mei_cos";
                }
                
            }

            //###################################################################################

            private string GetSalt { get { return Android.OS.Build.Id + Android.OS.Build.User + "########"; } }
            private string GetPassword { get { return Android.OS.Build.Serial + "com.steiiin.rw_cos_mei"; } }

            //###################################################################################

            private readonly string enc_pass;
            private readonly byte[] enc_salt;
            
            //###################################################################################

            private static class Crypto
            {
                
                public static string Encrypt(string clear_text, string pass, byte[] salt)
                {

                    try
                    {

                        var enc = GetEnc(pass, salt);
                        if (string.IsNullOrWhiteSpace(clear_text) || string.IsNullOrWhiteSpace(pass)) return "";

                        byte[] encryptedBytes;
                        using (ICryptoTransform encryptor = enc.CreateEncryptor(enc.Key, enc.IV))
                        {
                            byte[] bytesToEncrypt = Encoding.UTF8.GetBytes(clear_text);
                            encryptedBytes = InMemoryCrypt(bytesToEncrypt, encryptor);
                        }
                        return Convert.ToBase64String(encryptedBytes);

                    }
                    catch (Exception) { }

                    return "";
                    
                }
                public static string Decrypt(string enc_text, string pass, byte[] salt)
                {

                    try
                    {

                        var enc = GetEnc(pass, salt);
                        if (string.IsNullOrWhiteSpace(enc_text) || string.IsNullOrWhiteSpace(pass) || !IsBase64String(enc_text)) return "";

                        byte[] descryptedBytes;
                        using (ICryptoTransform decryptor = enc.CreateDecryptor(enc.Key, enc.IV))
                        {
                            byte[] encryptedBytes = Convert.FromBase64String(enc_text);
                            descryptedBytes = InMemoryCrypt(encryptedBytes, decryptor);
                        }
                        return Encoding.UTF8.GetString(descryptedBytes);

                    }
                    catch (Exception) { }
                    return "";

                }

                //###################################################################################
                
                private static byte[] InMemoryCrypt(byte[] data, ICryptoTransform transform)
                {

                    try
                    {

                        MemoryStream memory = new MemoryStream();
                        using (Stream stream = new CryptoStream(memory, transform, CryptoStreamMode.Write))
                        {
                            stream.Write(data, 0, data.Length);
                        }
                        return memory.ToArray();

                    }
                    catch (Exception)
                    {
                        return new MemoryStream().ToArray();
                    }
                   
                }
                private static RijndaelManaged GetEnc(string pass, byte[] salt)
                {

                    try
                    {

                        var key = new Rfc2898DeriveBytes(pass, salt);

                        var algorithm = new RijndaelManaged();
                        int bytesForKey = algorithm.KeySize / 8;
                        int bytesForIV = algorithm.BlockSize / 8;
                        algorithm.Key = key.GetBytes(bytesForKey);
                        algorithm.IV = key.GetBytes(bytesForIV);
                        return algorithm;

                    }
                    catch (Exception)
                    {
                        return null;
                    }
                                       
                }

                //###################################################################################

                private static bool IsBase64String(string s)
                {
                    try
                    {

                        s = s.Trim();
                        return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);

                    }
                    catch (Exception) { }
                    return false;
                }

            }

            //###################################################################################

            public void DecryptCredentials(string e_user, string e_pass)
            {

                Username = Crypto.Decrypt(e_user, enc_pass, enc_salt);
                Password = Crypto.Decrypt(e_pass, enc_pass, enc_salt);

                EncryptedUsername = e_user;
                EncryptedPassword = e_pass;

            }
            public void EncryptCredentials(string user, string pass)
            {

                EncryptedUsername = Crypto.Encrypt(user, enc_pass, enc_salt);
                EncryptedPassword = Crypto.Encrypt(pass, enc_pass, enc_salt);

                Username = user;
                Password = pass;

            }

            //###################################################################################

            public string Username { get; private set; }
            public string Password { get; private set; }

            public string EncryptedUsername { get; private set; }
            public string EncryptedPassword { get; private set; }

        }

    }

}