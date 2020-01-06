using Android.Content;
using Android.Content.PM;
using Android.Support.V4.Content;
using Java.Lang;
using Org.Jsoup.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///DataTypes
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    public class FeedEntry
    {

        public FeedEntry(string key, string title, DateTime date, string author, string body, List<EntryAttachment> attachments)
        {
            Key = key; Title = title; Author = author; Date = date; MarkedRead = false;

            BodyText = body;

            Dictionary<string, EntryAttachment> dictAttachment = new Dictionary<string, EntryAttachment>();
            foreach (var item in attachments)
            {
                dictAttachment.Add(item.Key, item);
            }
            _attachments = dictAttachment;
        }

        //#######################################################################################

        public int ID { get; set; } = -1;
        public string Key { get; }
        public string Title { get; }
        public string Author { get; }
        public DateTime Date { get; }
        public string BodyText { get; }

        //#######################################################################################

        private readonly Dictionary<string, EntryAttachment> _attachments;
        public List<EntryAttachment> Attachments { get { return _attachments.Values.ToList(); } }
        public EntryAttachment GetAttachment(string Key) { return _attachments[Key]; }

        //#######################################################################################

        public bool MarkedRead { get; set; }

    }
    public class ShiftsEntry
    {

        public ShiftsEntry(int month, int year, DateTime lastUpdate, string lastVersion, EntryAttachment shiftAttachment)
        {
            Key = year.ToString("0000") + "#" + month.ToString("00");
            Month = month; Year = year; LastUpdate = lastUpdate; LastVersion = lastVersion; MarkedRead = false;
            Title = new DateTime(year, month, 1).ToString("MMMM yyyy");
            ShiftAttachment = shiftAttachment;
        }

        //#######################################################################################

        public int ID { get; set; } = -1;
        public string Key { get; }
        public int Month { get; }
        public int Year { get; }
        public string Title { get; }

        public DateTime LastUpdate { get; }
        public string LastVersion { get; }

        //#######################################################################################

        public EntryAttachment ShiftAttachment { get; }

        //#######################################################################################

        public bool MarkedRead { get; set; }

    }

    public class EntryAttachment
    {

        public const string DOWNLOADABLE_HASH = "##only_link##";

        public EntryAttachment(string title, string url, bool isLink)
        {
            Key = url + "#" + title;
            Title = title;

            IsDownloadable = false;
            if (isLink)
            {

                //Sharepointlink bereinigen
                url = url.Replace(":/r", "").Replace("/:b", "").Replace("/:w", "").Replace("/:p", "").Replace("/:x", "").Replace("/:f", "");

                //Check, ob eine Datei vorliegt. Berücksichtigt NUR PDF, DOCX, XLSX, PPTX, DOC, XLS, PPT, TXT.
                bool isFile = false;
                string url_extension = Path.GetExtension(url).ToLower();
                if (url_extension.Contains("?")) { url_extension = url_extension.Substring(0, url_extension.IndexOf("?")); }
                if (new List<string>() { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" }.Contains(url_extension)) { isFile = true; }

                if (isFile)
                {
                    url = Path.GetDirectoryName(url) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(url) + url_extension;
                    IsDownloadable = true;
                }

            }
            else
            {
                IsDownloadable = true;
            }

            RemoteURL = url;

            _isDownloaded = false;
            _localPath = string.Empty;

        }
        public EntryAttachment(string key, string title, string url, string localpath)
        {
            Key = key;
            Title = title;
            RemoteURL = url;

            if (localpath == DOWNLOADABLE_HASH)
            {
                _localPath = string.Empty;
                IsDownloadable = false;
            }
            else
            {
                _localPath = localpath;
                IsDownloadable = true;
            }

        }

        //#######################################################################################

        public int ID { get; set; } = -1;

        public string Key { get; }
        public string Title { get; }

        public string RemoteURL { get; }

        public bool IsDownloadable { get; }

        public bool IsDownloaded { get { if (_isDownloaded && System.IO.File.Exists(LocalFilePath)) { return true; } return false; } }
        public string LocalFilePath { get { if (IsDownloadable) { return _localPath; } else { return DOWNLOADABLE_HASH; } } }

        private bool _isDownloaded = false;
        private string _localPath = string.Empty;

        //#######################################################################################

        public void UpdateAttachmentDownloaded(string localPath)
        {
            if (!IsDownloadable) { return; }
            if (System.IO.File.Exists(localPath))
            {
                _localPath = localPath;
                _isDownloaded = true;
            }
        }

    }

    //##############################################

    namespace Helper
    {

        public enum HandlerMethod
        {
            ADD_HANDLERS,
            REMOVE_HANDLERS
        }

        public class Constant
        {

            public const string STATE_BLOCKED = "#BLOCKED#";

        }

        public class Converter
        {

            public static bool IsTextInteger(string item)
            {
                try
                {
                    int num = Integer.ParseInt(item);
                    return true;
                }
                catch
                {
                    //String enthielt keine Integer; 
                    return false;
                }
            }

            public static string GetPlainOfHtml(string html)
            {

                string plain = "";
                Document doc = Org.Jsoup.Jsoup.ParseBodyFragment(html); var it = doc.Select("*");

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

                    plain += e.OwnText();

                }
                return plain;

            }

        }

        public class FileOpen
        {

            public static bool Open(Context context, string filePath)
            {

                string extension = System.IO.Path.GetExtension(filePath).ToLower();

                Java.IO.File file = new Java.IO.File(filePath);
                Android.Net.Uri uri = FileProvider.GetUriForFile(context, "com.steiiin.rw_cos_mei.FileProvider", file);

                try
                {

                    Intent intent = new Intent(Intent.ActionView);
                    if (extension == ".doc" || extension == ".docx")
                    {
                        intent.SetDataAndType(uri, "application/msword");
                    }
                    else if (extension == ".pdf")
                    {
                        intent.SetDataAndType(uri, "application/pdf");
                    }
                    else if (extension == ".xls" || extension == ".xlsx")
                    {
                        intent.SetDataAndType(uri, "application/vnd.ms-excel");
                    }
                    else if (extension == ".txt")
                    {
                        intent.SetDataAndType(uri, "text/plain");
                    }
                    else if (extension == ".bmp")
                    {
                        intent.SetDataAndType(uri, "image/bmp");
                    }
                    else if (extension == ".jpg" || extension == ".jpeg")
                    {
                        intent.SetDataAndType(uri, "image/jpeg");
                    }
                    else if (extension == ".gif")
                    {
                        intent.SetDataAndType(uri, "image/gif");
                    }
                    else if (extension == ".png")
                    {
                        intent.SetDataAndType(uri, "image/png");
                    }
                    else if (extension == ".webp")
                    {
                        intent.SetDataAndType(uri, "image/webp");
                    }
                    else
                    {
                        intent.SetDataAndType(uri, "*/*");
                    }

                    GrantPermission(context, intent, uri);

                    intent.AddFlags(ActivityFlags.NewTask);
                    context.StartActivity(intent);

                    return true;

                }
                catch { }

                try
                {

                    Intent intent = new Intent(Intent.ActionView, uri);
                    Intent choose = Intent.CreateChooser(intent, extension.Substring(1).ToUpper() + "-Datei öffnen");

                    GrantPermission(context, intent, uri);

                    context.StartActivity(choose);

                    return true;

                }
                catch { }

                return false;

            }
            private static void GrantPermission(Context c, Intent i, Android.Net.Uri uri)
            {

                List<ResolveInfo> resInfoList = c.PackageManager.QueryIntentActivities(i, PackageInfoFlags.MatchDefaultOnly).ToList();
                foreach (var item in resInfoList)
                {
                    string packageName = item.ActivityInfo.PackageName;
                    c.GrantUriPermission(packageName, uri, ActivityFlags.GrantReadUriPermission);
                }

            }

            public static bool OpenWeb(Context context, string url)
            {

                try
                {

                    Intent i = new Intent(Intent.ActionView);
                    i.SetData(Android.Net.Uri.Parse(url));
                    context.StartActivity(i);

                    return true;

                }
                catch { }

                return false;

            }

        }

    }

}