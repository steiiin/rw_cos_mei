using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

using Org.Jsoup;
using Org.Jsoup.Nodes;

using System;

using Toolbar = Android.Support.V7.Widget.Toolbar;

using TBL = rw_cos_mei.AppTable;


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///Activity_FeedEntry
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    [Activity(Label = "@string/app_name")]
    public class Activity_FeedEntry : AppCompatActivity
    {
        
        public const string BUNDLE_ENTRYKEY = "bundle_entrykey";

        private class ViewHolder
        {
            
            public TextView APPBAR_TITLE;

            public TextView TXT_DATE;
            public TextView TXT_TITLE;
            public TextView TXT_AUTHOR;
            public TextView TXT_BODY;

            public View DIVIDER_BODY;

            public LinearLayout LIST_ATTACHMENTS;
            
        }
        private ViewHolder c;

        private FeedEntry _currentEntry;
        private Adapters.ListFeedAttachmentAdapter _currentAttachmentAdapter;

        //###################################################################################

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //entryFeed
            string entryID = Intent.GetStringExtra(BUNDLE_ENTRYKEY);
            _currentEntry = TBL.GetFeedEntry(entryID);

            //Layout füllen
            SetContentView(Resource.Layout.activity_feedEntry);

            CreateViewholder();
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

            base.OnResume();
            
        }
        protected override void OnPause()
        {
            base.OnPause();

            //Eventhandler entfernen
            CreateViewHandler(HandlerMethod.REMOVE_HANDLERS);

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

            c = new ViewHolder
            {
                APPBAR_TITLE = FindViewById<TextView>(Resource.Id.appbar_title),
                TXT_DATE = FindViewById<TextView>(Resource.Id.txt_date),
                TXT_TITLE = FindViewById<TextView>(Resource.Id.txt_title),
                TXT_AUTHOR = FindViewById<TextView>(Resource.Id.txt_author),
                TXT_BODY = FindViewById<TextView>(Resource.Id.txt_body),
                DIVIDER_BODY = FindViewById(Resource.Id.divider_body),
                LIST_ATTACHMENTS = FindViewById<LinearLayout>(Resource.Id.list_attachments)
            };

            c.TXT_DATE.Text = _currentEntry.Date.ToString("dd. MMMM yyyy");
            c.TXT_TITLE.Text = _currentEntry.Title;
            c.TXT_AUTHOR.Text = GetString(Resource.String.feedentry_from, _currentEntry.Author);

            if (string.IsNullOrEmpty(_currentEntry.Body))
            {
                c.TXT_BODY.Visibility = ViewStates.Gone;
                c.DIVIDER_BODY.Visibility = ViewStates.Gone;
            }
            else
            {
                c.TXT_BODY.Text = GetPlainOfHtml(_currentEntry.Body);
                c.TXT_BODY.MovementMethod = Android.Text.Method.LinkMovementMethod.Instance;
                c.DIVIDER_BODY.Visibility = ViewStates.Visible;
            }
            
            _currentAttachmentAdapter = new Adapters.ListFeedAttachmentAdapter(this, c.LIST_ATTACHMENTS, _currentEntry);
            
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

            c.APPBAR_TITLE.Text = _currentEntry.Date.ToString("dd. MMMM yyyy");

        }

        private void CreateViewHandler(HandlerMethod method)
        {
            
            if (method == HandlerMethod.ADD_HANDLERS)
            {

                _currentAttachmentAdapter.EntrySelected += AttachmentAdapter_EntrySelected;
                _currentAttachmentAdapter.AttachmentRetrieveError += AttachmentAdapter_AttachmentRetrieveError;

            }
            else
            {

                _currentAttachmentAdapter.EntrySelected -= AttachmentAdapter_EntrySelected;
                _currentAttachmentAdapter.AttachmentRetrieveError -= AttachmentAdapter_AttachmentRetrieveError;

            }

        }

        private void AttachmentAdapter_AttachmentRetrieveError(object sender, Adapters.AttachmentRetrieveErrorEventArgs e)
        {

            string ErrorText = "";
            switch (e.Reason)
            {
                case Adapters.AttachmentRetrieveErrorReason.CONNECTION_LOST:

                    ErrorText = GetString(Resource.String.main_snack_connect);
                    break;

                case Adapters.AttachmentRetrieveErrorReason.RELOGIN_REQUIRED:

                    ErrorText = GetString(Resource.String.main_snack_relogin);
                    break;

                case Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR:
                default:

                    ErrorText = GetString(Resource.String.main_snack_error);
                    break;

            }

            if (string.IsNullOrWhiteSpace(ErrorText)) { return; }

            //Snackbar aufrufen
            View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
            Snackbar snack = Snackbar.Make(rootView, ErrorText, Snackbar.LengthLong);
            snack.Show();

        }
        private void AttachmentAdapter_EntrySelected(object sender, Adapters.ListFeedAttachmentAdapterEntrySelected e)
        {

            EntryAttachment attachment = TBL.GetFeedEntry(e.EntryKey)?.GetAttachment(e.AttachmentKey);
            FileOpen.Open(this, attachment.FileLocalUrl);

        }

        //####################################################################

        private string GetPlainOfHtml(string html)
        {

            string plain = "";

            Document doc = Jsoup.ParseBodyFragment(html); var it = doc.Select("*");
            for (int i = 0; i < it.Size(); i++)
            {
                Element e = ((Element)it.Get(i));

                string node = e.NodeName();

                //Neue Zeile
                if (node == "p" ||
                   node == "br")
                {
                    plain += System.Environment.NewLine;
                }

                //Links
                if (node == "a")
                {
                    bool href = e.HasAttr("href");
                    if (href)
                    {
                        string link = e.Attr("href");
                        link = link.Replace("/:b:/s", "https://maltesercloud.sharepoint.com/:b:/s");

                        plain += System.Environment.NewLine + System.Environment.NewLine +
                                 GetPlainOfHtml(e.Html()) + System.Environment.NewLine +
                                 link +
                                 System.Environment.NewLine + System.Environment.NewLine;

                    }
                    break;
                }

                plain += e.OwnText();

            }

            return plain;

        }

    }

    namespace Adapters
    {

        public class ListFeedAttachmentAdapter
        {

            private readonly Context _context;
            private FeedEntry _entry;
            
            //####################################################################################

            public event EventHandler<ListFeedAttachmentAdapterEntrySelected> EntrySelected;
            public event EventHandler<Adapters.AttachmentRetrieveErrorEventArgs> AttachmentRetrieveError;
            
            //####################################################################################

            private class Viewholder
            {
                public View CONVERTVIEW;

                public Button BTN_ATTACHMENT;
                public ProgressBar PROGRESS_INDICATOR;
            }

            //####################################################################################
            
            public ListFeedAttachmentAdapter(Context context, LinearLayout parent, FeedEntry entry)
            {
                _context = context;
                _entry = entry;

                Inflate(parent);
            }

            //####################################################################################

            private void Inflate(LinearLayout parent)
            {

                parent.RemoveAllViews();
                if(_entry.Attachments.Count == 0) { return; }

                foreach (var item in _entry.Attachments)
                {

                    Viewholder hold = new Viewholder
                    {
                        CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feedEntry_attachment, parent, false)
                    };

                    hold.BTN_ATTACHMENT = hold.CONVERTVIEW.FindViewById<Button>(Resource.Id.btn_attachment);
                    hold.PROGRESS_INDICATOR = hold.CONVERTVIEW.FindViewById<ProgressBar>(Resource.Id.progress_working);
                    
                    hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;

                    hold.BTN_ATTACHMENT.Text = item.FileName;
                    hold.BTN_ATTACHMENT.Click += (s, e) =>
                    {
                        if (hold.BTN_ATTACHMENT.Tag != null) { return; }

                        ViewAttachment(hold, item);
                    };
                    if(item.IsAttachmentDownloaded)
                    {
                        hold.BTN_ATTACHMENT.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Rgb(180, 180, 180));
                    }
                                                           
                    parent.AddView(hold.CONVERTVIEW);
                }
                

            }

            //####################################################################################

            private void ViewAttachment(Viewholder hold, EntryAttachment item)
            {

                hold.BTN_ATTACHMENT.Tag = "BLOCKED";

                if (item.IsAttachmentDownloaded)
                {
                    hold.BTN_ATTACHMENT.Tag = null;
                    EntrySelected?.Invoke(this, new ListFeedAttachmentAdapterEntrySelected(_entry.Key, item.Key));
                }
                else
                {
                    
                    hold.PROGRESS_INDICATOR.Visibility = ViewStates.Visible;

                    TBL.SP_Object.GetNewsFeedAttachment(item,
                    delegate (Adapters.AttachmentRetrieveErrorReason reason) {

                        if(reason != AttachmentRetrieveErrorReason.RELOGIN_REQUIRED)
                        {
                            hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;
                            hold.BTN_ATTACHMENT.Tag = null;
                        }
                        
                        AttachmentRetrieveError?.Invoke(this, new AttachmentRetrieveErrorEventArgs(reason));

                    },
                    delegate (string path) {
                        
                        hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;

                        item.UpdateAttachment(path);

                        hold.BTN_ATTACHMENT.Tag = null;
                        hold.BTN_ATTACHMENT.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Rgb(180, 180, 180));

                        EntrySelected?.Invoke(this, new ListFeedAttachmentAdapterEntrySelected(_entry.Key, item.Key));

                    });

                }

            }
            
        }
        public class ListFeedAttachmentAdapterEntrySelected : EventArgs
        {
            public ListFeedAttachmentAdapterEntrySelected(string entryKey, string attachmentKey)
            {
                EntryKey = entryKey; AttachmentKey = attachmentKey;
            }
            public string EntryKey { get; }
            public string AttachmentKey { get; }
        }

    }

}

