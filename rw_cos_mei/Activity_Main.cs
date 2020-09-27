using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using rw_cos_mei.Helper;
using System;
using System.Collections.Generic;
using AlertDialog = Android.Support.V7.App.AlertDialog;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using TBL = rw_cos_mei.AppTable;
using Toolbar = Android.Support.V7.Widget.Toolbar;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///Activity_Main
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.Splash")]
    public class Activity_Main : AppCompatActivity
    {

        public const string BUNDLE_BOTTOMID_INTENT = "bundle_bottomid_intent";

        //###################################################################################

        private class ViewHolder
        {

            public TextView TOOLBAR_REFRESHTIMER;
            public Android.Support.V4.Widget.SwipeRefreshLayout REFRESH_SWIPE;

            public BottomNavigationView BOTTOMNAVIGATION;
            public ViewPager VIEWPAGER;
            public Adapters.ViewPagerAdapter VIEWPAGER_ADAPTER;

            public View REFRESH_PROGRESS;
            public View REFRESH_PROGRESS_OVERLAY;

        }
        private ViewHolder c;

        //###################################################################################

        protected override void OnCreate(Bundle savedInstanceState)
        {

            //Statischen Speicher wiederherstellen, wenn im Hintergrund vom System gelöscht
            if (TBL.SP_Object == null)
            {
                Activity_Init.InitRoutine(this);
            }

            SetTheme(Resource.Style.AppTheme);
            base.OnCreate(savedInstanceState);

            //Layout füllen
            SetContentView(Resource.Layout.activity_main);
            CreateViewholder();
            CreateToolbar();

        }
        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
        }

        protected override void OnResume()
        {
            base.OnResume();

            //Eventhandler erstellen
            CreateViewHandler(HandlerMethod.ADD_HANDLERS);

            //SharepointStatus
            ViewStateChanger(TBL.SP_Object.State);

            //Refresh erst beginnen, wenn App startet
            if ((DateTime.Now - TBL.LastTableRefresh).TotalMinutes > 30 || TBL.IsFeedEmpty)
            {
                RefreshCloud();
            }

        }
        protected override void OnPause()
        {

            CreateViewHandler(HandlerMethod.REMOVE_HANDLERS);

            base.OnPause();
        }

        protected override void OnStop()
        {
            TBL.UnBlockSyncService();
            TBL.SaveSettings(this);
            base.OnStop();
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

                    RefreshCloud();

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
        private async void RefreshCloud()
        {
            if (TBL.SP_Object.State != SharepointAPIState.WORKING)
            {

                TBL.BlockSyncService();
                await TBL.SP_Object.UpdateNewsFeed();

            }
        }

        //###################################################################################

        private void CreateViewholder()
        {

            //Viewholder
            c = new ViewHolder()
            {
                TOOLBAR_REFRESHTIMER = FindViewById<TextView>(Resource.Id.main_lastrefresh),

                BOTTOMNAVIGATION = FindViewById<BottomNavigationView>(Resource.Id.main_bottomNavigation),
                VIEWPAGER = FindViewById<ViewPager>(Resource.Id.main_viewPager),

                REFRESH_PROGRESS = FindViewById(Resource.Id.main_progress),
                REFRESH_PROGRESS_OVERLAY = FindViewById(Resource.Id.main_progress_overlay)
            };

            CreateListFragments();

            //Events
            c.BOTTOMNAVIGATION.NavigationItemSelected += OnBottomNavigationItemSelected;
            c.BOTTOMNAVIGATION.SelectedItemId = TBL.BottomNavigationSelectedId;

        }
        private void CreateListFragments()
        {

            Adapters.ViewPagerAdapter adapter = new Adapters.ViewPagerAdapter(SupportFragmentManager);
            c.VIEWPAGER_ADAPTER = adapter;
            c.VIEWPAGER.Adapter = adapter;

            c.VIEWPAGER_ADAPTER.Inflate();

        }

        private void CreateViewHandler(HandlerMethod method)
        {

            if (method == HandlerMethod.ADD_HANDLERS)
            {

                //Oberfläche an Sharepoint-Status anpassen
                TBL.SP_Object.StateChanged += SP_Object_StateChanged;

                //ViewPager
                c.VIEWPAGER.PageSelected += VIEWPAGER_PageSelected;

                c.VIEWPAGER_ADAPTER.FeedItemSelected += LIST_FEED_ItemSelected;

                c.VIEWPAGER_ADAPTER.ShiftsItemSelected += LIST_SHIFTS_ItemSelected;
                c.VIEWPAGER_ADAPTER.ShiftsItemAttachmentRetrieveError += LIST_SHIFTS_AttachmentRetrieveError;

                c.VIEWPAGER_ADAPTER.RefreshCloudRequested += VIEWPAGER_ADAPTER_RequestRefresh;

            }
            else
            {

                //Dialoge schließen
                if (dialogError != null) { dialogError.Dispose(); dialogError = null; }
                if (dialogWrongLogin != null) { dialogWrongLogin.Dispose(); dialogWrongLogin = null; }

                //Oberfläche an Sharepoint-Status anpassen
                TBL.SP_Object.StateChanged -= SP_Object_StateChanged;

                //ViewPager
                c.VIEWPAGER.PageSelected -= VIEWPAGER_PageSelected;

                c.VIEWPAGER_ADAPTER.FeedItemSelected -= LIST_FEED_ItemSelected;
                c.VIEWPAGER_ADAPTER.ShiftsItemSelected -= LIST_SHIFTS_ItemSelected;
                c.VIEWPAGER_ADAPTER.ShiftsItemAttachmentRetrieveError -= LIST_SHIFTS_AttachmentRetrieveError;

                c.VIEWPAGER_ADAPTER.RefreshCloudRequested -= VIEWPAGER_ADAPTER_RequestRefresh;

            }

        }

        private void VIEWPAGER_ADAPTER_RequestRefresh(object sender, EventArgs e)
        {

            c.REFRESH_SWIPE = (Android.Support.V4.Widget.SwipeRefreshLayout)sender;
            RefreshCloud();

        }

        private void CreateToolbar()
        {

            //Toolbar erstellen
            var toolbar = FindViewById<Toolbar>(Resource.Id.appbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetDisplayShowCustomEnabled(true);
            SupportActionBar.SetDisplayShowTitleEnabled(false);

            UpdateRefreshTimer();

        }
        private void UpdateRefreshTimer()
        {

            string timer = GetString(Resource.String.main_norefresh);

            if (TBL.LastTableRefresh != DateTime.MinValue)
            {
                if (TBL.LastTableRefresh.Day == DateTime.Now.Day && TBL.LastTableRefresh.Month == DateTime.Now.Month && TBL.LastTableRefresh.Year == DateTime.Now.Year)
                {
                    timer = TBL.LastTableRefresh.ToString("t");
                }
                else
                {
                    timer = TBL.LastTableRefresh.ToString("g");
                }
            }

            c.TOOLBAR_REFRESHTIMER.Text = GetString(Resource.String.main_list_feed_item_update, timer);

        }

        //###################################################################################

        IMenuItem BOTTOMNAV_PREVITEM;

        private void OnBottomNavigationItemSelected(object sender, BottomNavigationView.NavigationItemSelectedEventArgs e)
        {

            switch (e.Item.ItemId)
            {
                case Resource.Id.menu_feed:

                    TBL.UpdateBottomNavigationSelectedId(Resource.Id.menu_feed);
                    c.VIEWPAGER.SetCurrentItem(0, true);

                    break;
                case Resource.Id.menu_shifts:

                    TBL.UpdateBottomNavigationSelectedId(Resource.Id.menu_shifts);
                    c.VIEWPAGER.SetCurrentItem(1, true);

                    break;
            }

        }
        private void VIEWPAGER_PageSelected(object sender, ViewPager.PageSelectedEventArgs e)
        {

            if (BOTTOMNAV_PREVITEM != null)
            {
                BOTTOMNAV_PREVITEM.SetChecked(false);
            }
            else
            {
                c.BOTTOMNAVIGATION.Menu.GetItem(0).SetChecked(false);
            }

            c.BOTTOMNAVIGATION.Menu.GetItem(e.Position).SetChecked(true);
            BOTTOMNAV_PREVITEM = c.BOTTOMNAVIGATION.Menu.GetItem(e.Position);
            TBL.UpdateBottomNavigationSelectedId(c.BOTTOMNAVIGATION.Menu.GetItem(e.Position).ItemId);

            if (c.REFRESH_SWIPE != null && c.REFRESH_SWIPE.Refreshing) { c.REFRESH_SWIPE.Refreshing = false; }

        }

        //###################################################################################

        private void SP_Object_StateChanged(object sender, SharepointAPIStateChangedEventArgs e)
        {
            ViewStateChanger(e.State);
        }
        private void ViewStateChanger(SharepointAPIState state)
        {

            //Progress
            if (state == SharepointAPIState.WORKING)
            {

                TBL.BlockSyncService();

                //Progressindikator anzeigen. Vollbild, wenn keine Daten.
                if (TBL.IsFeedEmpty)
                {
                    c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                    c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Visible;
                }
                else
                {
                    c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;
                    c.REFRESH_PROGRESS.Visibility = ViewStates.Visible;
                }

            }
            else
            {
                c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;
                if (c.REFRESH_SWIPE != null) { c.REFRESH_SWIPE.Refreshing = false; }
            }

            //Status
            switch (state)
            {
                case SharepointAPIState.WRONG_LOGIN:

                    TBL.UnBlockSyncService();

                    if (dialogError != null && dialogError.IsShowing) { dialogError.Dismiss(); }
                    if (dialogWrongLogin != null && dialogWrongLogin.IsShowing) { dialogWrongLogin.Dismiss(); }

                    //Dialog anzeigen, der in die Einstellungen führt.
                    int title = Resource.String.main_dialog_wronglogin_title;
                    int msg = Resource.String.main_dialog_wronglogin_msg;
                    if (TBL.IsFeedEmpty)
                    {
                        title = Resource.String.main_dialog_nologin_title;
                        msg = Resource.String.main_dialog_nologin_msg;
                    }

                    dialogWrongLogin = new AlertDialog.Builder(this)
                        .SetTitle(title)
                        .SetMessage(msg)
                        .SetPositiveButton(Resource.String.dialog_toSettings, (ss, ee) =>
                        {
                            TBL.SaveSettings(this);
                            StartSettings();
                        })
                        .SetCancelable(false)
                        .Show();

                    break;

                case SharepointAPIState.SERVER_ERROR:

                    TBL.UnBlockSyncService();

                    if (dialogError != null && dialogError.IsShowing) { dialogError.Dismiss(); }
                    if (dialogWrongLogin != null && dialogWrongLogin.IsShowing) { dialogWrongLogin.Dismiss(); }

                    //Dialog anzeigen, der WIEDERHOLEN anbietet.
                    dialogError = new AlertDialog.Builder(this)
                        .SetTitle(Resource.String.main_dialog_error_title)
                        .SetMessage(Resource.String.main_dialog_error_msg)
                        .SetPositiveButton(Resource.String.dialog_retry, (ss, ee) => { RefreshCloud(); })
                        .SetCancelable(false)
                        .Show();

                    break;

                case SharepointAPIState.CONNECTION_LOST:

                    TBL.UnBlockSyncService();

                    if (dialogError != null && dialogError.IsShowing) { dialogError.Dismiss(); }
                    if (dialogWrongLogin != null && dialogWrongLogin.IsShowing) { dialogWrongLogin.Dismiss(); }

                    if (TBL.IsFeedEmpty)
                    {

                        //Dialog anzeigen, der WIEDERHOLEN anbietet.
                        dialogError = new AlertDialog.Builder(this)
                            .SetTitle(Resource.String.main_dialog_connect_title)
                            .SetMessage(Resource.String.main_dialog_connect_msg)
                            .SetPositiveButton(Resource.String.dialog_retry, (ss, ee) => { RefreshCloud(); })
                            .SetCancelable(false)
                            .Show();

                    }
                    else
                    {

                        //Snackbar anzeigen, mit WIEDERHOLEN
                        View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
                        Snackbar snack = Snackbar.Make(rootView, Resource.String.main_snack_connect, Snackbar.LengthLong);
                        snack.SetAction(Resource.String.dialog_retry, (ss) => { RefreshCloud(); });
                        snack.Show();

                    }

                    break;

                case SharepointAPIState.LOGGED_IN:

                    if (dialogError != null && dialogError.IsShowing) { dialogError.Dismiss(); }
                    if (dialogWrongLogin != null && dialogWrongLogin.IsShowing) { dialogWrongLogin.Dismiss(); }

                    break;

                case SharepointAPIState.OK:
                case SharepointAPIState.OFFLINE:

                    TBL.UnBlockSyncService();

                    if (dialogError != null && dialogError.IsShowing) { dialogError.Dismiss(); }
                    if (dialogWrongLogin != null && dialogWrongLogin.IsShowing) { dialogWrongLogin.Dismiss(); }

                    UpdateRefreshTimer();

                    //Zeige Datenbank-Zeug
                    c.VIEWPAGER_ADAPTER.Inflate();

                    if (HasFeedChanged)
                    {
                        c.VIEWPAGER_ADAPTER.JumpToTop();
                    }

                    break;

            }

        }

        private bool HasFeedChanged
        {
            get
            {
                int count = 0;
                foreach (var item in TBL.FeedEntries)
                {
                    if (!item.MarkedRead) { count += 1; }
                }
                return count > 0;
            }
        }

        AlertDialog dialogWrongLogin = null;
        AlertDialog dialogError = null;

        //###################################################################################

        private void LIST_FEED_ItemSelected(object sender, Adapters.ListFeedAdapterItemSelectedEventArgs e)
        {

            //feedEntry öffnen
            var intent = new Intent();
            intent.SetClass(this, typeof(Activity_FeedEntry));
            intent.PutExtra(Activity_FeedEntry.BUNDLE_ENTRYKEY, e.EntryKey);

            StartActivity(intent);

        }

        private void LIST_SHIFTS_AttachmentRetrieveError(object sender, Adapters.AttachmentRetrieveErrorEventArgs e)
        {
            var ErrorText = e.Reason switch
            {
                Adapters.AttachmentRetrieveErrorReason.CONNECTION_LOST => GetString(Resource.String.main_snack_connect),
                Adapters.AttachmentRetrieveErrorReason.RELOGIN_REQUIRED => GetString(Resource.String.main_snack_relogin),
                _ => GetString(Resource.String.main_snack_error),
            };
            if (string.IsNullOrWhiteSpace(ErrorText)) { return; }

            //Snackbar aufrufen
            View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
            Snackbar snack = Snackbar.Make(rootView, ErrorText, Snackbar.LengthLong);
            snack.Show();

        }
        private void LIST_SHIFTS_ItemSelected(object sender, Adapters.ListShiftsAdapterItemSelectedEventArgs e)
        {

            EntryAttachment attachment = TBL.GetShiftsEntry(e.EntryKey).ShiftAttachment;
            FileOpen.Open(this, attachment.LocalFilePath);

        }

    }

    namespace Adapters
    {

        public class ViewPagerAdapter : FragmentPagerAdapter
        {

            public Fragments.Fragment_ListFeed FRAGMENT_FEED { get; private set; }
            public Fragments.Fragment_ListShifts FRAGMENT_SHIFTS { get; private set; }

            //###################################################################################

            public ViewPagerAdapter(FragmentManager manager) : base(manager) { }

            public event EventHandler<Adapters.ListFeedAdapterItemSelectedEventArgs> FeedItemSelected;

            public event EventHandler<Adapters.ListShiftsAdapterItemSelectedEventArgs> ShiftsItemSelected;
            public event EventHandler<Adapters.AttachmentRetrieveErrorEventArgs> ShiftsItemAttachmentRetrieveError;

            public event EventHandler RefreshCloudRequested;

            //###################################################################################

            public override int Count => 2;

            public override Fragment GetItem(int position)
            {
                switch (position)
                {
                    case 0:
                        if (FRAGMENT_FEED == null) { FRAGMENT_FEED = new Fragments.Fragment_ListFeed(); }
                        return FRAGMENT_FEED;

                    case 1:
                        if (FRAGMENT_SHIFTS == null) { FRAGMENT_SHIFTS = new Fragments.Fragment_ListShifts(); }
                        return FRAGMENT_SHIFTS;

                    default:
                        return new Fragment();
                }
            }

            public override Java.Lang.Object InstantiateItem(ViewGroup container, int position)
            {

                var frag = base.InstantiateItem(container, position);
                if (frag is Fragments.Fragment_ListFeed)
                {
                    FRAGMENT_FEED = (Fragments.Fragment_ListFeed)frag;

                    FRAGMENT_FEED.ItemSelected += FeedItemSelected;
                    FRAGMENT_FEED.RefreshCloudRequested += FRAGMENT_RefreshCloudRequested;

                    if (inflate_pending_feed)
                    {
                        inflate_pending_feed = false;
                        FRAGMENT_FEED.Inflate();
                    }

                    return FRAGMENT_FEED;
                }
                else
                {
                    FRAGMENT_SHIFTS = (Fragments.Fragment_ListShifts)frag;

                    FRAGMENT_SHIFTS.ItemSelected += ShiftsItemSelected;
                    FRAGMENT_SHIFTS.AttachmentRetrieveError += ShiftsItemAttachmentRetrieveError;

                    FRAGMENT_SHIFTS.RefreshCloudRequested += FRAGMENT_RefreshCloudRequested;

                    if (inflate_pending_shifts)
                    {
                        inflate_pending_shifts = false;
                        FRAGMENT_SHIFTS.Inflate();
                    }

                    return FRAGMENT_SHIFTS;
                }

            }

            private void FRAGMENT_RefreshCloudRequested(object sender, EventArgs e)
            {
                RefreshCloudRequested?.Invoke(sender, e);
            }

            //###################################################################################

            bool inflate_pending_feed = false;
            bool inflate_pending_shifts = false;

            public void Inflate()
            {

                if (FRAGMENT_FEED == null) { inflate_pending_feed = true; }
                if (FRAGMENT_SHIFTS == null) { inflate_pending_shifts = true; }
                if (inflate_pending_feed || inflate_pending_shifts) { return; }

                FRAGMENT_FEED.Inflate();
                FRAGMENT_SHIFTS.Inflate();
            }
            public void JumpToTop()
            {

                if (FRAGMENT_FEED == null) { return; }
                if (FRAGMENT_SHIFTS == null) { return; }

                FRAGMENT_FEED.JumpToTop();
                FRAGMENT_SHIFTS.JumpToTop();

            }

        }

        public class ListFeedAdapter : BaseAdapter<FeedEntry>
        {

            private readonly Context _context;

            private List<SourceHolder> _source;
            private Dictionary<int, ViewHolder> _viewholders;

            private enum SourceType
            {
                NOTHINGNEW,
                SUB_UNREAD,
                SUB_OLDER,
                SECTION,
                FEEDENTRY
            }
            private class SourceHolder
            {
                public SourceType Type;

                public FeedEntry FeedEntry;

                public int SectionYear;
                public string SectionMonth;

                public int UnreadCount;

                public void SetNothingNew()
                {
                    Type = SourceType.NOTHINGNEW;
                }
                public void SetSubheadUnread(int count)
                {
                    Type = SourceType.SUB_UNREAD;
                    UnreadCount = count;
                }
                public void SetSubheadOlder()
                {
                    Type = SourceType.SUB_OLDER;
                }
                public void SetSection(string month, int year)
                {
                    Type = SourceType.SECTION;
                    SectionMonth = month;
                    SectionYear = year;
                }
                public void SetFeedEntry(FeedEntry entry)
                {
                    Type = SourceType.FEEDENTRY;
                    FeedEntry = entry;
                }

            }

            private class ViewHolder
            {
                public View CONVERTVIEW;

                public TextView UNREAD_COUNT;
                public View UNREAD_MARKALL;

                public TextView SECTION_MONTH;

                public TextView ENTRY_TITLE;
                public TextView ENTRY_DATE;

            }

            //############################################################################

            public ListFeedAdapter(Context context)
            {
                _context = context;
                CreateSource();
            }

            private void CreateSource()
            {

                //Viewholder erstellen
                _viewholders = new Dictionary<int, ViewHolder>();
                _source = new List<SourceHolder>();
                if (TBL.IsFeedEmpty) { return; }

                //EntrieElemente erstellen & in die Liste einfügen
                List<FeedEntry> listUnread = new List<FeedEntry>();
                List<FeedEntry> listRead = new List<FeedEntry>();
                foreach (var item in TBL.FeedEntries)
                {
                    if (item.MarkedRead) { listRead.Add(item); }
                    else { listUnread.Add(item); }
                }

                //Unread-Sektion
                if (listUnread.Count > 0)
                {
                    var v = new SourceHolder(); v.SetSubheadUnread(listUnread.Count);
                    _source.Add(v);

                    CreateFeedSubhead(listUnread);
                }
                else
                {
                    var v = new SourceHolder(); v.SetNothingNew();
                    _source.Add(v);
                }

                //Older-Sektion
                if (listRead.Count > 0)
                {
                    var v = new SourceHolder(); v.SetSubheadOlder();
                    _source.Add(v);

                    CreateFeedSubhead(listRead);
                }

            }
            private void CreateFeedSubhead(List<FeedEntry> entries)
            {

                int lastMonth = -1;
                foreach (FeedEntry item in entries)
                {

                    //Monats-Subhead
                    if (lastMonth > 0 && item.Date.Month != lastMonth)
                    {
                        var vSM = new SourceHolder(); vSM.SetSection(item.Date.ToString("MMMM"), item.Date.Year);
                        _source.Add(vSM);
                    }
                    lastMonth = item.Date.Month;

                    //Entry einfügen
                    var vE = new SourceHolder(); vE.SetFeedEntry(item);
                    _source.Add(vE);

                }

            }

            //############################################################################

            public override FeedEntry this[int position] { get { if (_source[position].Type == SourceType.FEEDENTRY) { return _source[position].FeedEntry; } else { return null; } } }
            public override int Count => _source.Count;

            //############################################################################

            public override long GetItemId(int position)
            {
                return position;
            }
            public override View GetView(int position, View convertView, ViewGroup parent)
            {

                SourceHolder source = _source[position];
                ViewHolder v = null;

                switch (source.Type)
                {
                    case SourceType.NOTHINGNEW:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_nounread, parent, false)
                            };

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        nothingNewView = v.CONVERTVIEW;
                        SetNothingNewSubheadHeight();

                        return v.CONVERTVIEW;

                    case SourceType.SUB_UNREAD:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_unread, parent, false)
                            };

                            v.UNREAD_COUNT = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_unreadCount);
                            v.UNREAD_MARKALL = v.CONVERTVIEW.FindViewById(Resource.Id.btn_markAllRead);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        v.UNREAD_COUNT.Text = _context.Resources.GetString(Resource.String.main_list_feed_unread_title, source.UnreadCount);
                        v.UNREAD_MARKALL.Click += UNREAD_MARKALL_Click;

                        return v.CONVERTVIEW;

                    case SourceType.SUB_OLDER:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_read, parent, false)
                            };

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        return v.CONVERTVIEW;

                    case SourceType.SECTION:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_month, parent, false)
                            };

                            v.SECTION_MONTH = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        string sectionText = source.SectionMonth;
                        if (source.SectionYear != DateTime.Now.Year) { sectionText += " " + source.SectionYear; }
                        v.SECTION_MONTH.Text = sectionText;

                        return v.CONVERTVIEW;

                    case SourceType.FEEDENTRY:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_item, parent, false)
                            };

                            v.ENTRY_TITLE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);
                            v.ENTRY_DATE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_date);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        if (source.FeedEntry.Date.Year == DateTime.Now.Year) { v.ENTRY_DATE.Text = source.FeedEntry.Date.ToString("dd. MMMM"); }
                        else { v.ENTRY_DATE.Text = source.FeedEntry.Date.ToString("dd. MMMM yyyy"); }
                        v.ENTRY_TITLE.Text = source.FeedEntry.Title;

                        if (source.FeedEntry.MarkedRead) { v.CONVERTVIEW.Alpha = 0.6F; }
                        else { v.CONVERTVIEW.Alpha = 1.0F; }

                        return v.CONVERTVIEW;

                    default:
                        return null;
                }

            }

            //############################################################################

            private int nothingNewHeight = -1;
            private View nothingNewView;

            public void UpdateNothingNewSubheadHeight(int height)
            {

                if (nothingNewHeight == height) { return; }

                nothingNewHeight = height;
                if (nothingNewView == null) { return; }

                SetNothingNewSubheadHeight();

            }
            private void SetNothingNewSubheadHeight()
            {

                if (nothingNewHeight <= 0) { return; }
                nothingNewView.LayoutParameters = new ListView.LayoutParams(ListView.LayoutParams.MatchParent, nothingNewHeight);

            }

            //############################################################################

            private void UNREAD_MARKALL_Click(object sender, EventArgs e)
            {
                TBL.MarkReadFeedEntryAll();

                CreateSource();
                NotifyDataSetChanged();

            }

        }
        public class ListFeedAdapterItemSelectedEventArgs : EventArgs
        {
            public ListFeedAdapterItemSelectedEventArgs(string entryKey)
            {
                EntryKey = entryKey;
            }
            public string EntryKey { get; }
        }

        public class ListShiftsAdapter : BaseAdapter<ShiftsEntry>
        {

            private readonly Context _context;

            private readonly List<SourceHolder> _source;
            private readonly Dictionary<int, ViewHolder> _viewholders;

            private enum SourceType
            {
                SUB_ACTIVE,
                SUB_INACTIVE,
                SHIFTSENTRY
            }
            private class SourceHolder
            {
                public SourceType Type;
                public ShiftsEntry ShiftsEntry;

                public SourceHolder(SourceType type)
                {
                    Type = type;
                }
                public SourceHolder(ShiftsEntry entry)
                {
                    Type = SourceType.SHIFTSENTRY;
                    ShiftsEntry = entry;
                }
            }

            public class ViewHolder
            {
                public View CONVERTVIEW;

                public TextView ENTRY_TITLE;
                public TextView LASTUPDATE;
                public TextView LASTVERSION;
                public ProgressBar PROGRESS;

                public TextView SECTION_TITLE;
            }

            //############################################################################

            public ListShiftsAdapter(Context context)
            {
                _context = context;

                //Viewholder erstellen
                _viewholders = new Dictionary<int, ViewHolder>();
                _source = new List<SourceHolder>();

                int isActiveSection = -1;
                DateTime activeBorder = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0);

                foreach (ShiftsEntry item in TBL.ShiftsEntries)
                {

                    DateTime entryBorder = new DateTime(item.Year, item.Month, 1, 0, 0, 0);
                    if (entryBorder >= activeBorder)
                    {
                        if (isActiveSection == -1)
                        {
                            _source.Add(new SourceHolder(SourceType.SUB_ACTIVE));
                            isActiveSection = 1;
                        }
                    }
                    else
                    {
                        if (isActiveSection == 1)
                        {
                            _source.Add(new SourceHolder(SourceType.SUB_INACTIVE));
                            isActiveSection = 0;
                        }
                    }

                    //Entry erstellen
                    _source.Add(new SourceHolder(item));

                }

            }

            //############################################################################

            public override ShiftsEntry this[int position] { get { if (_source[position].Type == SourceType.SHIFTSENTRY) { return _source[position].ShiftsEntry; } else { return null; } } }
            public ViewHolder GetViewholder(int position) { if (_viewholders.ContainsKey(position)) { return _viewholders[position]; } else { return null; } }

            public override int Count => _source.Count;

            //############################################################################

            public override long GetItemId(int position)
            {
                return position;
            }
            public override View GetView(int position, View convertView, ViewGroup parent)
            {

                SourceHolder source = _source[position];
                ViewHolder v;

                switch (source.Type)
                {
                    case SourceType.SUB_ACTIVE:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_section_active, parent, false)
                            };

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        return v.CONVERTVIEW;

                    case SourceType.SUB_INACTIVE:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_section_inactive, parent, false)
                            };

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        return v.CONVERTVIEW;

                    case SourceType.SHIFTSENTRY:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder
                            {
                                CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_item, parent, false)
                            };

                            v.ENTRY_TITLE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);
                            v.LASTUPDATE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_lastupdate);
                            v.LASTVERSION = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_lastversion);
                            v.PROGRESS = v.CONVERTVIEW.FindViewById<ProgressBar>(Resource.Id.progress_attachDownload);

                            _viewholders.Add(position, v);

                        }

                        v = _viewholders[position];
                        v.ENTRY_TITLE.Text = source.ShiftsEntry.Title;
                        v.LASTUPDATE.Text = _context.Resources.GetString(Resource.String.main_list_feed_item_update, source.ShiftsEntry.LastUpdate.ToString("dd. MMMM yyyy"));
                        v.LASTVERSION.Text = _context.Resources.GetString(Resource.String.main_list_feed_item_version, source.ShiftsEntry.LastVersion);
                        v.PROGRESS.Visibility = ViewStates.Gone;

                        VisualizeShiftsEntryState(source.ShiftsEntry, v);

                        return v.CONVERTVIEW;

                    default:
                        return null;
                }

            }

            //############################################################################

            public void VisualizeShiftsEntryState(ShiftsEntry e, ViewHolder v)
            {

                DateTime activeBorder = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0);
                DateTime entrySource = new DateTime(e.Year, e.Month, 1, 0, 0, 0);

                if (entrySource >= activeBorder)
                {

                    if (!e.MarkedRead)
                    {
                        v.CONVERTVIEW.SetBackgroundColor(new Android.Graphics.Color(ContextCompat.GetColor(_context, Resource.Color.background_list_shifts)));
                        v.CONVERTVIEW.Alpha = 1.0F;
                        v.PROGRESS.IndeterminateDrawable.SetColorFilter(Android.Graphics.Color.White, Android.Graphics.PorterDuff.Mode.SrcAtop);

                        return;
                    }
                    else
                    {
                        v.CONVERTVIEW.Alpha = 1.0F;
                    }

                }
                else
                {
                    v.CONVERTVIEW.Alpha = 0.4F;
                }

                v.CONVERTVIEW.SetBackgroundColor(Android.Graphics.Color.Transparent);
                v.PROGRESS.IndeterminateDrawable.SetColorFilter(new Android.Graphics.Color(ContextCompat.GetColor(_context, Resource.Color.background_list_shifts)), Android.Graphics.PorterDuff.Mode.SrcAtop);

            }

        }
        public class ListShiftsAdapterItemSelectedEventArgs : EventArgs
        {
            public ListShiftsAdapterItemSelectedEventArgs(string entryKey, string attachmentKey)
            {
                EntryKey = entryKey; AttachmentKey = attachmentKey;
            }
            public string EntryKey { get; }
            public string AttachmentKey { get; }
        }

        public class AttachmentRetrieveErrorEventArgs
        {
            public AttachmentRetrieveErrorEventArgs(AttachmentRetrieveErrorReason reason) { Reason = reason; }
            public AttachmentRetrieveErrorReason Reason { get; }
        }
        public enum AttachmentRetrieveErrorReason
        {
            CONNECTION_LOST,
            RETRIEVE_ERROR,
            RELOGIN_REQUIRED
        }

    }
    namespace Fragments
    {

        public class Fragment_ListFeed : Fragment
        {

            private ListView LIST_FEED;
            private Adapters.ListFeedAdapter ADAPTER_FEED;
            private Android.Support.V4.Widget.SwipeRefreshLayout LIST_REFRESH;

            private const string BUNDLE_LIST_SCROLLOFFSET = "bundle_list_scrolloffset";
            private const string BUNDLE_LIST_SCROLLINDEX = "bundle_list_scrollindex";

            //##########################################################################

            public event EventHandler<Adapters.ListFeedAdapterItemSelectedEventArgs> ItemSelected;
            public event EventHandler RefreshCloudRequested;

            //##########################################################################

            public void Inflate()
            {

                if (LIST_FEED == null)
                {
                    inflate_pending = true;
                    return;
                }

                ADAPTER_FEED = new Adapters.ListFeedAdapter(Activity);
                LIST_FEED.Adapter = ADAPTER_FEED;

                if (lastHeight > 0) { ADAPTER_FEED.UpdateNothingNewSubheadHeight(lastHeight); }
                LIST_FEED.SetSelectionFromTop(last_pos, last_offset);

            }
            public void JumpToTop()
            {
                if (LIST_FEED == null) { return; }
                LIST_FEED.SmoothScrollToPositionFromTop(0, 0);
            }

            //##########################################################################

            public override void OnResume()
            {
                base.OnResume();
                lastHeight = -1;

                LIST_FEED.LayoutChange += LIST_FEED_LayoutChange;
                LIST_FEED.ItemClick += LIST_FEED_ItemClick;
                LIST_FEED.ScrollStateChanged += LIST_FEED_ScrollStateChanged;
                LIST_REFRESH.Refresh += GetRefreshCloud;

            }
            public override void OnPause()
            {
                base.OnPause();

                LIST_FEED.LayoutChange -= LIST_FEED_LayoutChange;
                LIST_FEED.ItemClick -= LIST_FEED_ItemClick;
                LIST_FEED.ScrollStateChanged -= LIST_FEED_ScrollStateChanged;
                LIST_REFRESH.Refresh -= GetRefreshCloud;

            }

            public override void OnSaveInstanceState(Bundle outState)
            {
                base.OnSaveInstanceState(outState);

                GetScrollPosition(out int position, out int offset);

                outState.PutInt(BUNDLE_LIST_SCROLLINDEX, position);
                outState.PutInt(BUNDLE_LIST_SCROLLOFFSET, offset);
            }
            public override void OnActivityCreated(Bundle savedInstanceState)
            {
                base.OnActivityCreated(savedInstanceState);
                if (savedInstanceState == null) { return; }

                int position = savedInstanceState.GetInt(BUNDLE_LIST_SCROLLINDEX, 0);
                int offset = savedInstanceState.GetInt(BUNDLE_LIST_SCROLLOFFSET, 0);

                last_pos = position;
                last_offset = offset;

            }

            //##########################################################################

            private bool inflate_pending = false;

            private int last_pos = 0;
            private int last_offset = 0;

            public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            {
                View v = inflater.Inflate(Resource.Layout.fragment_main_list_feed, null);

                LIST_FEED = v.FindViewById<ListView>(Resource.Id.main_list_feed);
                LIST_FEED.Divider = null;

                LIST_REFRESH = v.FindViewById<Android.Support.V4.Widget.SwipeRefreshLayout>(Resource.Id.main_refresh_feed);

                if (inflate_pending)
                {
                    inflate_pending = false;
                    Inflate();
                }

                return v;
            }

            private void GetScrollPosition(out int position, out int offset)
            {
                position = LIST_FEED.FirstVisiblePosition; View v = LIST_FEED.GetChildAt(0);
                offset = (v == null) ? 0 : (v.Top - LIST_FEED.PaddingTop);
            }
            private void GetRefreshCloud(object sender, EventArgs e)
            {
                RefreshCloudRequested?.Invoke(LIST_REFRESH, new EventArgs());
            }

            //##########################################################################

            private int lastHeight = -1;

            private void LIST_FEED_LayoutChange(object sender, View.LayoutChangeEventArgs e)
            {
                var height = e.Bottom - e.Top;
                lastHeight = height;

                if (ADAPTER_FEED != null) { ADAPTER_FEED.UpdateNothingNewSubheadHeight(height); }
            }

            private void LIST_FEED_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
            {

                FeedEntry x = ADAPTER_FEED[e.Position];
                if (x == null) { return; }

                TBL.MarkReadFeedEntry(x.Key);
                ItemSelected?.Invoke(this, new Adapters.ListFeedAdapterItemSelectedEventArgs(x.Key));

            }

            private void LIST_FEED_ScrollStateChanged(object sender, AbsListView.ScrollStateChangedEventArgs e)
            {
                if (e.ScrollState == ScrollState.Idle)
                {
                    GetScrollPosition(out int position, out int offset);
                    last_pos = position;
                    last_offset = offset;
                }
            }

        }
        public class Fragment_ListShifts : Fragment
        {

            private ListView LIST_SHIFTS;
            private Adapters.ListShiftsAdapter ADAPTER_SHIFTS;
            private Android.Support.V4.Widget.SwipeRefreshLayout LIST_REFRESH;

            private const string BUNDLE_LIST_SCROLLOFFSET = "bundle_list_shifts_scrolloffset";
            private const string BUNDLE_LIST_SCROLLINDEX = "bundle_list_shifts_scrollindex";

            //##########################################################################

            public event EventHandler<Adapters.ListShiftsAdapterItemSelectedEventArgs> ItemSelected;
            public event EventHandler<Adapters.AttachmentRetrieveErrorEventArgs> AttachmentRetrieveError;

            public event EventHandler RefreshCloudRequested;

            //##########################################################################

            public void Inflate()
            {

                if (LIST_SHIFTS == null)
                {
                    inflate_pending = true;
                    return;
                }

                ADAPTER_SHIFTS = new Adapters.ListShiftsAdapter(Activity);
                LIST_SHIFTS.Adapter = ADAPTER_SHIFTS;

                LIST_SHIFTS.SetSelectionFromTop(last_pos, last_offset);

            }
            public void JumpToTop()
            {
                if (LIST_SHIFTS == null) { return; }
                LIST_SHIFTS.SmoothScrollToPositionFromTop(0, 0);
            }

            //##########################################################################

            public override void OnResume()
            {

                base.OnResume();

                LIST_SHIFTS.ItemClick += LIST_SHIFTS_ItemClick;
                LIST_SHIFTS.ScrollStateChanged += LIST_SHIFTS_ScrollStateChanged;

                LIST_REFRESH.Refresh += GetRefreshCloud;

            }
            public override void OnPause()
            {

                base.OnPause();

                LIST_SHIFTS.ItemClick -= LIST_SHIFTS_ItemClick;
                LIST_SHIFTS.ScrollStateChanged -= LIST_SHIFTS_ScrollStateChanged;

                LIST_REFRESH.Refresh -= GetRefreshCloud;

            }

            public override void OnSaveInstanceState(Bundle outState)
            {
                base.OnSaveInstanceState(outState);

                GetScrollPosition(out int position, out int offset);

                outState.PutInt(BUNDLE_LIST_SCROLLINDEX, position);
                outState.PutInt(BUNDLE_LIST_SCROLLOFFSET, offset);

            }
            public override void OnActivityCreated(Bundle savedInstanceState)
            {
                base.OnActivityCreated(savedInstanceState);
                if (savedInstanceState == null) { return; }

                int position = savedInstanceState.GetInt(BUNDLE_LIST_SCROLLINDEX, 0);
                int offset = savedInstanceState.GetInt(BUNDLE_LIST_SCROLLOFFSET, 0);

                last_pos = position;
                last_offset = offset;

            }

            //##########################################################################

            private bool inflate_pending = false;

            private int last_pos = 0;
            private int last_offset = 0;

            public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            {
                View v = inflater.Inflate(Resource.Layout.fragment_main_list_shifts, null);

                LIST_SHIFTS = v.FindViewById<ListView>(Resource.Id.main_list_shifts);
                LIST_REFRESH = v.FindViewById<Android.Support.V4.Widget.SwipeRefreshLayout>(Resource.Id.main_refresh_shifts);

                LIST_SHIFTS.Divider = null;

                if (inflate_pending)
                {
                    inflate_pending = false;
                    Inflate();
                }

                return v;
            }

            private void GetScrollPosition(out int position, out int offset)
            {
                position = LIST_SHIFTS.FirstVisiblePosition; View v = LIST_SHIFTS.GetChildAt(0);
                offset = (v == null) ? 0 : (v.Top - LIST_SHIFTS.PaddingTop);
            }
            private void GetRefreshCloud(object sender, EventArgs e)
            {
                RefreshCloudRequested?.Invoke(LIST_REFRESH, new EventArgs());
            }

            //##########################################################################

            private void LIST_SHIFTS_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
            {

                ShiftsEntry x = ADAPTER_SHIFTS[e.Position];
                Adapters.ListShiftsAdapter.ViewHolder v = ADAPTER_SHIFTS.GetViewholder(e.Position);
                if (x == null || v == null || v.CONVERTVIEW.Tag?.ToString() == Helper.Constant.STATE_BLOCKED) { return; }

                ViewAttachment(x, v);

            }

            private void LIST_SHIFTS_ScrollStateChanged(object sender, AbsListView.ScrollStateChangedEventArgs e)
            {
                if (e.ScrollState == ScrollState.Idle)
                {
                    GetScrollPosition(out int position, out int offset);
                    last_pos = position;
                    last_offset = offset;
                }
            }

            private void ViewAttachment(ShiftsEntry x, Adapters.ListShiftsAdapter.ViewHolder v)
            {

                //Item blockieren
                v.CONVERTVIEW.Tag = Helper.Constant.STATE_BLOCKED;

                //Wartekreis anzeigen
                v.PROGRESS.Visibility = ViewStates.Visible;

                //Download starten
                TBL.SP_Object.GetNewsFeedAttachment(x.ShiftAttachment,
                delegate (Adapters.AttachmentRetrieveErrorReason reason)
                {

                    //Wartekreis ausblenden, wenn Download beendet
                    if (reason != Adapters.AttachmentRetrieveErrorReason.RELOGIN_REQUIRED)
                    {
                        v.PROGRESS.Visibility = ViewStates.Gone;
                        v.CONVERTVIEW.Tag = null;
                    }

                    //An das zuständige Fragment melden
                    AttachmentRetrieveError?.Invoke(this, new Adapters.AttachmentRetrieveErrorEventArgs(reason));

                },
                delegate (string localPath)
                {

                    //Wartekreis ausblenden
                    v.PROGRESS.Visibility = ViewStates.Gone;

                    //Gelesen markieren
                    TBL.MarkReadShiftsEntry(x.Key);

                    //Blockierung aufheben & Aussehen anpassen
                    v.CONVERTVIEW.Tag = null;
                    ADAPTER_SHIFTS.VisualizeShiftsEntryState(x, v);

                    //Datei öffnen
                    ItemSelected?.Invoke(this, new Adapters.ListShiftsAdapterItemSelectedEventArgs(x.Key, x.ShiftAttachment.Key));

                });

            }

        }

    }

}