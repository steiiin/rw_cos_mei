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
using Fragment = Android.Support.V4.App.Fragment;

using TBL = rw_cos_mei.AppTable;
using Android.Util;
using Android.Support.V4.View;
using Android.Support.V4.App;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using Java.Util;
using Android.Support.V4.Content;


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

            public TextView TOOLBAR_REFRESHTIMER;

            public BottomNavigationView BOTTOMNAVIGATION;
            public ViewPager VIEWPAGER;
            public Adapters.ViewPagerAdapter VIEWPAGER_ADAPTER;

            public View REFRESH_PROGRESS;
            public View REFRESH_PROGRESS_OVERLAY;
                        
        }
        private ViewHolder c;

        //###################################################################################

        private bool onStartup;

        protected override void OnCreate(Bundle savedInstanceState)
        {

            onStartup = false;
            if (savedInstanceState == null) { onStartup = true; }
            savedInstanceState = null;
            base.OnCreate(savedInstanceState);
            
            //Layout füllen
            SetContentView(Resource.Layout.activity_main);
            CreateViewholder();
            CreateToolbar();
            
        }
     
        protected override void OnResume()
        {
            base.OnResume();

            //Hack, um den Statischen Speicher wiederherzustellen, wenn Android die App im Hintergrund killt. 
            // -> Kein Bock, statt dem AppTable alle Objekte Parcelable zu programmieren.
            if(TBL.SP_Object == null)
            {
                Activity_Init.InitRoutine(this);
            }

            //Oberfläche an Sharepoint-Status anpassen
            TBL.SP_Object.StateChanged += SP_Object_StateChanged;
            ViewStateChanger(TBL.SP_Object.State);

            //ViewPager
            c.VIEWPAGER.PageSelected += VIEWPAGER_PageSelected;

            c.VIEWPAGER_ADAPTER.FRAGMENT_FEED.ItemSelected += LIST_FEED_ItemSelected;
            c.VIEWPAGER_ADAPTER.FRAGMENT_SHIFTS.ItemSelected += LIST_SHIFTS_ItemSelected;
            c.VIEWPAGER_ADAPTER.FRAGMENT_SHIFTS.AttachmentRetrieveError += LIST_SHIFTS_AttachmentRetrieveError;

            //Refresh erst beginnen, wenn App startet
            if (onStartup)
            {
                onStartup = false;
                if((DateTime.Now - TBL.LastTableRefresh).TotalMinutes > 30 || TBL.IsFeedEmpty)
                {
                    RefreshCloud();
                }
            }

        }
        protected override void OnPause()
        {

            TBL.SP_Object.StateChanged -= SP_Object_StateChanged;

            c.VIEWPAGER.PageSelected -= VIEWPAGER_PageSelected;

            c.VIEWPAGER_ADAPTER.FRAGMENT_FEED.ItemSelected -= LIST_FEED_ItemSelected;
            c.VIEWPAGER_ADAPTER.FRAGMENT_SHIFTS.ItemSelected -= LIST_SHIFTS_ItemSelected;
            c.VIEWPAGER_ADAPTER.FRAGMENT_SHIFTS.AttachmentRetrieveError -= LIST_SHIFTS_AttachmentRetrieveError;

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

                    if (TBL.SP_Object.State != SharepointAPIState.WORKING)
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
        private async void RefreshCloud()
        {
            TBL.BlockSyncService();
            await TBL.SP_Object.UpdateNewsFeed();
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

            c.VIEWPAGER.OffscreenPageLimit = 2;

            c.VIEWPAGER_ADAPTER.Inflate();

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

            if(TBL.LastTableRefresh != DateTime.MinValue)
            {
                if(TBL.LastTableRefresh.Day == DateTime.Now.Day && TBL.LastTableRefresh.Month == DateTime.Now.Month && TBL.LastTableRefresh.Year == DateTime.Now.Year)
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

        }
        
        //###################################################################################
        
        private void SP_Object_StateChanged(object sender, SharepointAPIStateChangedEventArgs e)
        {
            ViewStateChanger(e.State);
        }
        private void ViewStateChanger(SharepointAPIState state)
        {

            AlertDialog dialogWrongLogin = null;
            AlertDialog dialogError = null;

            switch (state)
            {
                case SharepointAPIState.WORKING:

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

                    if (TBL.IsFeedEmpty)
                    {

                        //Dialog anzeigen, der WIEDERHOLEN anbietet.
                        dialogError = new AlertDialog.Builder(this)
                            .SetTitle(Resource.String.main_dialog_error_title)
                            .SetMessage(Resource.String.main_dialog_error_msg)
                            .SetPositiveButton(Resource.String.dialog_retry, (ss, ee) => { })
                            .SetCancelable(true)
                            .Show();
                        dialogError.GetButton((int)DialogButtonType.Positive).Click += delegate
                        {

                            dialogError.Dismiss();
                            RefreshCloud();

                        };

                    }
                    else
                    {

                        //Snackbar anzeigen, mit WIEDERHOLEN
                        View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
                        Snackbar snack = Snackbar.Make(rootView, Resource.String.main_dialog_error_msg, Snackbar.LengthLong);
                        snack.SetAction(Resource.String.dialog_retry, (ss) => { RefreshCloud(); });
                        snack.Show();

                    }

                    break;

                case SharepointAPIState.OK:
                case SharepointAPIState.LOGGED_IN:
                case SharepointAPIState.OFFLINE:

                    TBL.UnBlockSyncService();

                    if (dialogWrongLogin != null) { dialogWrongLogin.Dismiss(); }
                    if (dialogError != null) { dialogError.Dismiss(); }

                    c.REFRESH_PROGRESS.Visibility = ViewStates.Gone;
                    c.REFRESH_PROGRESS_OVERLAY.Visibility = ViewStates.Gone;

                    UpdateRefreshTimer();

                    //Zeige Datenbank-Zeug
                    c.VIEWPAGER_ADAPTER.Inflate();

                    break;

            }  

        }
        
        //###################################################################################
        
        private void LIST_FEED_ItemSelected(object sender, Adapters.ListFeedAdapterItemSelectedEventArgs e)
        {

            //feedEntry öffnen
            var intent = new Intent();
            intent.SetClass(this, typeof(Activity_FeedEntry));
            intent.PutExtra(Activity_FeedEntry.BUNDLE_ENTRYKEY, e.EntryKey);

            StartActivity(intent);

        }
        
        private void LIST_SHIFTS_AttachmentRetrieveError(object sender, EventArgs e)
        {

            //Snackbar aufrufen
            View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
            Snackbar snack = Snackbar.Make(rootView, "Anhang konnte nicht geladen werden. Internet?", Snackbar.LengthLong);
            snack.Show();

        }
        private void LIST_SHIFTS_ItemSelected(object sender, Adapters.ListShiftsAdapterItemSelectedEventArgs e)
        {

            EntryAttachment attachment = TBL.GetShiftsEntry(e.EntryKey).ShiftAttachment;
            FileOpen.Open(this, attachment.FileLocalUrl);

        }
        
    }

    namespace Adapters
    {

        public class ViewPagerAdapter : FragmentPagerAdapter
        {

            public Fragments.Fragment_ListFeed FRAGMENT_FEED { get; private set; }
            public Fragments.Fragment_ListShifts FRAGMENT_SHIFTS { get; private set; }

            //###################################################################################

            public ViewPagerAdapter(FragmentManager manager) : base(manager)
            {
                GetItem(0);
                GetItem(1);
            }

            //###################################################################################

            public override int Count => 2;

            public override Fragment GetItem(int position)
            {
                switch(position)
                {
                    case 0:
                        if(FRAGMENT_FEED == null) { FRAGMENT_FEED = new Fragments.Fragment_ListFeed(); }
                        return FRAGMENT_FEED;

                    case 1:
                        if (FRAGMENT_SHIFTS == null) { FRAGMENT_SHIFTS = new Fragments.Fragment_ListShifts(); }
                        return FRAGMENT_SHIFTS;

                    default:
                        return new Fragment();
                }
            }

            //###################################################################################

            public void Inflate()
            {
                FRAGMENT_FEED.Inflate();
                FRAGMENT_SHIFTS.Inflate();
            }

        }
        
        public class ListFeedAdapter : BaseAdapter<FeedEntry>
        {

            private Context _context;

            private List<SourceHolder> _source;
            private Dictionary<int, ViewHolder> _viewholders;

            private enum SourceType
            {
                NOTHINGNEW,
                SUB_UNREAD,
                SUB_OLDER,
                SECTION_YEAR,
                SECTION_MONTH,
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
                public void SetSectionYear(int year)
                {
                    Type = SourceType.SECTION_YEAR;
                    SectionYear = year;
                }
                public void SetSectionMonth(string month)
                {
                    Type = SourceType.SECTION_MONTH;
                    SectionMonth = month;
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

                public TextView SECTION_YEAR;
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
                int lastYear = -1;
                foreach (FeedEntry item in entries)
                {

                    //Monats-Subhead
                    if (lastMonth > 0 && item.Date.Month != lastMonth)
                    {
                        var vSM = new SourceHolder(); vSM.SetSectionMonth(item.Date.ToString("MMMM"));
                        _source.Add(vSM);
                    }

                    //Jahres-Subhead
                    if (lastYear > 0 && item.Date.Year != lastYear)
                    {
                        var vSY = new SourceHolder(); vSY.SetSectionYear(item.Date.Year);
                        _source.Add(vSY);
                    }
                    lastMonth = item.Date.Month; lastYear = item.Date.Year;

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

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_nounread, parent, false);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        nothingNewView = v.CONVERTVIEW;
                        SetNothingNewSubheadHeight();

                        return v.CONVERTVIEW;

                    case SourceType.SUB_UNREAD:
                        
                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_unread, parent, false);

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

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_section_read, parent, false);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        return v.CONVERTVIEW;

                    case SourceType.SECTION_YEAR:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_year, parent, false);

                            v.SECTION_YEAR = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        v.SECTION_YEAR.Text = source.SectionYear.ToString("yyyy");

                        return v.CONVERTVIEW;

                    case SourceType.SECTION_MONTH:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_subhead_month, parent, false);

                            v.SECTION_MONTH = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        v.SECTION_MONTH.Text = source.SectionMonth;

                        return v.CONVERTVIEW;

                    case SourceType.FEEDENTRY:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feed_item, parent, false);

                            v.ENTRY_TITLE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_title);
                            v.ENTRY_DATE = v.CONVERTVIEW.FindViewById<TextView>(Resource.Id.txt_date);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        if(source.FeedEntry.Date.Year == DateTime.Now.Year) { v.ENTRY_DATE.Text = source.FeedEntry.Date.ToString("dd. MMMM"); }
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

                if(nothingNewHeight == height) { return; }

                nothingNewHeight = height;
                if(nothingNewView == null) { return; }

                SetNothingNewSubheadHeight();

            }
            private void SetNothingNewSubheadHeight()
            {

                if(nothingNewHeight <= 0) { return; }
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

            private Context _context;

            private List<SourceHolder> _source;
            private Dictionary<int, ViewHolder> _viewholders;

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
                public SourceHolder (ShiftsEntry entry)
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
                        if(isActiveSection == -1)
                        {
                            _source.Add(new SourceHolder(SourceType.SUB_ACTIVE));
                            isActiveSection = 1;
                        }
                    }
                    else
                    {
                        if(isActiveSection == 1)
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

            public override ShiftsEntry this[int position] { get { if(_source[position].Type == SourceType.SHIFTSENTRY) { return _source[position].ShiftsEntry; } else { return null; } } }
            public ViewHolder GetViewholder(int position) { if(_viewholders.ContainsKey(position)) { return _viewholders[position]; } else { return null; } }

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
                    case SourceType.SUB_ACTIVE:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_section_active, parent, false);
                            
                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];
                        
                        return v.CONVERTVIEW;

                    case SourceType.SUB_INACTIVE:

                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_section_inactive, parent, false);

                            _viewholders.Add(position, v);

                        }
                        v = _viewholders[position];

                        return v.CONVERTVIEW;

                    case SourceType.SHIFTSENTRY:
                        
                        if (!_viewholders.ContainsKey(position))
                        {

                            v = new ViewHolder();
                            v.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_shifts_item, parent, false);
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

                    if(!e.MarkedRead)
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

    }
    namespace Fragments
    {
        
        public class Fragment_ListFeed : Fragment
        {
            
            private ListView LIST_FEED;
            private Adapters.ListFeedAdapter ADAPTER_FEED;

            private const string BUNDLE_LIST_SCROLLOFFSET = "bundle_list_scrolloffset";
            private const string BUNDLE_LIST_SCROLLINDEX = "bundle_list_scrollindex";

            //##########################################################################
            
            public event EventHandler<Adapters.ListFeedAdapterItemSelectedEventArgs> ItemSelected;

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

                if(lastHeight > 0) { ADAPTER_FEED.UpdateNothingNewSubheadHeight(lastHeight); }

            }

            public override void OnResume()
            {
                base.OnResume();
                lastHeight = -1;
                
                LIST_FEED.LayoutChange += LIST_FEED_LayoutChange;
                LIST_FEED.ItemClick += LIST_FEED_ItemClick;

                if (Activity?.Intent != null)
                {
                    int position = Activity.Intent.GetIntExtra(BUNDLE_LIST_SCROLLINDEX, 0);
                    int offset = Activity.Intent.GetIntExtra(BUNDLE_LIST_SCROLLOFFSET, 0);
                    LIST_FEED.SetSelectionFromTop(position, offset);
                }

            }
            public override void OnPause()
            {
                base.OnPause();

                LIST_FEED.LayoutChange -= LIST_FEED_LayoutChange;
                LIST_FEED.ItemClick -= LIST_FEED_ItemClick;

                int position = LIST_FEED.FirstVisiblePosition; View v = LIST_FEED.GetChildAt(0);
                int top = (v == null) ? 0 : (v.Top - LIST_FEED.PaddingTop);

                Activity.Intent.PutExtra(BUNDLE_LIST_SCROLLINDEX, position);
                Activity.Intent.PutExtra(BUNDLE_LIST_SCROLLOFFSET, top);
                
            }
            
            //##########################################################################
            
            private bool inflate_pending = false;
            public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            {
                View v = inflater.Inflate(Resource.Layout.fragment_main_list_feed, null);
                LIST_FEED = v.FindViewById<ListView>(Resource.Id.main_list_feed);
                LIST_FEED.Divider = null;

                if (inflate_pending)
                {
                    inflate_pending = false;
                    Inflate();
                }

                return v;
            }

            //##########################################################################
            
            private int lastHeight = -1;
            private void LIST_FEED_LayoutChange(object sender, View.LayoutChangeEventArgs e)
            {
                var height = e.Bottom - e.Top;
                lastHeight = height;

                ADAPTER_FEED.UpdateNothingNewSubheadHeight(height);
            }
            
            private void LIST_FEED_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
            {

                FeedEntry x = ADAPTER_FEED[e.Position];
                if (x == null) { return; }

                TBL.MarkReadFeedEntry(x.Key);
                ItemSelected?.Invoke(this, new Adapters.ListFeedAdapterItemSelectedEventArgs(x.Key));

            }

        }
        public class Fragment_ListShifts : Fragment
        {

            private ListView LIST_SHIFTS;
            private Adapters.ListShiftsAdapter ADAPTER_SHIFTS;

            private const string BUNDLE_LIST_SCROLLOFFSET = "bundle_list_shifts_scrolloffset";
            private const string BUNDLE_LIST_SCROLLINDEX = "bundle_list_shifts_scrollindex";

            //##########################################################################

            public event EventHandler<Adapters.ListShiftsAdapterItemSelectedEventArgs> ItemSelected;
            public event EventHandler AttachmentRetrieveError;

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

            }

            public override void OnResume()
            {

                base.OnResume();

                LIST_SHIFTS.ItemClick += LIST_SHIFTS_ItemClick;

                if (Activity?.Intent != null)
                {
                    int position = Activity.Intent.GetIntExtra(BUNDLE_LIST_SCROLLINDEX, 0);
                    int offset = Activity.Intent.GetIntExtra(BUNDLE_LIST_SCROLLOFFSET, 0);
                    LIST_SHIFTS.SetSelectionFromTop(position, offset);
                }

            }
            public override void OnPause()
            {

                base.OnPause();

                LIST_SHIFTS.ItemClick -= LIST_SHIFTS_ItemClick;

                int position = LIST_SHIFTS.FirstVisiblePosition; View v = LIST_SHIFTS.GetChildAt(0);
                int top = (v == null) ? 0 : (v.Top - LIST_SHIFTS.PaddingTop);

                Activity.Intent.PutExtra(BUNDLE_LIST_SCROLLINDEX, position);
                Activity.Intent.PutExtra(BUNDLE_LIST_SCROLLOFFSET, top);

            }

            //##########################################################################

            private void LIST_SHIFTS_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
            {

                ShiftsEntry x = ADAPTER_SHIFTS[e.Position];
                Adapters.ListShiftsAdapter.ViewHolder v = ADAPTER_SHIFTS.GetViewholder(e.Position);
                if (x == null || v == null) { return; }

                ViewAttachment(x, v);

            }

            private void ViewAttachment(ShiftsEntry x, Adapters.ListShiftsAdapter.ViewHolder v)
            {

                v.CONVERTVIEW.Tag = "BLOCKED";

                if (x.ShiftAttachment.IsAttachmentDownloaded)
                {
                    v.CONVERTVIEW.Tag = null;
                    ItemSelected?.Invoke(this, new Adapters.ListShiftsAdapterItemSelectedEventArgs(x.Key, x.ShiftAttachment.Key));
                }
                else
                {
                    
                    v.PROGRESS.Visibility = ViewStates.Visible;

                    TBL.SP_Object.GetNewsFeedAttachment(x.ShiftAttachment,
                    delegate
                    {

                        v.PROGRESS.Visibility = ViewStates.Gone;
                        v.CONVERTVIEW.Tag = null;

                        AttachmentRetrieveError?.Invoke(this, new EventArgs());

                    },
                    delegate (string localPath)
                    {

                        v.PROGRESS.Visibility = ViewStates.Gone;

                        x.ShiftAttachment.UpdateAttachment(localPath);
                        TBL.MarkReadShiftsEntry(x.Key);

                        v.CONVERTVIEW.Tag = null;
                        ADAPTER_SHIFTS.VisualizeShiftsEntryState(x, v);

                        ItemSelected?.Invoke(this, new Adapters.ListShiftsAdapterItemSelectedEventArgs(x.Key, x.ShiftAttachment.Key));

                    });

                }

            }

            //##########################################################################

            private bool inflate_pending = false;
            public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            {
                View v = inflater.Inflate(Resource.Layout.fragment_main_list_shifts, null);
                LIST_SHIFTS = v.FindViewById<ListView>(Resource.Id.main_list_shifts);

                LIST_SHIFTS.Divider = ContextCompat.GetDrawable(Context, Resource.Drawable.trans_divider);
                LIST_SHIFTS.DividerHeight = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 1, Resources.DisplayMetrics);

                if (inflate_pending)
                {
                    inflate_pending = false;
                    Inflate();
                }

                return v;
            }

        }

    }

}