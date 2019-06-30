using Android.Content;
using Android.Net;
using Org.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Uri = System.Uri;

using TBL = rw_cos_mei.AppTable;

namespace rw_cos_mei
{

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///SharepointAPI
    ///> OK
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public class SharepointAPI
    {

        private Context _context;

        //#########################################################

        public SharepointAPI(Context context) { _context = context; }

        //#########################################################

        private string _url_malteserHost = "https://maltesercloud.sharepoint.com";
        private string _url_getAdfs = "https://login.microsoftonline.com/GetUserRealm.srf";
        private string _url_getSpToken = "https://login.microsoftonline.com/rst2.srf";

        private string _url_endpoint = "https://maltesercloud.sharepoint.com/sites/mhd/DD/RD/RW_Mei/";

        //#########################################################

        private const int REQUEST_TIMEOUT = 12000;

        //#########################################################

        public SharepointAPIState State { get; private set; } = SharepointAPIState.WORKING;

        private string _username;
        private string _password;

        private CookieContainer _cookieJar;
        private string _digest;

        //#########################################################

        public event EventHandler<SharepointAPIStateChangedEventArgs> StateChanged;

        private void InvokeStateChanged(SharepointAPIState state)
        {
            if(State == SharepointAPIState.WRONG_LOGIN && state == SharepointAPIState.ERROR) { return; }
            State = state;
            StateChanged?.Invoke(this, new SharepointAPIStateChangedEventArgs(state));
        }

        //########################################################

        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        //########################################################

        public async Task<bool> CreateLogin()
        {

            InvokeStateChanged(SharepointAPIState.WORKING);

            if (!IsOnline()) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }
            if (string.IsNullOrWhiteSpace(_username)) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }
            if (string.IsNullOrWhiteSpace(_password)) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }

            //Cookiecontainer laden
            _cookieJar = new CookieContainer();
            _digest = string.Empty;

            try
            {

                //Anmeldung ###########################################################################################################################################
                //Federate-Login der Malteser abrufen

                string realmRequest = string.Format("login={0}&xml=1", _username.Replace("@", "%40"));

                HttpWebRequest request = await GetRequest_POSTAsync(GetRequest(new Uri(_url_getAdfs)), RequestContentType.WWW_FORM, RequestContentType.XML, realmRequest);
                if (request == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                HttpWebResponse response = await GetResponse(request);
                if (response == null) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }

                if (response.StatusCode == HttpStatusCode.OK)
                {

                    string responseData = GetResponseData(response); response.Close();

                    XElement x = XElement.Parse(responseData);
                    string NameSpaceType = x.Descendants().Where(xg => xg.Name.LocalName == "NameSpaceType").First().Value;
                    if (NameSpaceType != "Federated") { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }

                    string sts = x.Descendants().Where(xg => xg.Name.LocalName == "STSAuthURL").First().Value;
                    string auth_certificate = x.Descendants().Where(xg => xg.Name.LocalName == "Certificate").First().Value;

                    //#####################################################################################################################
                    //ADFS beantragen

                    string msgID = Guid.NewGuid().ToString("D");
                    string r_created = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff0000Z");
                    string r_expired = DateTime.Now.AddMinutes(10).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff0000Z");
                    string adfsRequest = @"<?xml version='1.0' encoding='UTF-8'?><s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:wsse='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd' xmlns:saml='urn:oasis:names:tc:SAML:1.0:assertion' xmlns:wsp='http://schemas.xmlsoap.org/ws/2004/09/policy' xmlns:wsu='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd' xmlns:wsa='http://www.w3.org/2005/08/addressing' xmlns:wssc='http://schemas.xmlsoap.org/ws/2005/02/sc' xmlns:wst='http://schemas.xmlsoap.org/ws/2005/02/trust'><s:Header><wsa:Action s:mustUnderstand='1'>http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</wsa:Action><wsa:To s:mustUnderstand='1'>{0}</wsa:To><wsa:MessageID>{1}</wsa:MessageID><ps:AuthInfo xmlns:ps='http://schemas.microsoft.com/Passport/SoapServices/PPCRL' Id='PPAuthInfo'><ps:HostingApp>Managed IDCRL</ps:HostingApp><ps:BinaryVersion>6</ps:BinaryVersion><ps:UIVersion>1</ps:UIVersion><ps:Cookies></ps:Cookies><ps:RequestParams>AQAAAAIAAABsYwQAAAAxMDMz</ps:RequestParams></ps:AuthInfo><wsse:Security><wsse:UsernameToken wsu:Id='user'><wsse:Username>{2}</wsse:Username><wsse:Password>{3}</wsse:Password></wsse:UsernameToken><wsu:Timestamp Id='Timestamp'><wsu:Created>{4}</wsu:Created><wsu:Expires>{5}</wsu:Expires></wsu:Timestamp></wsse:Security></s:Header><s:Body><wst:RequestSecurityToken Id='RST0'><wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType><wsp:AppliesTo><wsa:EndpointReference><wsa:Address>urn:federation:MicrosoftOnline</wsa:Address></wsa:EndpointReference></wsp:AppliesTo><wst:KeyType>http://schemas.xmlsoap.org/ws/2005/05/identity/NoProofKey</wst:KeyType></wst:RequestSecurityToken></s:Body></s:Envelope>";

                    request = await GetRequest_POSTAsync(GetRequest(new Uri(sts)), RequestContentType.SOAP, RequestContentType.ALL, string.Format(adfsRequest, sts, msgID, _username, _password, r_created, r_expired));
                    if (request == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                    response = await GetResponse(request);
                    if (response == null) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        responseData = GetResponseData(response); response.Close();

                        x = XElement.Parse(responseData);
                        string auth_SignatureValue = x.Descendants().Where(xg => xg.Name.LocalName == "SignatureValue").First().Value;
                        string auth_X509Certificate = x.Descendants().Where(xg => xg.Name.LocalName == "X509Certificate").First().Value;
                        string auth_DigestValue = x.Descendants().Where(xg => xg.Name.LocalName == "DigestValue").First().Value;
                        string auth_NameIdentifier = x.Descendants().Where(xg => xg.Name.LocalName == "NameIdentifier").First().Value;
                        string auth_AssertionID = x.Descendants().Where(xg => xg.Name.LocalName == "Assertion").First().Attributes("AssertionID").First().Value;
                        string auth_Issuer = x.Descendants().Where(xg => xg.Name.LocalName == "Assertion").First().Attributes("Issuer").First().Value;

                        string auth_AssertionFullXml = x.Descendants().Where(xg => xg.Name.LocalName == "Assertion").First().ToString(SaveOptions.DisableFormatting);

                        //################################################################################################################
                        //Sharepoint-Token beantragen

                        string spTokenRequest = @"<S:Envelope xmlns:S='http://www.w3.org/2003/05/soap-envelope' xmlns:wsse='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd' xmlns:wsp='http://schemas.xmlsoap.org/ws/2004/09/policy' xmlns:wsu='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd' xmlns:wsa='http://www.w3.org/2005/08/addressing' xmlns:wst='http://schemas.xmlsoap.org/ws/2005/02/trust'><S:Header><wsa:Action S:mustUnderstand='1'>http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</wsa:Action><wsa:To S:mustUnderstand='1'>https://login.microsoftonline.com/rst2.srf</wsa:To><ps:AuthInfo xmlns:ps='http://schemas.microsoft.com/LiveID/SoapServices/v1' Id='PPAuthInfo'><ps:BinaryVersion>5</ps:BinaryVersion><ps:HostingApp>Managed IDCRL</ps:HostingApp></ps:AuthInfo><wsse:Security>{0}</wsse:Security></S:Header><S:Body><wst:RequestSecurityToken xmlns:wst='http://schemas.xmlsoap.org/ws/2005/02/trust' Id='RST0'><wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType><wsp:AppliesTo><wsa:EndpointReference><wsa:Address>sharepoint.com</wsa:Address></wsa:EndpointReference></wsp:AppliesTo><wsp:PolicyReference URI='MBI'/></wst:RequestSecurityToken></S:Body></S:Envelope>";

                        request = await GetRequest_POSTAsync(GetRequest(new Uri(_url_getSpToken)), RequestContentType.SOAP, RequestContentType.ALL, string.Format(spTokenRequest, auth_AssertionFullXml));
                        if (request == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                        response = await GetResponse(request);
                        if (response == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                        if (response.StatusCode == HttpStatusCode.OK)
                        {

                            responseData = GetResponseData(response); response.Close();

                            x = XElement.Parse(responseData);
                            string auth_BinarySecurityToken = x.Descendants().Where(xg => xg.Name.LocalName == "BinarySecurityToken").First().Value;

                            //###################################################################################################################
                            //Cookies laden

                            Uri idcrlUri = new Uri(_url_malteserHost + "/_vti_bin/idcrl.svc/");

                            request = GetRequest(idcrlUri);
                            request.Headers.Set("Authorization", "BPOSIDCRL " + auth_BinarySecurityToken);
                            request.Headers.Add("X-IDCRL_ACCEPTED", "t");
                            response = await GetResponse(request);
                            if (response == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                            if (response.StatusCode == HttpStatusCode.OK)
                            {

                                //#############################################################################################################################
                                //Diggest beantragen

                                Uri digestUri = new Uri(_url_malteserHost + "/_vti_bin/sites.asmx");
                                string digestRequest = @"<?xml version='1.0' encoding='utf-8'?><soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'><soap:Body><GetUpdatedFormDigestInformation xmlns='http://schemas.microsoft.com/sharepoint/soap/' /></soap:Body></soap:Envelope>";

                                request = await GetRequest_POSTAsync(GetRequest(digestUri), RequestContentType.XML, RequestContentType.ALL, digestRequest);
                                if (request == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                                request.Headers.Add("X-RequestForceAuthentication", "true");
                                request.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
                                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                                request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sharepoint/soap/GetUpdatedFormDigestInformation");

                                response = await GetResponse(request);
                                if (response == null) { InvokeStateChanged(SharepointAPIState.ERROR); return false; }

                                if (response.StatusCode == HttpStatusCode.OK)
                                {

                                    responseData = GetResponseData(response); response.Close();

                                    x = XElement.Parse(responseData);
                                    _digest = x.Descendants().Where(xg => xg.Name.LocalName == "DigestValue").First().Value;

                                    InvokeStateChanged(SharepointAPIState.LOGGED_IN); return true;

                                }

                            }

                        }

                    }

                }

                //Fehler ###########################################################################################################################################
                //Fehler weil der Server einen Fehler gemeldet hat
                InvokeStateChanged(SharepointAPIState.ERROR); return false;

            }
            catch (Exception)
            {
                InvokeStateChanged(SharepointAPIState.ERROR); return false;
            }

        }

        //########################################################

        public async Task UpdateNewsFeed()
        {
            await UpdateNewsFeed(false);
        }
        public async Task UpdateNewsFeed(bool doNotify)
        {
           
            InvokeStateChanged(SharepointAPIState.WORKING);

            if (!IsOnline()) { InvokeStateChanged(SharepointAPIState.ERROR); return; }

            if (await CreateLogin())
            {

                string query = "_api/web/lists/getbytitle('Neues%20aus%20der%20Rettungswache')/items?$select=ID,Title,Body,Modified,AttachmentFiles,Author/Name,Author/Title&$orderby=Modified%20desc&$expand=AttachmentFiles,Author/Id";

                try
                {
                   
                    HttpWebRequest request = GetRequest(new Uri(_url_endpoint + query));
                    request.Headers.Add("X-RequestDigest", _digest);
                    request.Accept = "application/json; odata=verbose";
                    HttpWebResponse response = await GetResponse(request);
                    if (response == null) { InvokeStateChanged(SharepointAPIState.ERROR); return; }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        string responseData = GetResponseData(response); response.Close();

                        JSONObject feedDoc = new JSONObject(responseData);

                        var results = feedDoc.GetJSONObject("d").GetJSONArray("results");

                        var listFeed = new List<FeedEntry>();
                        for (int i = 0; i < results.Length(); i++)
                        {
                            var c = results.GetJSONObject(i);

                            string title = c.GetString("Title");
                            string key = title.Trim(' ').ToLower() + "#" + c.GetString("Id");

                            string body = c.GetString("Body");
                            if (body == "null") { body = string.Empty; }
                            if (!DateTime.TryParse(c.GetString("Modified"), out DateTime date)) { date = DateTime.Now; }
                            string author = c.GetJSONObject("Author").GetString("Title");

                            List<EntryAttachment> attachmentList = new List<EntryAttachment>();
                            var attachmentObjects = c.GetJSONObject("AttachmentFiles").GetJSONArray("results");

                            for (int j = 0; j < attachmentObjects.Length(); j++)
                            {
                                var d = attachmentObjects.GetJSONObject(j);

                                string filename = d.GetString("FileName");
                                string rel_url = d.GetString("ServerRelativeUrl");

                                EntryAttachment attachment = new EntryAttachment(filename, rel_url);
                                attachmentList.Add(attachment);
                            }

                            FeedEntry entry = new FeedEntry(key, title, body, date, author, attachmentList);
                            listFeed.Add(entry);

                        }

                        TBL.UpdateEntries(listFeed, doNotify);

                        InvokeStateChanged(SharepointAPIState.OK);
                        return;

                    }

                }
                catch (Exception) { }

            }

            InvokeStateChanged(SharepointAPIState.ERROR);

        }

        public void GetNewsFeedAttachment(EntryAttachment attachment, Action<int> onError, Action<string> onDownloaded, bool relogin = true)
        {

            string tmpRaw = Path.GetTempFileName();
            string nameEsc = Path.GetFileNameWithoutExtension(attachment.FileName).Replace(" ", "_");

            string extension = Path.GetExtension(attachment.FileRemoteUrl);
            string filePath = Path.GetDirectoryName(tmpRaw) + Path.DirectorySeparatorChar + nameEsc + Path.GetFileNameWithoutExtension(tmpRaw) + extension;
            Uri fileAdress = new Uri(_url_malteserHost + attachment.FileRemoteUrl);
            
            //Request erstellen
            if (!IsOnline()) { onError(0); return; }

            var client = new DownloadClient(fileAdress, filePath, _cookieJar, _digest);
            client.DownloadProgressChanged += (ss, ee) => { };
            client.DownloadFinished += async (ss, ee) =>
            {
                if (ee.DownloadFinished)
                {
                    onDownloaded(filePath);
                    return;
                }
                
                if (relogin)
                {
                    await CreateLogin();
                    GetNewsFeedAttachment(attachment, onError, onDownloaded, false);
                }
                else
                {
                    onError(0);
                }
                return;

            };
            client.StartDownloadCallback();

        }

        //########################################################

        private bool IsOnline()
        {
            ConnectivityManager cm = (ConnectivityManager)_context.GetSystemService(Context.ConnectivityService);
            NetworkInfo netInfo = cm.ActiveNetworkInfo;
            return netInfo != null && netInfo.IsConnected;
        }

        private HttpWebRequest GetRequest(Uri host, string relative)
        {
            return GetRequest(new Uri(host, relative));
        }
        private HttpWebRequest GetRequest(Uri uri)
        {
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.Timeout = REQUEST_TIMEOUT;
            request.CookieContainer = _cookieJar;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:56.0) Gecko/20100101 Firefox/63.0";
            request.Accept = "text/html, application/xhtml+xml, */*";
            request.Headers.Add("Accept-Language", "de-DE");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.AllowAutoRedirect = false;
            request.ServicePoint.Expect100Continue = false;

            return request;
        }

        private async Task<HttpWebResponse> GetResponse(HttpWebRequest request)
        {
            try
            {
                HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
                if (response.StatusCode == HttpStatusCode.Found)
                {
                    response.Close();
                    request = GetRequest(response.ResponseUri, response.Headers.Get("Location"));
                    request.Referer = response.ResponseUri.ToString();
                    response = await GetResponse(request);
                }
                return response;
            }
            catch (Exception)
            {
                return null;
            }


        }
        private string GetResponseData(HttpWebResponse response)
        {

            string result = string.Empty;

            StreamReader stream = new StreamReader(response.GetResponseStream());
            result = stream.ReadToEnd();
            stream.Close();

            return result;

        }

        private enum RequestContentType
        {
            ALL,
            JSON,
            WWW_FORM,
            XML,
            SOAP
        }
        private async Task<HttpWebRequest> GetRequest_POSTAsync(HttpWebRequest request, RequestContentType content, RequestContentType accept, string post)
        {

            //Post in Byte kodieren
            byte[] data = new System.Text.UTF8Encoding().GetBytes(post);

            //Request anpassen
            request.Method = "POST";

            switch (accept)
            {
                case RequestContentType.ALL:
                    request.Accept = "*/*;";

                    break;
                case RequestContentType.JSON:
                    request.Accept = "application / json; odata = verbose";

                    break;
                case RequestContentType.WWW_FORM:
                    request.Accept = "application/x-www-form-urlencoded;";

                    break;
                case RequestContentType.XML:
                    request.Accept = "text/xml;";

                    break;
                case RequestContentType.SOAP:
                    request.Accept = "application/soap+xml;";

                    break;
            }

            switch (content)
            {
                case RequestContentType.JSON:
                    request.ContentType = "application/json; odata=verbose";

                    break;
                case RequestContentType.WWW_FORM:
                    request.ContentType = "application/x-www-form-urlencoded;";

                    break;
                case RequestContentType.XML:
                    request.ContentType = "text/xml;";

                    break;
                case RequestContentType.SOAP:
                    request.ContentType = "application/soap+xml;";

                    break;
            }

            request.ContentLength = data.Length;

            //POST-Stream einfügen
            if (string.IsNullOrEmpty(post)) { return request; }

            try
            {

                Stream poststream = await request.GetRequestStreamAsync();
                poststream.Write(data, 0, data.Length);
                poststream.Close();

                return request;

            }
            catch (Exception)
            {
                return null;
            }

        }

        //########################################################

        private class DownloadClientFinishedEventArgs
        {
            public DownloadClientFinishedEventArgs(bool finished) { DownloadFinished = finished; }
            public bool DownloadFinished { get; }
        }

        private class DownloadClient
        {

            public event EventHandler<int> DownloadProgressChanged;
            public event EventHandler<DownloadClientFinishedEventArgs> DownloadFinished;

            //##################################################################################

            private string _filePath;
            private Uri _remotePath;
            private CookieContainer _cookieJar;
            private string _digest;

            //##################################################################################

            public DownloadClient(Uri remotePath, string localPath, CookieContainer jar, string digest)
            {

                _filePath = localPath;
                _remotePath = remotePath;
                _cookieJar = jar;
                _digest = digest;

            }

            public async void StartDownloadCallback()
            {

                try
                {

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_remotePath);

                    request.Method = "GET";
                    request.Timeout = REQUEST_TIMEOUT;
                    request.CookieContainer = _cookieJar;
                    request.Headers.Add("X-RequestDigest", _digest);
                    if (request == null) { DownloadFinished?.Invoke(this, new DownloadClientFinishedEventArgs(false)); return; }

                    HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (WriteFile(response, _filePath))
                        {
                            DownloadFinished?.Invoke(this, new DownloadClientFinishedEventArgs(true));
                            return;
                        }
                    }

                }
                catch (Exception) { }

                DownloadFinished?.Invoke(this, new DownloadClientFinishedEventArgs(false));

            }

            //##################################################################################

            private bool WriteFile(HttpWebResponse response, string filePath)
            {
                long total = 0;
                long received = 0;

                try
                {

                    byte[] buffer = new byte[1024];

                    FileStream fileStream = File.OpenWrite(_filePath);
                    using (Stream input = response.GetResponseStream())
                    {
                        total = input.Length;

                        int size = input.Read(buffer, 0, buffer.Length);
                        while (size > 0)
                        {
                            fileStream.Write(buffer, 0, size);
                            received += size;
                            DownloadProgressChanged?.Invoke(this, (int)(received / total) * 100);

                            size = input.Read(buffer, 0, buffer.Length);
                        }

                        fileStream.Flush();
                        fileStream.Close();
                    }

                    FileInfo fi = new FileInfo(_filePath);

                    return System.IO.File.Exists(_filePath);

                }
                catch (Exception) { }

                return false;

            }

        }

    }

    //######################################################################

    public enum SharepointAPIState
    {
        WORKING,
        LOGGED_IN,
        OK,
        WRONG_LOGIN,
        ERROR
    }

    public class SharepointAPIStateChangedEventArgs
    {
        public SharepointAPIStateChangedEventArgs(SharepointAPIState state) { State = state; }
        public SharepointAPIState State { get; }
    }

}