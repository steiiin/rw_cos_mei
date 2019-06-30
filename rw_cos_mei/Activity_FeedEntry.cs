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
            
            var adapter = new Adapters.ListFeedAttachmentAdapter(this, c.LIST_ATTACHMENTS, _currentEntry);
            adapter.EntrySelected += (sender, entry) =>
            {

                EntryAttachment attachment = TBL.GetFeedEntry(entry.EntryKey)?.GetAttachment(entry.AttachmentKey);
                FileOpen.Open(this, attachment.FileLocalUrl);

            };
            adapter.AttachmentRetrieveError += (sender, e) => {

                //Snackbar aufrufen
                View rootView = this.Window.DecorView.FindViewById(Android.Resource.Id.Content);
                Snackbar snack = Snackbar.Make(rootView, Resource.String.main_dialog_error_attachment_snack, Snackbar.LengthLong);
                snack.Show();

            };

        }

        private void CreateToolbar()
        {

            //Toolbar erstellen
            var toolbar = FindViewById<Toolbar>(Resource.Id.appbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetDisplayShowCustomEnabled(true);
            SupportActionBar.SetDisplayShowTitleEnabled(false);

            SupportActionBar.SetHomeAsUpIndicator(Resource.Drawable.ic_action_upArrow);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);

            c.APPBAR_TITLE.Text = _currentEntry.Date.ToString("dd. MMMM yyyy");

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

            private Context _context;
            private FeedEntry _entry;
            
            //####################################################################################

            public event EventHandler<ListFeedAttachmentAdapterEntrySelected> EntrySelected;

            public event EventHandler AttachmentRetrieveError;

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

            private void Inflate(LinearLayout parent)
            {

                parent.RemoveAllViews();
                if(_entry.Attachments.Count == 0) { return; }

                foreach (var item in _entry.Attachments)
                {

                    Viewholder hold = new Viewholder();
                    hold.CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feedEntry_attachment, parent, false);
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
                    delegate (int error) {

                        hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;
                        AttachmentRetrieveError?.Invoke(this, new EventArgs());

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

        public class ListFeedAttachmentAdapterEntrySelected
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

