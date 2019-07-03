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

            public Spinner SPINNER_TIME;

            public CheckBox CHECK_NOTIFY_NEWFEED;
            public CheckBox CHECK_NOTIFY_NEWSHIFTS;
            public CheckBox CHECK_NOTIFY_NEWSHIFTSVERSION;
            
        }
        private ViewHolder c;

        //###################################################################################

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Layout füllen
            SetContentView(Resource.Layout.activity_settings);
            CreateViewholder();
            CreateToolbar();
            CreateSpinner();

            //Oberfläche an Sharepoint-Status anpassen
            SP_StateChanged(null, new SharepointAPIStateChangedEventArgs(TBL.SP_Object.State));
            
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

                SPINNER_TIME = FindViewById<Spinner>(Resource.Id.spinner_sync_time),

                CHECK_NOTIFY_NEWFEED = FindViewById<CheckBox>(Resource.Id.check_noti_feed),
                CHECK_NOTIFY_NEWSHIFTS = FindViewById<CheckBox>(Resource.Id.check_noti_shifts),
                CHECK_NOTIFY_NEWSHIFTSVERSION = FindViewById<CheckBox>(Resource.Id.check_noti_shiftsVersion)
            };

            //Credentials-Button
            c.BTN_CRED.Click += LOGIN_BTN_Click;

            //Häufigkeit-Spinner
            c.SPINNER_TIME.ItemSelected += SPINNER_TIME_ItemSelected;

            //Benachrichtigungs-Checkboxen
            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.ONLY_FEED ||
                TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS ||
                TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWFEED.Checked = true; }

            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.ONLY_SHIFTS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.SHIFTS_AND_VERSIONS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWSHIFTS.Checked = true; }

            if (TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.SHIFTS_AND_VERSIONS ||
               TBL.NotificationType == Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS) { c.CHECK_NOTIFY_NEWSHIFTSVERSION.Checked = true; }

            c.CHECK_NOTIFY_NEWFEED.CheckedChange += CHECK_NOTIFY_CHANGED;
            c.CHECK_NOTIFY_NEWSHIFTS.CheckedChange += CHECK_NOTIFY_CHANGED;
            c.CHECK_NOTIFY_NEWSHIFTSVERSION.CheckedChange += CHECK_NOTIFY_CHANGED;

            //SharepointAPI
            TBL.SP_Object.StateChanged += SP_StateChanged;

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
                TBL.SaveSettings(this);

                JobSchedulerHelper.CreateSyncJob(this, TBL.GetSyncIntervalSettingTiming(interval));
            }

        }

        private void CHECK_NOTIFY_CHANGED(object sender, CompoundButton.CheckedChangeEventArgs e)
        {

            c.CHECK_NOTIFY_NEWSHIFTSVERSION.Enabled = c.CHECK_NOTIFY_NEWSHIFTS.Checked;
            var state = Notification.NotifySettings.NotifySettingsType.FEED_AND_SHIFTS_AND_VERSIONS;

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
            TBL.SaveSettings(this);

        }

        private void SP_StateChanged(object sender, SharepointAPIStateChangedEventArgs e)
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
                case SharepointAPIState.WRONG_LOGIN:
                case SharepointAPIState.ERROR:
                    c.BTN_CRED.Tag = null;
                    c.BTN_ICON_OK.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_WORKING.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_ERROR.Visibility = ViewStates.Visible;
                    
                    break;
                case SharepointAPIState.OK:
                case SharepointAPIState.LOGGED_IN:
                    c.BTN_CRED.Tag = null;
                    c.BTN_ICON_ERROR.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_WORKING.Visibility = ViewStates.Invisible;
                    c.BTN_ICON_OK.Visibility = ViewStates.Visible;
                    
                    break;
            }

            UpdateLoginButton();

        }
        
        //###################################################################################

        private void UpdateLoginButton()
        {

            if(string.IsNullOrWhiteSpace(TBL.Username))
            {
                c.BTN_CRED_USERNAME.Text = Resources.GetString(Resource.String.settings_cred_btn_noone);
            }
            else
            {
                c.BTN_CRED_USERNAME.Text = TBL.Username;
            }

        }

        private void LOGIN_BTN_Click(object sender, EventArgs e)
        {
            if(c.BTN_CRED.Tag != null) { return; } //Wenn State == Working

            var dialog = new Dialogs.DialogCredentialsInput(this,
                async (object ss, Dialogs.DialogCredentialsInputEventArgs ee) => 
                {
                    TBL.UpdateCredentials(ee.Username, ee.Password);
                    TBL.SaveSettings(this);
                    
                    await TBL.SP_Object.UpdateNewsFeed();
                });
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

