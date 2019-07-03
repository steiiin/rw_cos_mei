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
///Activity_Main
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Activity(Label = "@string/app_name")]
    public class Activity_Main : AppCompatActivity
    {

        private const string BUNDLE_VIEWSWITCHER_INDEX = "bundle_viewflipper";
        public const string BUNDLE_BOTTOMID_INTENT = "bundle_bottomid_intent";

        private const int VIEWSWITCHER_FEED = 0;
        private const int VIEWSWITCHER_SHIFTS = 1;

        //###################################################################################

        private class ViewHolder
        {

            public BottomNavigationView BOTTOMNAVIGATION;
            public ViewSwitcher VIEWSWITCHER_LISTS;

            public View REFRESH_PROGRESS;
            public View REFRESH_PROGRESS_OVERLAY;
            public View REFRESH_DISABLE_OVERLAY;

            public LinearLayout LIST_FEED;
            public LinearLayout LIST_SHIFTS;

            public Adapters.ListFeedAdapter ADAPTER_FEED;
            public Adapters.ListShiftsAdapter ADAPTER_SHIFTS;

        }
        private ViewHolder c;

        //###################################################################################

        private bool onStartup;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            onStartup = false;

            //Layout füllen
            SetContentView(Resource.Layout.activity_main);
            CreateViewholder();
            CreateToolbar();

            CreateListAdapters();

            //Instance wiederherstellen
            if (savedInstanceState != null)
            {
                c.VIEWSWITCHER_LISTS.DisplayedChild = savedInstanceState.GetInt(BUNDLE_VIEWSWITCHER_INDEX);
            }
            else
            {
                onStartup = true;
            }

        }
        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            outState.PutInt(BUNDLE_VIEWSWITCHER_INDEX, c.VIEWSWITCHER_LISTS.DisplayedChild);
        }

        protected override void OnResume()
        {
            base.OnResume();

            //Oberfläche an Sharepoint-Status anpassen
            ViewStateChanger(TBL.SP_Object.State);

            //Refresh erst beginnen, wenn App startet
            if (onStartup)
            {
                onStartup = false;
                RefreshCloud();
            }

        }

        protected override void OnDestroy()
        {
            TBL.UnBlockSyncService();
            base.OnDestroy();
        }

        //###################################################################################

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return base.OnCreateOptionsMenu(menu);
        }
        public override bool OnOptionsItemSelected(IMenuItem item)
        {

            switch (item.ItemId)
            {
                case (Resource.Id.menu_sync):

                    if (TBL.SP_Object.State == SharepointAPIState.ERROR || TBL.SP_Object.State == SharepointAPIState.OK)
                    {

                        RefreshCloud();

                    }
                    break;

                case (Resource.Id.menu_settings):

                    StartSettings();
                    break;

            }

            return true;
        }

        private void StartSettings()
        {
            StartActivity(typeof(Activity_Settings));
        }

        //###################################################################################

        private void CreateViewholder()
        {

            //Viewholder
            c = new ViewHolder()
            {
                BOTTOMNAVIGATION = FindViewById<BottomNavigationView>(Resource.Id.main_bottomNavigation),
                VIEWSWITCHER_LISTS = FindViewById<ViewSwitcher>(Resource.Id.main_viewSwitcher),

                REFRESH_PROGRESS = FindViewById(Resource.Id.main_progress),
                REFRESH_PROGRESS_OVERLAY = FindViewById(Resource.Id.main_progress_overlay),
                REFRESH_DISABLE_OVERLAY = FindViewById(Resource.Id.main_login_overlay),

                LIST_FEED = FindViewById<LinearLayout>(Resource.Id.main_list_feed),
                LIST_SHIFTS = FindViewById<LinearLayout>(Resource.Id.main_list_shifts)
            };

            c.VIEWSWITCHER_LISTS.SetInAnimation(this, Resource.Animation.anim_slide_in_left);
            c.VIEWSWITCHER_LISTS.SetOutAnimation(this, Resource.Animation.anim_slide_out_right);
            
            //Events
            c.BOTTOMNAVIGATION.NavigationItemSelected += OnBottomNavigationItemSelected;
            c.BOTTOMNAVIGATION.SelectedItemId = TBL.BottomNavigationSelectedId;

            TBL.SP_Object.StateChanged += (s,e) => { ViewStateChanger(e.State); };

        }
        
        private void CreateToolbar()
        {

            //Toolbar erstellen
            var toolbar = FindViewById<Toolbar>(Resource.Id.appbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetDisplayShowCustomEnabled(true);
            SupportActionBar.SetDisplayShowTitleEnabled(false);

        }

        private void CreateListAdapters()
        {

            //Feed-Adpater
            c.ADAPTER_FEED = new Adapters.ListFeedAdapter(this, c.LIST_FEED);
            c.ADAPTER_FEED.ItemSelected += (sender, e) =>
            {

                //feedEntry öffnen
                var intent = new Intent();
                intent.SetClass(this, typeof(Activity_FeedEntry));
                intent.PutExtra(Activity_FeedEntry.BUNDLE_ENTRYKEY, e.EntryKey);

                StartActivity(intent);

            };

            //Shifts-Adapter
            c.ADAPTER_SHIFTS = new Adapters.ListShiftsAdapter(this, c.LIST_SHIFTS);
            c.ADAPTER_SHIFTS.ItemSelected += (sender, e) =>
            {

                EntryAttachment attachment = TBL.GetShiftsEntry(e.EntryKey).ShiftAttachment;
                FileOpen.Open(this, attachment.FileLocalUrl);

            };
            c.ADAPTER_SHIFTS.AttachmentRetrieveError += (sender, e) =>
            {

                //Snackbar aufrufen
                View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
                Snackbar snack = Snackbar.Make(rootView, "Anhang konnte nicht geladen werden. Internet?", Snackbar.LengthLong);
                snack.Show();

            };
            
            c.ADAPTER_FEED.Inflate();
            c.ADAPTER_SHIFTS.Inflate();

        }

        //###################################################################################

        private int _currentBottomId = -1;

        private void OnBottomNavigationItemSelected(object sender, BottomNavigationView.NavigationItemSelectedEventArgs e)
        {

            if(_currentBottomId == e.Item.ItemId) { return; }
            _currentBottomId = e.Item.ItemId;

            switch (e.Item.ItemId)
            {
                case Resource.Id.menu_feed:

                    c.VIEWSWITCHER_LISTS.SetInAnimation(this, Resource.Animation.anim_slide_in_left);
                    c.VIEWSWITCHER_LISTS.SetOutAnimation(this, Resource.Animation.anim_slide_out_right);

                    c.VIEWSWITCHER_LISTS.DisplayedChild = VIEWSWITCHER_FEED;
                    TBL.UpdateBottomNavigationSelectedId(Resource.Id.menu_feed);

                    break;
                case Resource.Id.menu_shifts:

                    c.VIEWSWITCHER_LISTS.SetInAnimation(this, Resource.Animation.anim_slide_in_right);
                    c.VIEWSWITCHER_LISTS.SetOutAnimation(this, Resource.Animation.anim_slide_out_left);

                    c.VIEWSWITCHER_LISTS.DisplayedChild = VIEWSWITCHER_SHIFTS;
                    TBL.UpdateBottomNavigationSelectedId(Resource.Id.menu_shifts);

                    break;
            }

            TBL.SaveSettings(this);

        }
        
        //###################################################################################

        private async void RefreshCloud()
        {
            TBL.BlockSyncService();
            await TBL.SP_Object.UpdateNewsFeed();
        }
        
        //###################################################################################

        private void ViewStateChanger(SharepointAPIState state)
        {

            AlertDialog dialogWrongLogin = null;
            AlertDialog dialogError = null;

            switch (state)
            {
                case SharepointAPIState.WORKING:
                case SharepointAPIState.LOGGED_IN:

                    TBL.BlockSyncService();

                    //Progressindikator anzeigen. Vollbild, wenn keine Daten.
                    if (c.ADAPTER_FEED.IsEmpty)
                    {
                        c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                        c.REFRESH_DISABLE_OVERLAY.Visibility = ViewStates.Gone;
                        c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Visible;
                    }
                    else
                    {
                        c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;
                        c.REFRESH_DISABLE_OVERLAY.Visibility = ViewStates.Visible;
                        c.REFRESH_PROGRESS.Visibility = ViewStates.Visible;
                    }

                    break;

                case SharepointAPIState.WRONG_LOGIN:

                    TBL.UnBlockSyncService();

                    //Dialog anzeigen, der in die Einstellungen führt.
                    dialogWrongLogin = new AlertDialog.Builder(this)
                        .SetTitle(Resource.String.main_dialog_login_title)
                        .SetMessage(Resource.String.main_dialog_login_msg)
                        .SetPositiveButton(Resource.String.dialog_toSettings, (ss, ee) => { })
                        .SetCancelable(false)
                        .Show();
                    dialogWrongLogin.GetButton((int)DialogButtonType.Positive).Click += delegate {
                        
                        dialogWrongLogin.Dismiss();
                        StartSettings();
                        
                    };

                    break;

                case SharepointAPIState.ERROR:

                    TBL.UnBlockSyncService();

                    c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                    c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;
                    c.REFRESH_DISABLE_OVERLAY.Visibility = ViewStates.Gone;

                    if (c.ADAPTER_FEED.IsEmpty)
                    {

                        //Dialog anzeigen, der WIEDERHOLEN anbietet.
                        dialogError = new AlertDialog.Builder(this)
                            .SetTitle(Resource.String.main_dialog_error_title)
                            .SetMessage(Resource.String.main_dialog_error_msg)
                            .SetPositiveButton(Resource.String.dialog_retry, (ss, ee) => { })
                            .SetCancelable(true)
                            .Show();
                        dialogError.GetButton((int)DialogButtonType.Positive).Click += delegate {

                            dialogError.Dismiss();
                            RefreshCloud();

                        };

                    }
                    else
                    {

                        c.REFRESH_DISABLE_OVERLAY.Visibility = ViewStates.Gone;

                        //Snackbar anzeigen, mit WIEDERHOLEN
                        View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
                        Snackbar snack = Snackbar.Make(rootView, Resource.String.main_dialog_error_msg, Snackbar.LengthLong);
                        snack.SetAction(Resource.String.dialog_retry, (ss) => { RefreshCloud(); });
                        snack.Show();

                    }

                    break;

                case SharepointAPIState.OK:

                    TBL.UnBlockSyncService();

                    if (dialogWrongLogin != null) { dialogWrongLogin.Dismiss(); }
                    if (dialogError != null) { dialogError.Dismiss(); }

                    c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                    c.REFRESH_DISABLE_OVERLAY.Visibility = ViewStates.Gone;
                    c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;

                    //Zeige Datenbank-Zeug
                    c.ADAPTER_FEED.Inflate();
                    c.ADAPTER_SHIFTS.Inflate();

                    break;

            }  

        }
        
    }

    namespace Adapters
    {

        public class ListFeedAdapter
        {

            private Context _context;
            private LinearLayout _container;

            //####################################################################################

            public event EventHandler<ListFeedAdapterItemSelectedEventArgs> ItemSelected;
            public bool IsEmpty { get { if (TBL.FeedEntries == null) { return true; } else { return TBL.FeedEntries.Count == 0; } } }

            //####################################################################################

            public ListFeedAdapter(Context context, LinearLayout container)
            {
                _context = context;
                _container = container;
            }

            public void Inflate()
            {

                _container.RemoveAllViews();

                //EntrieElemente erstellen & in die Liste einfügen
                List<FeedEntry> listUnread = new List<FeedEntry>();
                List<FeedEntry> listRead = new List<FeedEntry>();
                if(IsEmpty) { return; }

                foreach (var item in TBL.FeedEntries)
                {
                    if (item.MarkedRead)
                    {
                        listRead.Add(item);
                    }
                    else
                    {
                        listUnread.Add(item);
                    }
                }

                //Unread-Sektion
                if (listUnread.Count > 0)
                {
                    View section = CreateSectionNotMarkedRead(listUnread.Count);
                    _container.AddView(section);

                    CreateSectionFeedEntries(listUnread);
                }
                else
                {
                    View noUnread = CreateSectionNoUnread();
                    _container.AddView(noUnread);
                }

                //Older-Sektion
                if (listRead.Count > 0)
                {
                    View section = CreateSectionMarkedRead();
                    _container.AddView(section);

                    CreateSectionFeedEntries(listRead);
                }

            }

            //####################################################################################

            private View CreateSectionNotMarkedRead(int count)
            {

                View SECTION = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_unread, _container, false);

                var txt_unreadCount = SECTION.FindViewById<TextView>(Resource.Id.txt_unreadCount);
                var btn_markAllRead = SECTION.FindViewById(Resource.Id.btn_markAllRead);

                txt_unreadCount.Text = _context.Resources.GetString(Resource.String.main_list_feed_unread_title, count);
                btn_markAllRead.Click += (ss, ee) => { TBL.MarkReadFeedEntryAll(); Inflate(); };

                return SECTION;

            }
            private View CreateSectionMarkedRead()
            {

                View SECTION = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_read, _container, false);
                return SECTION;

            }
            private View CreateSectionNoUnread()
            {
                View SECTION = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_nounread, _container, false);
                return SECTION;
            }

            private enum SubheadType { YEAR, MONTH };
            private View CreateSubhead(SubheadType type, DateTime itemDate)
            {

                View SUBHEAD;

                switch (type)
                {
                    case SubheadType.YEAR:

                        SUBHEAD = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_year, _container, false);

                        var txt_year = SUBHEAD.FindViewById<TextView>(Resource.Id.txt_title);
                        txt_year.Text = itemDate.ToString("yyyy");

                        return SUBHEAD;

                    case SubheadType.MONTH:

                        SUBHEAD = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_month, _container, false);

                        var txt_month = SUBHEAD.FindViewById<TextView>(Resource.Id.txt_title);
                        txt_month.Text = itemDate.ToString("MMMM");

                        return SUBHEAD;

                    default:

                        SUBHEAD = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_month, _container, false);

                        var txt_empty = SUBHEAD.FindViewById<TextView>(Resource.Id.txt_title);
                        txt_empty.Text = string.Empty;

                        return SUBHEAD;

                }

            }

            private View CreateFeedEntry(FeedEntry item, bool showDivider)
            {

                View ITEM = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_item, _container, false);

                var txt_date = ITEM.FindViewById<TextView>(Resource.Id.txt_date);
                var txt_title = ITEM.FindViewById<TextView>(Resource.Id.txt_title);
                View item_divider = ITEM.FindViewById(Resource.Id.item_divider);

                //Daten füllen
                if (item.Date.Year == DateTime.Now.Year) { txt_date.Text = item.Date.ToString("dd. MMMM"); }
                else { txt_date.Text = item.Date.ToString("dd. MMMM yyyy"); }
                txt_title.Text = item.Title;

                if (showDivider) { item_divider.Visibility = ViewStates.Visible; }
                else { item_divider.Visibility = ViewStates.Gone; }

                if (item.MarkedRead) { ITEM.Alpha = 0.6F; }
                else { ITEM.Alpha = 1.0F; }

                //Touchevent
                ITEM.Click += (s, e) =>
                {
                    TBL.MarkReadFeedEntry(item.Key);
                    ItemSelected?.Invoke(this, new ListFeedAdapterItemSelectedEventArgs(item.Key));
                };

                return ITEM;

            }
            private void CreateSectionFeedEntries(List<FeedEntry> list)
            {

                bool showDivider = false; //ersten Divider ausblenden
                int lastMonth = -1;
                int lastYear = -1;

                foreach (FeedEntry item in list)
                {

                    //Monats-Subhead
                    if (lastMonth > 0 && item.Date.Month != lastMonth)
                    {
                        View SUBHEAD = CreateSubhead(SubheadType.MONTH, item.Date);
                        _container.AddView(SUBHEAD); showDivider = false;
                    }

                    //Jahres-Subhead
                    if (lastYear > 0 && item.Date.Year != lastYear)
                    {
                        View SUBHEAD = CreateSubhead(SubheadType.YEAR, item.Date);
                        _container.AddView(SUBHEAD); showDivider = false;
                    }
                    lastMonth = item.Date.Month; lastYear = item.Date.Year;

                    //Entry einfügen
                    View entry = CreateFeedEntry(item, showDivider);
                    _container.AddView(entry);

                }

            }

        }
        public class ListFeedAdapterItemSelectedEventArgs
        {
            public ListFeedAdapterItemSelectedEventArgs(string entryKey)
            {
                EntryKey = entryKey;
            }
            public string EntryKey { get; }
        }

        public class ListShiftsAdapter
        {

            private Context _context;
            private LinearLayout _container;

            //############################################################################

            public event EventHandler<ListShiftsAdapterItemSelectedEventArgs> ItemSelected;
            public event EventHandler AttachmentRetrieveError;

            //############################################################################

            public ListShiftsAdapter(Context context, LinearLayout container)
            {
                _context = context;
                _container = container;
            }

            public void Inflate()
            {

                _container.RemoveAllViews();

                bool showDivider = false; //ersten Divider ausblenden
                int lastYear = -1;

                foreach (ShiftsEntry item in TBL.ShiftsEntries)
                {

                    //Jahres-Subhead
                    if (lastYear > 0 && item.Year != lastYear)
                    {
                        View SUBHEAD = CreateSubhead(item.Year);
                        _container.AddView(SUBHEAD); showDivider = false;
                    }
                    lastYear = item.Year;

                    //Entry erstellen
                    View entry = CreateShiftsEntry(item, showDivider);
                    _container.AddView(entry);

                }

            }

            //###########################################################################

            private View CreateSubhead(int year)
            {

                View SUBHEAD = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_year, _container, false);

                var txt_year = SUBHEAD.FindViewById<TextView>(Resource.Id.txt_title);
                txt_year.Text = year.ToString("0000");

                return SUBHEAD;

            }

            private View CreateShiftsEntry(ShiftsEntry item, bool showDivider)
            {

                View ITEM = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_item, _container, false);

                var txt_title = ITEM.FindViewById<TextView>(Resource.Id.txt_title);
                var txt_update = ITEM.FindViewById<TextView>(Resource.Id.txt_lastupdate);
                var txt_version = ITEM.FindViewById<TextView>(Resource.Id.txt_lastversion);
                var item_divider = ITEM.FindViewById(Resource.Id.item_divider);
                var progress_attachDownload = ITEM.FindViewById(Resource.Id.progress_attachDownload);

                //Daten füllen
                txt_title.Text = item.Title;
                txt_update.Text = _context.Resources.GetString(Resource.String.main_list_feed_item_update, item.LastUpdate.ToString("dd. MMMM yyyy"));
                txt_version.Text = _context.Resources.GetString(Resource.String.main_list_feed_item_version, item.LastVersion);
                progress_attachDownload.Visibility = ViewStates.Gone;

                if (showDivider) { item_divider.Visibility = ViewStates.Visible; }
                else { item_divider.Visibility = ViewStates.Gone; showDivider = true; }

                if (item.MarkedRead) { ITEM.Alpha = 0.4F; }
                else { ITEM.Alpha = 1.0F; }

                //Touchevent
                ITEM.Click += (ss, ee) =>
                {
                    if(((View)ss).Tag != null) { return; }

                    ViewAttachment(item, new ProgressViewholer((View)ss, txt_version, progress_attachDownload));
                };

                return ITEM;

            }

            //###########################################################################

            private struct ProgressViewholer
            {
                public View SENDER { get; }
                public View TXT_VERSION { get; }
                public View PROGRESS_ATTACHDOWNLOAD { get; }
                public ProgressViewholer(View sender, View txt_version, View progress_attachDownload) { SENDER = sender; TXT_VERSION = txt_version; PROGRESS_ATTACHDOWNLOAD = progress_attachDownload; }
            }

            private void ViewAttachment(ShiftsEntry item, ProgressViewholer c)
            {

                c.SENDER.Tag = "BLOCKED";

                if (item.ShiftAttachment.IsAttachmentDownloaded)
                {
                    TBL.MarkReadShiftsEntry(item.Key);

                    c.SENDER.Tag = null;
                    ItemSelected?.Invoke(this, new ListShiftsAdapterItemSelectedEventArgs(item.Key, item.ShiftAttachment.Key));
                }
                else
                {

                    c.TXT_VERSION.Visibility = ViewStates.Gone;
                    c.PROGRESS_ATTACHDOWNLOAD.Visibility = ViewStates.Visible;

                    TBL.SP_Object.GetNewsFeedAttachment(item.ShiftAttachment,
                    delegate {

                        c.PROGRESS_ATTACHDOWNLOAD.Visibility = ViewStates.Gone;
                        c.TXT_VERSION.Visibility = ViewStates.Visible;

                        AttachmentRetrieveError?.Invoke(this, new EventArgs());

                    },
                    delegate (string localPath) {

                        c.PROGRESS_ATTACHDOWNLOAD.Visibility = ViewStates.Gone;
                        c.TXT_VERSION.Visibility = ViewStates.Visible;

                        item.ShiftAttachment.UpdateAttachment(localPath);
                        TBL.MarkReadShiftsEntry(item.Key);

                        c.SENDER.Tag = null;
                        c.SENDER.Alpha = 0.4F;

                        ItemSelected?.Invoke(this, new ListShiftsAdapterItemSelectedEventArgs(item.Key, item.ShiftAttachment.Key));

                    });

                }

            }

        }
        public class ListShiftsAdapterItemSelectedEventArgs
        {
            public ListShiftsAdapterItemSelectedEventArgs(string entryKey, string attachmentKey)
            {
                EntryKey = entryKey; AttachmentKey = attachmentKey;
            }
            public string EntryKey { get; }
            public string AttachmentKey { get; }
        }

    }

}