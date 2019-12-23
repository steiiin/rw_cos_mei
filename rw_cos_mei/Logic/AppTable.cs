using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Security;
using Android.Security.Keystore;
using Java.Security;
using Java.Util;
using Javax.Crypto;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        public const string CRED_USERNAME = "cred_username";
        public const string CRED_PASSWORD = "cred_password";

        public const string PREF_SP_BEARER = "pref_sp_bearer";
        public const string PREF_SP_OAUTHT = "pref_sp_oauth";
        
        public const string PREF_SYNCINTERVAL = "pref_sync_interval";
        public const string PREF_SYNCNOTIFY = "pref_sync_notification";
        public const string PREF_LASTREFRESH = "pref_sync_lastrefresh";

        public const string PREF_BOTTOMNAV_ID = "pref_bottomnav_id";
        public const string PREF_FIRSTSTART = "pref_firststart";

        public const int PREF_FEED_AUTOREMOVE = 12;
        
        //###################################################################################

        public static string Username { get; private set; }
        public static string Password { get; private set; }

        public static string BearerToken { get; private set; }
        public static string OAuthToken { get; private set; }

        public static void UpdateCredentials(string username, string password)
        {
            Username = username;
            Password = password;

            SP_Object.SetCredentials(username, password);
            SaveSettings(cc);
        }
        public static void UpdateTokens(string token, string oauth)
        {
            BearerToken = token;
            OAuthToken = oauth;

            SaveSettings(cc);
        }

        //###################################################################################

        public enum SyncIntervalSetting
        {
            THREE_HOURS,
            TWO_A_DAY,
            ONE_A_DAY,
            ONE_IN_THREE_DAYS
        }
        public class SyncIntervalSettingDescriptor
        {
            public string Description { get; private set; }
            public int Timespan { get; private set; }
            public SyncIntervalSettingDescriptor(string description, int timespan)
            {
                Description = description;
                Timespan = timespan;
            }
        }

        //############################################################

        public static Notification.NotifySettings.NotifySettingsType NotificationType { get; private set; }
        public static SyncIntervalSetting SyncInterval { get; private set; }
        public static bool IsSyncBlocked { get; private set; } = false;
        
        public static SyncIntervalSettingDescriptor GetSyncIntervalSettingDescriptor(Context c, SyncIntervalSetting interval)
        {

            switch (interval)
            {
                case SyncIntervalSetting.THREE_HOURS:
                    return new SyncIntervalSettingDescriptor(c.GetString(Resource.String.settings_sync_time_op0), 
                                                             3 * 60 * 60 * 1000);

                case SyncIntervalSetting.TWO_A_DAY:
                    return new SyncIntervalSettingDescriptor(c.GetString(Resource.String.settings_sync_time_op1),
                                                             12 * 60 * 60 * 1000);

                case SyncIntervalSetting.ONE_A_DAY:
                    return new SyncIntervalSettingDescriptor(c.GetString(Resource.String.settings_sync_time_op2),
                                                             24 * 60 * 60 * 1000);

                case SyncIntervalSetting.ONE_IN_THREE_DAYS:
                    return new SyncIntervalSettingDescriptor(c.GetString(Resource.String.settings_sync_time_op3),
                                                             72 * 60 * 60 * 1000);

                default:
                    return new SyncIntervalSettingDescriptor(c.GetString(Resource.String.settings_sync_time_op2),
                                                             24 * 60 * 60 * 1000);
            }
            
        }
        
        public static void UpdateSyncInterval(SyncIntervalSetting interval)
        {
            SyncInterval = interval;
            SaveSettings(cc);
        }
        public static void UpdateSyncNotification(Notification.NotifySettings.NotifySettingsType type)
        {
            NotificationType = type;
            SaveSettings(cc);
        }

        public static void BlockSyncService() { IsSyncBlocked = true; }
        public static void UnBlockSyncService() { IsSyncBlocked = false; }
        
        //###################################################################################

        public static int BottomNavigationSelectedId { get; private set; }
        public static void UpdateBottomNavigationSelectedId(int id)
        {
            if(id == BottomNavigationSelectedId) { return; }

            BottomNavigationSelectedId = id;
        }

        public static DateTime LastTableRefresh { get; private set; }

        public static bool IsFirstStart { get; set; }

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
            DateTime oldOffset = DateTime.Now.AddMonths(-PREF_FEED_AUTOREMOVE);

            foreach (var item in list)
            {

                if(item.Date > oldOffset) { 

                    if(IsShiftsEntry(item, out ShiftsEntry shiftsItem))
                    {

                        //ShiftsEntry
                        if (!_tableShifts.ContainsKey(shiftsItem.Key))
                        {

                            _tableShifts.Add(shiftsItem.Key, shiftsItem);

                            notify.AddNewShifts(shiftsItem);

                            DB_Object.SaveShiftsEntry(shiftsItem, false);

                        }
                        else
                        {

                            DateTime oldTime = _tableShifts[shiftsItem.Key].LastUpdate;
                            if (oldTime < shiftsItem.LastUpdate)
                            {

                                int oldID = _tableShifts[shiftsItem.Key].ID;
                                shiftsItem.ID = oldID;
                                int oldAttachmentID = _tableShifts[shiftsItem.Key].ShiftAttachment.ID;
                                shiftsItem.ShiftAttachment.ID = oldAttachmentID;
                                _tableShifts[shiftsItem.Key] = shiftsItem;

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
                            notify.AddFeedEntry(item);

                            DB_Object.SaveFeedEntry(item);

                        }

                    }

                }

            }

            //Listen sortieren

            if (reportNew && NotificationType != Notification.NotifySettings.NotifySettingsType.NO_NOTIFICATION)
            {
                Notify_Object.CreateNotification(notify);
            }

            LastTableRefresh = DateTime.Now;
            SaveSettings(cc);

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

            if(feed.Count > 0 || shifts.Count > 0)
            {
                if(LastTableRefresh == DateTime.MinValue) { LastTableRefresh = DateTime.Now; SaveSettings(cc); }
            }

        }

        //###################################################################################

        public static bool IsFeedEmpty { get { if (FeedEntries == null) { return true; } else { return FeedEntries.Count == 0; } } }

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

        private static Context cc;

        public static SharepointAPI SP_Object;
        public static DataSource DB_Object;
        public static Notification Notify_Object;

        //###################################################################################

        public static void Init(Context context)
        {
            
            //Datenbank
            DB_Object = new DataSource(context);

            //Benachrichtungen
            Notify_Object = new Notification(context);
            
            //Einstellungen laden
            LoadSettings(context);

            //Sharepoint-API
            SP_Object = new SharepointAPI(context);
            SP_Object.SetCredentials(Username, Password);
            SP_Object.SetTokens(BearerToken, OAuthToken);

        }

        //###################################################################################

        public static void LoadSettings(Context context)
        {
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            cc = context;

            //Anmeldung
            string get_username = prefs.GetString(CRED_USERNAME, string.Empty);
            string get_password = prefs.GetString(CRED_PASSWORD, string.Empty);
            
            var se = new SecureEncryptor(context);
            Username = se.Decrypt(get_username);
            Password = se.Decrypt(get_password);
            
            BearerToken  = prefs.GetString(PREF_SP_BEARER, string.Empty);
            OAuthToken = prefs.GetString(PREF_SP_OAUTHT, string.Empty);
            
            //Listen
            _tableFeed = new Dictionary<string, FeedEntry>();
            _tableShifts = new Dictionary<string, ShiftsEntry>();
            
            LastTableRefresh = DecodeStringToDate(prefs.GetString(PREF_LASTREFRESH, string.Empty), DateTime.MinValue);

            //Sync-Einstellungen
            SyncInterval = (SyncIntervalSetting)prefs.GetInt(PREF_SYNCINTERVAL, (int)SyncIntervalSetting.ONE_A_DAY);

            //Benachrichtigung-Einstellungen
            NotificationType = (Notification.NotifySettings.NotifySettingsType)prefs.GetInt(PREF_SYNCNOTIFY, (int)Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS);
            IsFirstStart = prefs.GetBoolean(PREF_FIRSTSTART, true);

            //MainActivity
            BottomNavigationSelectedId = prefs.GetInt(PREF_BOTTOMNAV_ID, Resource.Id.menu_feed);
            
        }
        public static void SaveSettings(Context context)
        {
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            ISharedPreferencesEditor editor = prefs.Edit();

            var se = new SecureEncryptor(context);
            string enc_username = se.Encrypt(Username);
            string enc_password = se.Encrypt(Password);
            
            editor.PutString(CRED_USERNAME, enc_username);
            editor.PutString(CRED_PASSWORD, enc_password);

            editor.PutString(PREF_SP_BEARER, BearerToken);
            editor.PutString(PREF_SP_OAUTHT, OAuthToken);

            editor.PutInt(PREF_SYNCINTERVAL, (int)SyncInterval);
            editor.PutInt(PREF_SYNCNOTIFY, (int)NotificationType);
            editor.PutString(PREF_LASTREFRESH, EncodeDateToString(LastTableRefresh)); 

            editor.PutInt(PREF_BOTTOMNAV_ID, BottomNavigationSelectedId);
            editor.PutBoolean(PREF_FIRSTSTART, IsFirstStart);
            
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
                if (Helper.Converter.IsTextInteger(split[2]) && split[2].Length == 4)
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

        private const string currentDateFormat = "yyyy/MM/dd HH:mm:ss";

        public static string EncodeDateToString(DateTime date)
        {
            return date.ToString(currentDateFormat);
        }
        public static DateTime DecodeStringToDate(string date, DateTime defaultDate)
        {

            if(!DateTime.TryParseExact(date, currentDateFormat, null, DateTimeStyles.None, out DateTime decodedDate))
            {
                decodedDate = defaultDate;
            }
            return decodedDate;
            
        }

        //###################################################################################
        
        private class SecureEncryptor
        {

            private readonly Context _context;
            private KeyStore storeObject;

            private readonly IKey Key_private;
            private readonly IPublicKey Key_public;

            private readonly static string AndroidKeyStore = "AndroidKeyStore";
            private readonly static string KeyTransformation = "RSA/ECB/PKCS1Padding";

            private readonly static string KEYALIAS_CREDENTIALS = "CRED_KEY";

            //###############################################################################

            public SecureEncryptor(Context context)
            {

                _context = context;

                storeObject = KeyStore.GetInstance(AndroidKeyStore);
                storeObject.Load(null);


                if (!storeObject.ContainsAlias(KEYALIAS_CREDENTIALS))
                {
                    CreateKey_Credentials();
                }

                Key_private = storeObject.GetKey(KEYALIAS_CREDENTIALS, null);
                Key_public = storeObject.GetCertificate(KEYALIAS_CREDENTIALS)?.PublicKey;

            }

            private void CreateKey_Credentials()
            {

                var generator = KeyPairGenerator.GetInstance("RSA", AndroidKeyStore);

                if (Build.VERSION.SdkInt < BuildVersionCodes.M)
                {

                    Java.Util.Calendar calendar = Java.Util.Calendar.Instance;
                    calendar.Add(Java.Util.CalendarField.Year, 20);

                    Date startDate = Java.Util.Calendar.Instance.Time;
                    Date endDate = calendar.Time;

#pragma warning disable 0618

                    var builder = new KeyPairGeneratorSpec.Builder(_context);

#pragma warning restore 0618

                    builder.SetAlias(KEYALIAS_CREDENTIALS);
                    builder.SetSerialNumber(Java.Math.BigInteger.One);
                    builder.SetSubject(new Javax.Security.Auth.X500.X500Principal("CN=${alias} CA Certificate"));
                    builder.SetStartDate(startDate);
                    builder.SetEndDate(endDate);

                    generator.Initialize(builder.Build());

                }
                else
                {

                    var builder = new KeyGenParameterSpec.Builder(KEYALIAS_CREDENTIALS, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);
                    builder.SetBlockModes(KeyProperties.BlockModeEcb);
                    builder.SetEncryptionPaddings(KeyProperties.EncryptionPaddingRsaPkcs1);
                    generator.Initialize(builder.Build());

                }

                generator.GenerateKeyPair();

            }

            //###############################################################################

            private class CipherWrapper
            {

                private Cipher cipher;

                public CipherWrapper(string transformation)
                {
                    cipher = Cipher.GetInstance(transformation);
                }

                public string EncryptString(string data, IKey key)
                {
                    cipher.Init(Javax.Crypto.CipherMode.EncryptMode, key);
                    var bytes = cipher.DoFinal(new ASCIIEncoding().GetBytes(data));
                    return Convert.ToBase64String(bytes);

                }

                public string DecryptString(string data, IKey key)
                {
                    cipher.Init(Javax.Crypto.CipherMode.DecryptMode, key);
                    var encryptedData = Convert.FromBase64String(data);
                    var decodedData = cipher.DoFinal(encryptedData);
                    return new ASCIIEncoding().GetString(decodedData);
                }

            }

            //###############################################################################

            public string Encrypt(string klartext)
            {
                if(string.IsNullOrEmpty(klartext)) { return string.Empty; }
                return new CipherWrapper(KeyTransformation)?.EncryptString(klartext, Key_public);
            }
            public string Decrypt(string locktext)
            {
                if (string.IsNullOrEmpty(locktext)) { return string.Empty; }
                return new CipherWrapper(KeyTransformation)?.DecryptString(locktext, Key_private);
            }

        }
        
    }

}