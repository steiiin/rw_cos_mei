using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

using System;
using System.Collections.Generic;

using AlertDialog = Android.Support.V7.App.AlertDialog;
using Toolbar = Android.Support.V7.Widget.Toolbar;

using TBL = rw_cos_mei.AppTable;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///Activity_Settings
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Activity(Label = "@string/app_name")]
    public class Activity_Settings : AppCompatActivity
    {
        
        private class ViewHolder
        {

            public View BTN_CRED;
            public TextView BTN_CRED_USERNAME;
            public View BTN_ICON_OK;
            public View BTN_ICON_ERROR;
            public View BTN_ICON_WORKING;

            public View CARD_WRONGLOGIN_HINT;
            public Button CARD_WRONGLOGIN_HINT_RETRY;

            public View CARD_CONNECTIONLOST_HINT;
            public Button CARD_CONNECTIONLOST_HINT_RETRY;

            public Spinner SPINNER_TIME;

            public CheckBox CHECK_NOTIFY_NEWFEED;
            public CheckBox CHECK_NOTIFY_NEWSHIFTS;
            public CheckBox CHECK_NOTIFY_NEWSHIFTSVERSION;

            public Button NOTIFICATION_LINK;
            
        }
        private ViewHolder c;

        //###################################################################################

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Layout füllen
            SetContentView(Resource.Layout.activity_settings);

            CreateViewholder();
            CreateSpinner();

            CreateToolbar();
                                    
        }

        protected override void OnResume()
        {

            //Statischen Speicher wiederherstellen, wenn im Hintergrund vom System gelöscht
            if (TBL.SP_Object == null)
            {
                Activity_Init.InitRoutine(this);
            }

            //Eventhandler hinzufügen
            CreateViewHandler(HandlerMethod.ADD_HANDLERS);

            //Oberfläche an SharepointState anpassen
            SP_Object_StateChanged(null, new SharepointAPIStateChangedEventArgs(TBL.SP_Object.State));

            base.OnResume();

        }
        protected override void OnPause()
        {

            //Eventhandler entfernen
            CreateViewHandler(HandlerMethod.REMOVE_HANDLERS);

            base.OnPause();

        }

        private void SP_Object_StateChanged(object sender, SharepointAPIStateChangedEventArgs e)
        {

            //Theme-Icon
            switch (e.State)
            {
                case SharepointAPIState.WORKING:
                    c.BTN_CRED.Tag = "#WORKING#";
                    c.BTN_ICON_OK.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_ERROR.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_WORKING.Visibility = ViewStates.Visible;

                    break;

                case SharepointAPIState.CONNECTION_LOST:
                case SharepointAPIState.WRONG_LOGIN:
                case SharepointAPIState.SERVER_ERROR:
                    c.BTN_CRED.Tag = null;
                    c.BTN_ICON_OK.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_WORKING.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_ERROR.Visibility = ViewStates.Visible;

                    break;
                case SharepointAPIState.OK:
                case SharepointAPIState.LOGGED_IN:
                case SharepointAPIState.OFFLINE:
                    c.BTN_CRED.Tag = null;
                    c.BTN_ICON_ERROR.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_WORKING.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_OK.Visibility = ViewStates.Visible;

                    break;
            }

            UpdateLoginButton();

        }

        //###################################################################################

        public override bool OnOptionsItemSelected(IMenuItem item)
        {

            switch (item.ItemId)
            {
                case (Android.Resource.Id.Home):
                    OnBackPressed();

                    break;
            }

            return true;
        }

        //###################################################################################

        private void CreateViewholder()
        {

            c = new ViewHolder()
            {
                BTN_CRED = FindViewById(Resource.Id.btn_credentialsInput),
                BTN_CRED_USERNAME = FindViewById<TextView>(Resource.Id.txt_credentialsInput_current),
                BTN_ICON_OK = FindViewById(Resource.Id.icon_credentialsInput_ok),
                BTN_ICON_ERROR = FindViewById(Resource.Id.icon_credentialsInput_error),
                BTN_ICON_WORKING = FindViewById(Resource.Id.icon_credentialsInput_working),

                CARD_WRONGLOGIN_HINT = FindViewById(Resource.Id.card_wronglogin_hint),
                CARD_WRONGLOGIN_HINT_RETRY = FindViewById<Button>(Resource.Id.card_wronglogin_retrybutton),
                CARD_CONNECTIONLOST_HINT = FindViewById(Resource.Id.card_connectionlost_hint),
                CARD_CONNECTIONLOST_HINT_RETRY = FindViewById<Button>(Resource.Id.card_connectionlost_retrybutton),

                SPINNER_TIME = FindViewById<Spinner>(Resource.Id.spinner_sync_time),

                CHECK_NOTIFY_NEWFEED = FindViewById<CheckBox>(Resource.Id.check_noti_feed),
                CHECK_NOTIFY_NEWSHIFTS = FindViewById<CheckBox>(Resource.Id.check_noti_shifts),
                CHECK_NOTIFY_NEWSHIFTSVERSION = FindViewById<CheckBox>(Resource.Id.check_noti_shiftsVersion),

                NOTIFICATION_LINK = FindViewById<Button>(Resource.Id.btn_notification_link)
            };
            
            //Benachrichtigungs-Checkboxen
            notification_checkbox_initiate = true;
            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.ONLY_FEED ||
                TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS ||
                TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWFEED.Checked = true; }

            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.ONLY_SHIFTS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.SHIFTS_AND_VERSIONS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWSHIFTS.Checked = true; }

            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.SHIFTS_AND_VERSIONS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked = true; }
            notification_checkbox_initiate = false;

            //Channellink
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                c.NOTIFICATION_LINK.Visibility = ViewStates.Visible;
            }
            else
            {
                c.NOTIFICATION_LINK.Visibility = ViewStates.Gone;
            }

        }
        private void CreateViewHandler(HandlerMethod method)
        {

            if(method == HandlerMethod.ADD_HANDLERS)
            {

                TBL.SP_Object.StateChanged += SP_Object_StateChanged;
                
                c.BTN_CRED.Click += CREDENTIAL_BTN_Click;
                c.CARD_WRONGLOGIN_HINT_RETRY.Click += CARD_WRONGLOGIN_HINT_RETRY_ClickAsync;

                c.SPINNER_TIME.ItemSelected += SPINNER_TIME_ItemSelected;
                
                c.CHECK_NOTIFY_NEWFEED.CheckedChange += CHECK_NOTIFY_CHANGED;
                c.CHECK_NOTIFY_NEWSHIFTS.CheckedChange += CHECK_NOTIFY_CHANGED;
                c.CHECK_NOTIFY_NEWSHIFTSVERSION.CheckedChange += CHECK_NOTIFY_CHANGED;

                c.NOTIFICATION_LINK.Click += NOTIFICATION_LINK_Click;

            }
            else
            {

                TBL.SP_Object.StateChanged -= SP_Object_StateChanged;

                c.BTN_CRED.Click -= CREDENTIAL_BTN_Click;
                c.CARD_WRONGLOGIN_HINT_RETRY.Click -= CARD_WRONGLOGIN_HINT_RETRY_ClickAsync;

                c.SPINNER_TIME.ItemSelected -= SPINNER_TIME_ItemSelected;

                c.CHECK_NOTIFY_NEWFEED.CheckedChange -= CHECK_NOTIFY_CHANGED;
                c.CHECK_NOTIFY_NEWSHIFTS.CheckedChange -= CHECK_NOTIFY_CHANGED;
                c.CHECK_NOTIFY_NEWSHIFTSVERSION.CheckedChange -= CHECK_NOTIFY_CHANGED;

                c.NOTIFICATION_LINK.Click -= NOTIFICATION_LINK_Click;

            }

            
            

            

        }
        
        private void CreateToolbar()
        {

            //Toolbar erstellen
            var toolbar = FindViewById<Toolbar>(Resource.Id.appbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetDisplayShowCustomEnabled(true);
            SupportActionBar.SetDisplayShowTitleEnabled(false);

            SupportActionBar.SetHomeAsUpIndicator(Resource.Drawable.ic_action_uparrow);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);

            try
            {
                TextView txt_version = FindViewById<TextView>(Resource.Id.txt_appversion);
                txt_version.Text = "Ver. " + Application.Context.ApplicationContext.PackageManager.GetPackageInfo(Application.Context.ApplicationContext.PackageName, 0).VersionName;
            }
            catch (Exception) { }
            
        }

        private List<TBL.SyncIntervalSetting> listSpinnerIntervalValues = new List<TBL.SyncIntervalSetting>() { TBL.SyncIntervalSetting.THREE_HOURS, TBL.SyncIntervalSetting.TWO_A_DAY, TBL.SyncIntervalSetting.ONE_A_DAY, TBL.SyncIntervalSetting.ONE_IN_THREE_DAYS };
        private void CreateSpinner()
        {

            List<string> intervalTitles = new List<string>();
            foreach (var item in listSpinnerIntervalValues) { intervalTitles.Add(TBL.GetSyncIntervalSettingDescription(this, item)); }

            ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, intervalTitles);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);

            c.SPINNER_TIME.Adapter = adapter;

            if(!listSpinnerIntervalValues.Contains(TBL.SyncInterval)) { return; }
            c.SPINNER_TIME.SetSelection(listSpinnerIntervalValues.IndexOf(TBL.SyncInterval));
                
        }

        //###################################################################################

        private void SPINNER_TIME_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {

            if ((e.Position >= 0) && (e.Position < listSpinnerIntervalValues.Count))
            {

                var interval = listSpinnerIntervalValues[e.Position];

                TBL.UpdateSyncInterval(interval);

                JobSchedulerHelper.CreateSyncJob(this, TBL.GetSyncIntervalSettingTiming(interval));
            }

        }

        private bool notification_checkbox_initiate = false;
        private void CHECK_NOTIFY_CHANGED(object sender, CompoundButton.CheckedChangeEventArgs e)
        {

            if(notification_checkbox_initiate) { return; }

            c.CHECK_NOTIFY_NEWSHIFTSVERSION.Enabled = c.CHECK_NOTIFY_NEWSHIFTS.Checked;
            var state = Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS;

            //Dienstplanversion deaktivieren, wenn aktiv
            if(!c.CHECK_NOTIFY_NEWSHIFTS.Checked && c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked) { c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked = false; }

            //Neuen Benachrichtungsstatus ermitteln
            if (c.CHECK_NOTIFY_NEWFEED.Checked)
            {
                if (c.CHECK_NOTIFY_NEWSHIFTS.Checked)
                {
                    if (c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked)
                    {
                        state = Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS;
                    }
                    else
                    {
                        state = Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS;
                    }
                }
                else
                {
                    state = Notification.NotifySettings.NotifySettingsType.ONLY_FEED;
                }
            }
            else
            {
                if (c.CHECK_NOTIFY_NEWSHIFTS.Checked)
                {
                    if (c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked)
                    {
                        state = Notification.NotifySettings.NotifySettingsType.SHIFTS_AND_VERSIONS;
                    }
                    else
                    {
                        state = Notification.NotifySettings.NotifySettingsType.ONLY_SHIFTS;
                    }
                }
                else
                {
                    state = Notification.NotifySettings.NotifySettingsType.NO_NOTIFICATION;
                }
            }

            TBL.UpdateSyncNotification(state);

        }
        
        private void NOTIFICATION_LINK_Click(object sender, EventArgs e)
        {

            if(Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {

                Intent link = new Intent(Android.Provider.Settings.ActionChannelNotificationSettings);
                link.PutExtra(Android.Provider.Settings.ExtraAppPackage, PackageName);
                link.PutExtra(Android.Provider.Settings.ExtraChannelId, rw_cos_mei.Notification.CHANNEL_ID);
                StartActivity(link);

            }
            
        }

        //###################################################################################

        private void UpdateLoginButton()
        {

            //Nutzername anpassen
            if(string.IsNullOrWhiteSpace(TBL.Username))
            {
                c.BTN_CRED_USERNAME.Text = Resources.GetString(Resource.String.settings_cred_btn_noone);
            }
            else
            {
                c.BTN_CRED_USERNAME.Text = TBL.Username;
            }

            //Wronglogin-Card
            switch (TBL.SP_Object.State)
            {
                case SharepointAPIState.OFFLINE:
                case SharepointAPIState.WORKING:
                case SharepointAPIState.LOGGED_IN:
                case SharepointAPIState.OK:

                    c.CARD_WRONGLOGIN_HINT.Visibility = ViewStates.Gone;
                    c.CARD_CONNECTIONLOST_HINT.Visibility = ViewStates.Gone;

                    break;

                case SharepointAPIState.SERVER_ERROR:
                case SharepointAPIState.WRONG_LOGIN:

                    c.CARD_CONNECTIONLOST_HINT.Visibility = ViewStates.Gone;

                    if (TBL.IsFeedEmpty)
                    {

                        c.CARD_WRONGLOGIN_HINT.Visibility = ViewStates.Gone;

                    }
                    else
                    { 

                        c.CARD_WRONGLOGIN_HINT.Visibility = ViewStates.Visible;

                    }

                    break;

                case SharepointAPIState.CONNECTION_LOST:

                    c.CARD_WRONGLOGIN_HINT.Visibility = ViewStates.Gone;
                    c.CARD_CONNECTIONLOST_HINT.Visibility = ViewStates.Visible;

                    break;

            }

        }
        private void CREDENTIAL_BTN_Click(object sender, EventArgs e)
        {
            if(c.BTN_CRED.Tag != null) { return; } //Wenn State == Working

            var dialog = new Dialogs.DialogCredentialsInput(this,
                async (object ss, Dialogs.DialogCredentialsInputEventArgs ee) => 
                {
                    TBL.UpdateCredentials(ee.Username, ee.Password);

                    int timeout = 30 * (1000 / 200);
                    while (TBL.SP_Object.State == SharepointAPIState.WORKING)
                    {
                        timeout -= 1;
                        if(timeout <= 0) { return; }

                        await System.Threading.Tasks.Task.Delay(200);
                    }

                    await TBL.SP_Object.UpdateNewsFeed();
                });
        }

        private async void CARD_WRONGLOGIN_HINT_RETRY_ClickAsync(object sender, EventArgs e)
        {
            if(TBL.SP_Object.State != SharepointAPIState.WORKING)
            {
                await TBL.SP_Object.UpdateNewsFeed();
            }
        }

    }
    
    namespace Dialogs
    {

        class DialogCredentialsInput
        {

            private Context _context;

            //##################################################################################

            private class ViewHolder
            {

                public AlertDialog dialog;

                public TextInputEditText TEXT_USERNAME;
                public TextInputEditText TEXT_PASSWORD;
                public TextInputLayout LAYOUT_USERNAME;
                public TextInputLayout LAYOUT_PASSWORD;

            }
            private ViewHolder con;

            //##################################################################################

            public DialogCredentialsInput(Context context, EventHandler<DialogCredentialsInputEventArgs> OnLoginEntered)
            {
                _context = context;

                //Dialogcontent erstellen
                con = new ViewHolder();
                LayoutInflater i = LayoutInflater.FromContext(context);
                View view = i.Inflate(Resource.Layout.dialog_login, null, false);

                con.TEXT_USERNAME = view.FindViewById<TextInputEditText>(Resource.Id.text_username); 
                con.TEXT_PASSWORD = view.FindViewById<TextInputEditText>(Resource.Id.text_password);
                con.LAYOUT_USERNAME = view.FindViewById<TextInputLayout>(Resource.Id.layout_username);
                con.LAYOUT_PASSWORD = view.FindViewById<TextInputLayout>(Resource.Id.layout_password);

                con.TEXT_USERNAME.Text = TBL.Username;
                con.TEXT_PASSWORD.Text = TBL.Password;

                //Dialog erstellen
                AlertDialog.Builder builder = new AlertDialog.Builder(context);
                builder.SetTitle(context.Resources.GetString(Resource.String.settings_dialog_cred_title));
                builder.SetMessage(context.Resources.GetString(Resource.String.settings_dialog_cred_msg));
                
                builder.SetNegativeButton(_context.Resources.GetString(Resource.String.dialog_cancel), (s,e) => { });
                builder.SetPositiveButton(_context.Resources.GetString(Resource.String.dialog_login), (s, e) => { });
               
                builder.SetView(view);
                con.dialog = builder.Show();

                con.dialog.GetButton((int)DialogButtonType.Positive).Click += (s, e) =>
                {

                    string user = con.TEXT_USERNAME.Text.ToLower();
                    string pass = con.TEXT_PASSWORD.Text;

                    bool valid = true;
                    if (string.IsNullOrWhiteSpace(user) || !user.EndsWith("malteser.org"))
                    {
                        valid = false;
                        con.LAYOUT_USERNAME.Error = context.Resources.GetString(Resource.String.settings_dialog_cred_error_email);
                    }
                    if (string.IsNullOrWhiteSpace(pass))
                    {
                        valid = false;
                        con.LAYOUT_PASSWORD.Error = context.Resources.GetString(Resource.String.settings_dialog_cred_error_pass);
                    }
                    if (valid)
                    {
                        con.LAYOUT_USERNAME.Error = string.Empty;
                        con.LAYOUT_PASSWORD.Error = string.Empty;
                    }
                    else
                    {
                        return;
                    }

                    OnLoginEntered?.Invoke(this, new DialogCredentialsInputEventArgs(con.TEXT_USERNAME.Text, con.TEXT_PASSWORD.Text));
                    con.dialog.Dismiss();

                };
                
            }
            
        }

        public class DialogCredentialsInputEventArgs
        {
            public DialogCredentialsInputEventArgs(string username, string password) { Username = username; Password = password; }
            public string Username { get; }
            public string Password { get; }
        }


    }

}

