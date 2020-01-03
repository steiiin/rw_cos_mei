using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Org.Jsoup;
using Org.Jsoup.Nodes;
using rw_cos_mei.Helper;
using System;
using TBL = rw_cos_mei.AppTable;
using Toolbar = Android.Support.V7.Widget.Toolbar;


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

            public TextView TXT_PREVIEWHINT;
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
            if (savedInstanceState != null) { entryID = savedInstanceState.GetString(BUNDLE_ENTRYKEY, ""); }
            _currentEntry = TBL.GetFeedEntry(entryID);

            if (_currentEntry == null)
            {
                Finish(); return;
            }

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
        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutString(BUNDLE_ENTRYKEY, _currentEntry.Key);
            base.OnSaveInstanceState(outState);
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
                TXT_PREVIEWHINT = FindViewById<TextView>(Resource.Id.txt_previewHint),
                LIST_ATTACHMENTS = FindViewById<LinearLayout>(Resource.Id.list_attachments)
            };

            c.TXT_DATE.Text = _currentEntry.Date.ToString("dd. MMMM yyyy");
            c.TXT_TITLE.Text = _currentEntry.Title;
            c.TXT_AUTHOR.Text = GetString(Resource.String.feedentry_from, _currentEntry.Author);

            if (string.IsNullOrEmpty(_currentEntry.BodyText))
            {
                c.TXT_BODY.Visibility = ViewStates.Gone;
                c.DIVIDER_BODY.Visibility = ViewStates.Gone;
            }
            else
            {
                c.TXT_BODY.Text = GetPlainOfHtml(_currentEntry.BodyText);
                c.DIVIDER_BODY.Visibility = ViewStates.Visible;
            }

            _currentAttachmentAdapter = new Adapters.ListFeedAttachmentAdapter(this, c.TXT_PREVIEWHINT, c.LIST_ATTACHMENTS, _currentEntry);

        }
        private void CreateToolbar()
        {

            //Toolbar erstellen
            var toolbar = FindViewById<Toolbar>(Resource.Id.appbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetDisplayShowCustomEnabled(true);
            SupportActionBar.SetDisplayShowTitleEnabled(false);

            SupportActionBar.SetHomeAsUpIndicator(Resource.Drawable.ic_action_return);
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
            if (attachment.IsDownloadable)
            {
                bool success = Helper.FileOpen.Open(this, attachment.LocalFilePath);
                if(!success) { AttachmentAdapter_AttachmentRetrieveError(null, new Adapters.AttachmentRetrieveErrorEventArgs(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR)); }
            }
            else
            {
                bool success = Helper.FileOpen.OpenWeb(this, attachment.RemoteURL);
                if (!success) { AttachmentAdapter_AttachmentRetrieveError(null, new Adapters.AttachmentRetrieveErrorEventArgs(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR)); }
            }

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
            private class PreviewViewholder
            {
                public View CONVERTVIEW;

                public ImageView PREVIEW_IMAGE;
                public ProgressBar PROGRESS_INDICATOR;
            }

            private PreviewViewholder _thisPreview;
            private float _thisPreviewRatio;
            private TextView _thisPreviewHint;

            //####################################################################################

            public ListFeedAttachmentAdapter(Context context, TextView hint, LinearLayout parent, FeedEntry entry)
            {
                _context = context;
                _entry = entry;

                _thisPreview = null;
                _thisPreviewRatio = -1;
                _thisPreviewHint = hint;
                hint.Visibility = ViewStates.Gone;

                Inflate(parent);
            }

            //####################################################################################

            private void Inflate(LinearLayout parent)
            {

                parent.RemoveAllViews();
                if (_entry.Attachments.Count == 0) { return; }

                bool firstItem = true;
                foreach (var item in _entry.Attachments)
                {
                    
                    if (firstItem)
                    {

                        //PreviewHolder erstellen
                        _thisPreview = new PreviewViewholder
                        {
                            CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feedEntry_preview, parent, false)
                        };

                        _thisPreview.PREVIEW_IMAGE = _thisPreview.CONVERTVIEW.FindViewById<ImageView>(Resource.Id.img_preview);
                        _thisPreview.PROGRESS_INDICATOR = _thisPreview.CONVERTVIEW.FindViewById<ProgressBar>(Resource.Id.progress_preview);

                        //Preview an Events koppeln
                        CreatePreview(item);
                        parent.LayoutChange += Parent_LayoutChange;

                        //Hinzufügen
                        parent.AddView(_thisPreview.CONVERTVIEW);
                        firstItem = false;

                    }

                    //Button erstellen
                    Viewholder hold = new Viewholder
                    {
                        CONVERTVIEW = LayoutInflater.FromContext(_context).Inflate(Resource.Layout.list_feedEntry_attachment, parent, false)
                    };

                    hold.BTN_ATTACHMENT = hold.CONVERTVIEW.FindViewById<Button>(Resource.Id.btn_attachment);
                    hold.PROGRESS_INDICATOR = hold.CONVERTVIEW.FindViewById<ProgressBar>(Resource.Id.progress_working);

                    hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;

                    hold.BTN_ATTACHMENT.Text = item.Title;
                    hold.BTN_ATTACHMENT.Click += (s, e) =>
                    {
                        if (hold.BTN_ATTACHMENT.Tag != null) { return; }

                        ViewAttachment(hold, item);
                    };
                    if (item.IsDownloaded)
                    {
                        hold.BTN_ATTACHMENT.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Rgb(180, 180, 180));
                    }

                    parent.AddView(hold.CONVERTVIEW);
                }


            }
            
            private void CreatePreview(EntryAttachment e)
            {
                if (!e.IsDownloadable) { _thisPreview.CONVERTVIEW.Visibility = ViewStates.Gone; return; }

                TBL.SP_Object.GetNewsFeedAttachment(e,
                delegate (Adapters.AttachmentRetrieveErrorReason reason)
                {
                    _thisPreview.CONVERTVIEW.Visibility = ViewStates.Gone;

                    _thisPreviewHint.Text = _context.GetString(Resource.String.feedentry_hint_nopreview);
                    _thisPreviewHint.Visibility = ViewStates.Visible;
                },
                delegate (string path)
                {

                    var generator = new Helper.PreviewGenerator(path);
                    if (generator.IsAvailable)
                    {

                        _thisPreview.PREVIEW_IMAGE.SetImageBitmap(generator.RenderedPreviewImage);

                        //Ratio berechnen
                        _thisPreviewRatio = (float)generator.RenderedPreviewImage.Height / (float)generator.RenderedPreviewImage.Width;
                        UpdatePreviewRatio();

                        _thisPreviewHint.Visibility = ViewStates.Visible;

                    }
                    else
                    {
                        _thisPreview.CONVERTVIEW.Visibility = ViewStates.Gone;

                        _thisPreviewHint.Text = _context.GetString(Resource.String.feedentry_hint_nopreview);
                        _thisPreviewHint.Visibility = ViewStates.Visible;
                    }

                });

            }
            private void UpdatePreviewRatio()
            {

                if (_thisPreviewRatio <= 0) { return; }
                if (_thisPreview.PREVIEW_IMAGE == null) { return; }

                int width = _thisPreview.PREVIEW_IMAGE.Width;
                int height = (int)(width * _thisPreviewRatio);

                _thisPreview.PREVIEW_IMAGE.LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, height);
                _thisPreview.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;

            }

            //####################################################################################

            private void ViewAttachment(Viewholder hold, EntryAttachment item)
            {

                hold.BTN_ATTACHMENT.Tag = "BLOCKED";
                hold.PROGRESS_INDICATOR.Visibility = ViewStates.Visible;

                TBL.SP_Object.GetNewsFeedAttachment(item,
                delegate (Adapters.AttachmentRetrieveErrorReason reason)
                {

                    if (reason != AttachmentRetrieveErrorReason.RELOGIN_REQUIRED)
                    {
                        hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;
                        hold.BTN_ATTACHMENT.Tag = null;
                    }

                    AttachmentRetrieveError?.Invoke(this, new AttachmentRetrieveErrorEventArgs(reason));

                },
                delegate (string path)
                {

                    hold.PROGRESS_INDICATOR.Visibility = ViewStates.Gone;

                    hold.BTN_ATTACHMENT.Tag = null;
                    hold.BTN_ATTACHMENT.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Rgb(180, 180, 180));

                    EntrySelected?.Invoke(this, new ListFeedAttachmentAdapterEntrySelected(_entry.Key, item.Key));

                });

            }

            //####################################################################################

            private void Parent_LayoutChange(object sender, View.LayoutChangeEventArgs e)
            {

                int width = e.Right - e.Left;
                if (width > 0) { UpdatePreviewRatio(); }

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

    //####################################################################

    namespace Helper
    {

        public class PreviewGenerator
        {

            public bool IsAvailable { get; private set; } = false;
            public Bitmap RenderedPreviewImage { get; private set; } = null;

            //####################################################################

            public PreviewGenerator(string filepath)
            {

                string extension = System.IO.Path.GetExtension(filepath).ToLower();
                switch (extension)
                {
                    case ".pdf":

                        //PDF 
                        var pdfGen = new GeneratorPDF().Generate(filepath);
                        if (pdfGen == null) { IsAvailable = false; return; }

                        RenderedPreviewImage = pdfGen;
                        IsAvailable = true;

                        return;

                    default:
                        IsAvailable = false;
                        return;
                }

            }

            //####################################################################

            public class GeneratorPDF
            {

                public Bitmap Generate(string path)
                {

                    try
                    {

                        var file = new Java.IO.File(path);
                        var fileDescriptor = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);

                        var docRenderer = new PdfRenderer(fileDescriptor);
                        if (docRenderer.PageCount == 0) { return null; }

                        var docPage = docRenderer.OpenPage(0);

                        Bitmap renderedPage = Bitmap.CreateBitmap(docPage.Width, docPage.Height, Bitmap.Config.Argb8888);
                        docPage.Render(renderedPage, null, null, PdfRenderMode.ForDisplay);

                        return renderedPage;

                    }
                    catch (Exception)
                    {
                        return null;
                    }

                }

            }

        }

    }

}



