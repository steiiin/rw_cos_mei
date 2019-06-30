using Android.Content;
using Android.Content.PM;
using Android.Support.V4.Content;
using Java.Lang;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///DataTypes
///-FeedEntry
///-ShiftsEntry
///-EntryAttachment
///
/// -Helper
/// -FileOpen
/// 
///> OK
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace rw_cos_mei
{

    public class FeedEntry
    {

        public FeedEntry(string key, string title, string body, DateTime date, string author, List<EntryAttachment> attachments)
        {
            Key = key; Title = title; Body = body; Author = author; Date = date; MarkedRead = false;

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
        public string Body { get; }
        public string Author { get; }
        public DateTime Date { get; }

        //#######################################################################################

        private Dictionary<string, EntryAttachment> _attachments;
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
            Title = "Dienstplan " + new DateTime(year, month, 1).ToString("MMMM yyyy");
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

        public EntryAttachment(string filename, string url)
        {
            Key = url + "#" + filename;
            FileName = filename; FileRemoteUrl = url;
            _attachmentLocal = string.Empty; _attachDownloaded = false;
        }
        public EntryAttachment(string key, string filename, string remoteP, string localP)
        {
            Key = key;
            FileName = filename;
            FileRemoteUrl = remoteP;
            UpdateAttachment(localP);
        }

        //#######################################################################################

        public int ID { get; set; } = -1;

        public string Key { get; }
        public string FileName { get; }
        public string FileRemoteUrl { get; }
        public string FileLocalUrl { get { return _attachmentLocal; } }

        //#######################################################################################

        public string _attachmentLocal;
        public bool _attachDownloaded;

        public bool IsAttachmentDownloaded { get { if (_attachDownloaded && System.IO.File.Exists(_attachmentLocal)) { return true; } return false; } }

        //#######################################################################################

        public void UpdateAttachment(string path)
        {
            if (System.IO.File.Exists(path))
            {
                _attachmentLocal = path;
                _attachDownloaded = true;
            }
        }

    }

    //##############################################
    
    public class Helper
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

    }

    //##############################################
    
    public class FileOpen
    {

        public static void Open(Context context, string filePath)
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
                else
                {
                    intent.SetDataAndType(uri, "*/*");
                }

                GrantPermission(context, intent, uri);

                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);

            }
            catch
            {

                Intent intent = new Intent(Intent.ActionView, uri);
                Intent choose = Intent.CreateChooser(intent, extension.Substring(1).ToUpper() + "-Datei öffnen");

                GrantPermission(context, intent, uri);

                context.StartActivity(choose);

            }


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

    }
    
}