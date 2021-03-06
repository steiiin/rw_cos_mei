﻿using Android.App;
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

            private readonly NotifySettingsType _type;
            private Context _context;

            private List<string> _newFeed = new List<string>();
            private List<string> _newShifts = new List<string>();
            private List<string> _newShiftsVersion = new List<string>();

            //###########################################

            public NotifySettings(Context context, NotifySettingsType type)
            {
                _context = context;
                _type = type;

                _newFeed = new List<string>();
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
                        if(_newFeed.Count > 0) { empty = false; }
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
            public bool HasOnlyShifts
            {
                get
                {
                    if (_type == NotifySettingsType.ONLY_FEED ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS ||
                        _type == NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS)
                    {
                        if (_newFeed.Count > 0) { return false; }
                    }
                    return true;
                }
            }

            public void AddFeedEntry(FeedEntry item)
            {
                _newFeed.Add(item.Title);
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

                    if(_newFeed.Count > 10)
                    {
                        inbox.AddLine(_newFeed.Count + " " + _context.GetString(Resource.String.app_notify_msg_feed));
                    }
                    else
                    {
                        foreach (var item in _newFeed)
                        {
                            inbox.AddLine(item);
                        }
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
            
            if(settings.HasOnlyShifts) { intent.PutExtra(Activity_Main.BUNDLE_BOTTOMID_INTENT, Resource.Id.menu_shifts); }
            else { intent.PutExtra(Activity_Main.BUNDLE_BOTTOMID_INTENT, Resource.Id.menu_feed); }

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

        public void CreateLoginNag()
        {

            Intent intent = new Intent(NotificationContext, typeof(Activity_Main));
            intent.SetFlags(ActivityFlags.NewTask);
            
            PendingIntent target = PendingIntent.GetActivity(NotificationContext, 0, intent, 0);

            var notify = new NotificationCompat.Builder(NotificationContext, CHANNEL_ID);
            notify.SetAutoCancel(true);
            notify.SetSmallIcon(Resource.Drawable.ic_stat_icon);
            notify.SetContentIntent(target);
            notify.SetContentTitle(NotificationContext.Resources.GetString(Resource.String.app_notify_title));
            notify.SetContentText(NotificationContext.Resources.GetString(Resource.String.app_notify_msg_login));

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