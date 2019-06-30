using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;

using System.Collections.Generic;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///Notification
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    public class Notification
    {

        public const string CHANNEL_ID = "rw_cos_mei_notify_channel_default";
        public const int NOTIFICATION_ID = 9999;

        public Context NotificationContext { get; }

        //#################################################################################################

        public Notification(Context context)
        {

            NotificationContext = context;

            CreateNotificationChannel();

        }

        //#################################################################################################

        public class NotifySettings
        {

            public enum NotifySettingsType
            {
                NO_NOTIFICATION,
                ONLY_FEED,
                ONLY_SHIFTS,
                SHIFTS_AND_VERSIONS,
                FEED_AND_SHIFTS,
                FEED_AND_SHIFTS_AND_VERSIONS
            }

            //###########################################

            private NotifySettingsType _type;
            private Context _context;

            private int _feedCount = 0;
            private List<string> _newShifts = new List<string>();
            private List<string> _newShiftsVersion = new List<string>();

            //###########################################

            public NotifySettings(Context context, NotifySettingsType type)
            {
                _context = context;
                _type = type;

                _feedCount = 0;
                _newShifts = new List<string>();
                _newShiftsVersion = new List<string>();
            }
            public bool IsEmpty
            {
                get
                {

                    bool empty = true;

                    if (_type == NotifySettingsType.ONLY_FEED ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                    {
                        if(_feedCount > 0) { empty = false; }
                    }

                    if (_type == NotifySettingsType.ONLY_SHIFTS ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS ||
                        _type == NotifySettingsType.SHIFTS_AND_VERSIONS ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                    {
                        if(_newShifts.Count > 0) { empty = false; }
                    }

                    if (_type == NotifySettingsType.SHIFTS_AND_VERSIONS ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                    {
                        if(_newShiftsVersion.Count > 0) { empty = false; }
                    }

                    return empty;

                }
            }

            public void AddFeedEntry()
            {
                _feedCount += 1;
            }
            public void AddNewShifts(ShiftsEntry item)
            {
                string msg = _context.GetString(Resource.String.app_notify_msg_new) + " " + item.Title;
                _newShifts.Add(msg);
            }
            public void AddNewShiftsVersion(ShiftsEntry item)
            {
                string msg = _context.GetString(Resource.String.app_notify_msg_version) + " " + item.Title + " (" + item.LastVersion + ")";
                _newShiftsVersion.Add(msg);
            }

            //###########################################

            public NotificationCompat.InboxStyle GetInbox()
            {
                var inbox = new NotificationCompat.InboxStyle();

                if (_type == NotifySettingsType.ONLY_FEED ||
                   _type == NotifySettingsType.FEED_AND_SHIFTS ||
                   _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                {
                    if (_feedCount == 1)
                    {
                        inbox.AddLine("1 " + _context.GetString(Resource.String.app_notify_msg_feed_one));
                    }
                    else if (_feedCount >= 2)
                    {
                        inbox.AddLine(_feedCount + " " + _context.GetString(Resource.String.app_notify_msg_feed));
                    }
                }

                if (_type == NotifySettingsType.ONLY_SHIFTS ||
                   _type == NotifySettingsType.FEED_AND_SHIFTS ||
                   _type == NotifySettingsType.SHIFTS_AND_VERSIONS ||
                   _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                {
                    foreach (var item in _newShifts)
                    {
                        inbox.AddLine(item);
                    }
                }

                if (_type == NotifySettingsType.SHIFTS_AND_VERSIONS ||
                   _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                {
                    foreach (var item in _newShiftsVersion)
                    {
                        inbox.AddLine(item);
                    }
                }

                return inbox;

            }

        }

        public void CreateNotification(NotifySettings settings)
        {

            if(settings.IsEmpty) { return; }

            Intent intent = new Intent(NotificationContext, typeof(Activity_Main));
            intent.SetFlags(ActivityFlags.NewTask);
            PendingIntent target = PendingIntent.GetActivity(NotificationContext, 0, intent, 0);

            var notify = new NotificationCompat.Builder(NotificationContext, CHANNEL_ID);
            notify.SetAutoCancel(true);
            notify.SetSmallIcon(Resource.Drawable.ic_stat_icon);
            notify.SetContentIntent(target);
            notify.SetContentTitle(NotificationContext.Resources.GetString(Resource.String.app_notify_title));
            notify.SetStyle(settings.GetInbox());

            var notificationManager = NotificationManagerCompat.From(NotificationContext);
            notificationManager.Notify(NOTIFICATION_ID, notify.Build());

        }

        //#################################################################################################
        
        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                // Notification channels are new in API 26 (and not a part of the
                // support library). There is no need to create a notification 
                // channel on older versions of Android.
                return;
            }

            var name = NotificationContext.Resources.GetString(Resource.String.app_notify_channel_default);
            var description = NotificationContext.Resources.GetString(Resource.String.app_notify_channel_default_desc);
            var channel = new NotificationChannel(Notification.CHANNEL_ID, name, NotificationImportance.Default)
            {
                Description = description
            };

            var notificationManager = (NotificationManager)NotificationContext.GetSystemService(Activity.NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }


    }

}