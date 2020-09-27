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
using TBL = rw_cos_mei.AppTable;
using Uri = System.Uri;

namespace rw_cos_mei
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///SharepointAPI
    ///> OK
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public class SharepointAPI
    {
        private readonly Context _context;

        //#########################################################

        public SharepointAPI(Context context)
        {
            _context = context;
        }

        //#########################################################

        private readonly string _url_malteserHost = "https://maltesercloud.sharepoint.com";
        private readonly string _url_getAdfs = "https://login.microsoftonline.com/GetUserRealm.srf";
        private readonly string _url_getSpToken = "https://login.microsoftonline.com/rst2.srf";

        private readonly string _url_endpoint = "https://maltesercloud.sharepoint.com/sites/hilfsdienst/2601/"; 

        //#########################################################

        private const int REQUEST_TIMEOUT = 10000;

        //#########################################################

        public SharepointAPIState State { get; private set; } = SharepointAPIState.OFFLINE;

        private string _username;
        private string _password;

        private string _bearer;
        private string _spOauth;

        //#########################################################

        public event EventHandler<SharepointAPIStateChangedEventArgs> StateChanged;
        private void InvokeStateChanged(SharepointAPIState state)
        {
            State = state;
            StateChanged?.Invoke(this, new SharepointAPIStateChangedEventArgs(state));
        }

        //########################################################

        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }
        public void SetTokens(string token, string oAuth)
        {
            _bearer = token;
            _spOauth = oAuth;
        }

        //########################################################

        private CookieContainer _cookieJar;

        public async Task<SharepointAPIState> CreateLogin()
        {
            InvokeStateChanged(SharepointAPIState.WORKING);

            if (!IsOnline()) { return SharepointAPIState.CONNECTION_LOST; }
            if (string.IsNullOrWhiteSpace(_username)) { return SharepointAPIState.WRONG_LOGIN; }
            if (string.IsNullOrWhiteSpace(_password)) { return SharepointAPIState.WRONG_LOGIN; }

            //Init
            _bearer = string.Empty;
            _spOauth = string.Empty;
            _cookieJar = new CookieContainer();

            try
            {
                //Anmeldung ###########################################################################################################################################
                //Federate-Login der Malteser abrufen

                string realmRequest = string.Format("login={0}&xml=1", _username.Replace("@", "%40"));

                HttpWebRequest request = await GetRequest_POSTAsync(GetRequest(new Uri(_url_getAdfs)), RequestContentType.WWW_FORM, RequestContentType.XML, realmRequest);
                if (request == null) { return SharepointAPIState.CONNECTION_LOST; }

                ResponseObject response = await GetResponse(request);
                switch (response.StatusCode)
                {
                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                        return SharepointAPIState.CONNECTION_LOST;

                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                        return SharepointAPIState.WRONG_LOGIN;

                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                    default:

                        return SharepointAPIState.SERVER_ERROR;

                    case ResponseObject.ResponseObjectStatusCode.OK:

                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                        {
                            string responseData = GetResponseData(response.Response); response.Close();

                            XElement x = XElement.Parse(responseData);
                            string NameSpaceType = x.Descendants().Where(xg => xg.Name.LocalName == "NameSpaceType").First().Value;
                            if (NameSpaceType != "Federated") { return SharepointAPIState.WRONG_LOGIN; }

                            string sts = x.Descendants().Where(xg => xg.Name.LocalName == "STSAuthURL").First().Value;
                            string auth_certificate = x.Descendants().Where(xg => xg.Name.LocalName == "Certificate").First().Value;

                            //#####################################################################################################################
                            //ADFS beantragen

                            string msgID = Guid.NewGuid().ToString("D");
                            string r_created = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff0000Z");
                            string r_expired = DateTime.Now.AddMinutes(10).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff0000Z");
                            string adfsRequest = @"<?xml version='1.0' encoding='UTF-8'?><s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:wsse='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd' xmlns:saml='urn:oasis:names:tc:SAML:1.0:assertion' xmlns:wsp='http://schemas.xmlsoap.org/ws/2004/09/policy' xmlns:wsu='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd' xmlns:wsa='http://www.w3.org/2005/08/addressing' xmlns:wssc='http://schemas.xmlsoap.org/ws/2005/02/sc' xmlns:wst='http://schemas.xmlsoap.org/ws/2005/02/trust'><s:Header><wsa:Action s:mustUnderstand='1'>http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</wsa:Action><wsa:To s:mustUnderstand='1'>{0}</wsa:To><wsa:MessageID>{1}</wsa:MessageID><ps:AuthInfo xmlns:ps='http://schemas.microsoft.com/Passport/SoapServices/PPCRL' Id='PPAuthInfo'><ps:HostingApp>Managed IDCRL</ps:HostingApp><ps:BinaryVersion>6</ps:BinaryVersion><ps:UIVersion>1</ps:UIVersion><ps:Cookies></ps:Cookies><ps:RequestParams>AQAAAAIAAABsYwQAAAAxMDMz</ps:RequestParams></ps:AuthInfo><wsse:Security><wsse:UsernameToken wsu:Id='user'><wsse:Username>{2}</wsse:Username><wsse:Password>{3}</wsse:Password></wsse:UsernameToken><wsu:Timestamp Id='Timestamp'><wsu:Created>{4}</wsu:Created><wsu:Expires>{5}</wsu:Expires></wsu:Timestamp></wsse:Security></s:Header><s:Body><wst:RequestSecurityToken Id='RST0'><wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType><wsp:AppliesTo><wsa:EndpointReference><wsa:Address>urn:federation:MicrosoftOnline</wsa:Address></wsa:EndpointReference></wsp:AppliesTo><wst:KeyType>http://schemas.xmlsoap.org/ws/2005/05/identity/NoProofKey</wst:KeyType></wst:RequestSecurityToken></s:Body></s:Envelope>";

                            request = await GetRequest_POSTAsync(GetRequest(new Uri(sts)), RequestContentType.SOAP, RequestContentType.ALL, string.Format(adfsRequest, sts, msgID, _username, _password, r_created, r_expired));
                            if (request == null) { return SharepointAPIState.CONNECTION_LOST; }

                            response = await GetResponse(request);
                            switch (response.StatusCode)
                            {
                                case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                    return SharepointAPIState.CONNECTION_LOST;

                                case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                    return SharepointAPIState.WRONG_LOGIN;

                                case ResponseObject.ResponseObjectStatusCode.ERROR:
                                case ResponseObject.ResponseObjectStatusCode.UNSET:
                                default:

                                    return SharepointAPIState.SERVER_ERROR;

                                case ResponseObject.ResponseObjectStatusCode.OK:

                                    if (response.Response?.StatusCode == HttpStatusCode.OK)
                                    {
                                        responseData = GetResponseData(response.Response); response.Close();

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
                                        if (request == null) { return SharepointAPIState.CONNECTION_LOST; }

                                        response = await GetResponse(request);
                                        switch (response.StatusCode)
                                        {
                                            case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                                return SharepointAPIState.CONNECTION_LOST;

                                            case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                return SharepointAPIState.WRONG_LOGIN;

                                            case ResponseObject.ResponseObjectStatusCode.ERROR:
                                            case ResponseObject.ResponseObjectStatusCode.UNSET:
                                            default:

                                                return SharepointAPIState.SERVER_ERROR;

                                            case ResponseObject.ResponseObjectStatusCode.OK:

                                                if (response.Response?.StatusCode == HttpStatusCode.OK)
                                                {
                                                    responseData = GetResponseData(response.Response); response.Close();

                                                    x = XElement.Parse(responseData);
                                                    string auth_BinarySecurityToken = x.Descendants().Where(xg => xg.Name.LocalName == "BinarySecurityToken").First().Value;

                                                    //###################################################################################################################
                                                    //Cookies laden

                                                    Uri idcrlUri = new Uri(_url_malteserHost + "/_vti_bin/idcrl.svc/");

                                                    request = GetRequest(idcrlUri);
                                                    request.Headers.Set("Authorization", "BPOSIDCRL " + auth_BinarySecurityToken);
                                                    request.Headers.Add("X-IDCRL_ACCEPTED", "t");

                                                    response = await GetResponse(request);
                                                    switch (response.StatusCode)
                                                    {
                                                        case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                                            return SharepointAPIState.CONNECTION_LOST;

                                                        case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                            return SharepointAPIState.WRONG_LOGIN;

                                                        case ResponseObject.ResponseObjectStatusCode.ERROR:
                                                        case ResponseObject.ResponseObjectStatusCode.UNSET:
                                                        default:

                                                            return SharepointAPIState.SERVER_ERROR;

                                                        case ResponseObject.ResponseObjectStatusCode.OK:

                                                            if (response.Response?.StatusCode == HttpStatusCode.OK)
                                                            {
                                                                //#############################################################################################################################
                                                                //Digest beantragen

                                                                Uri digestUri = new Uri(_url_malteserHost + "/_vti_bin/sites.asmx");
                                                                string digestRequest = @"<?xml version='1.0' encoding='utf-8'?><soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'><soap:Body><GetUpdatedFormDigestInformation xmlns='http://schemas.microsoft.com/sharepoint/soap/' /></soap:Body></soap:Envelope>";

                                                                request = await GetRequest_POSTAsync(GetRequest(digestUri), RequestContentType.XML, RequestContentType.ALL, digestRequest);
                                                                if (request == null) { return SharepointAPIState.CONNECTION_LOST; }

                                                                request.Headers.Add("X-RequestForceAuthentication", "true");
                                                                request.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
                                                                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                                                                request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sharepoint/soap/GetUpdatedFormDigestInformation");

                                                                response = await GetResponse(request);
                                                                switch (response.StatusCode)
                                                                {
                                                                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                                                        return SharepointAPIState.CONNECTION_LOST;

                                                                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                                        return SharepointAPIState.WRONG_LOGIN;

                                                                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                                                                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                                                                    default:

                                                                        return SharepointAPIState.SERVER_ERROR;

                                                                    case ResponseObject.ResponseObjectStatusCode.OK:

                                                                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                                                                        {
                                                                            responseData = GetResponseData(response.Response); response.Close();
                                                                            x = XElement.Parse(responseData);

                                                                            _bearer = x.Descendants().Where(xg => xg.Name.LocalName == "DigestValue").First().Value;
                                                                            _spOauth = _cookieJar.GetCookies(new Uri(_url_malteserHost))["SPOIDCRL"].Value;

                                                                            TBL.UpdateTokens(_bearer, _spOauth);

                                                                            return SharepointAPIState.LOGGED_IN;
                                                                        }
                                                                        return SharepointAPIState.SERVER_ERROR;
                                                                }
                                                            }
                                                            return SharepointAPIState.SERVER_ERROR;
                                                    }
                                                }
                                                return SharepointAPIState.SERVER_ERROR;
                                        }
                                    }
                                    return SharepointAPIState.SERVER_ERROR;
                            }
                        }
                        return SharepointAPIState.SERVER_ERROR;
                }
            }
            catch (Exception)
            {
                return SharepointAPIState.SERVER_ERROR;
            }
        }

        //########################################################

        public async Task UpdateNewsFeed()
        {
            await UpdateNewsFeed(false, true);
        }
        public async Task<SharepointAPIState> UpdateNewsFeed(bool doNotify, bool relogin)
        {
            List<string> feedAnchors = new List<string>
            {
                "rw_Mei/news_mei/"
                //"QM/",
                //""
            };
            
            var state = SharepointAPIState.WORKING;
            InvokeStateChanged(SharepointAPIState.WORKING);

            foreach (var item in feedAnchors)
            {
                state = await RetrieveNewsFeed(doNotify, relogin, item);
            }

            InvokeStateChanged(state);
            return state;

        }
        private async Task<SharepointAPIState> RetrieveNewsFeed(bool doNotify, bool relogin, string host)
        {

            InvokeStateChanged(SharepointAPIState.WORKING);

            if (!IsOnline()) { return SharepointAPIState.CONNECTION_LOST; } // InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return; }
            if (string.IsNullOrWhiteSpace(_username)) { return SharepointAPIState.WRONG_LOGIN; } // InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return; }
            if (string.IsNullOrWhiteSpace(_password)) { return SharepointAPIState.WRONG_LOGIN; } // InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return; }

            if (!string.IsNullOrWhiteSpace(_spOauth)) { _cookieJar = CreateOAuthCookie(); } else { _cookieJar = new CookieContainer(); }

            var listFeed = new List<FeedEntry>();

            string query = host + "_api/SitePages/pages?$select=Id,Title,Modified,CanvasJson1,lastModifiedBy,promotedState,Url&$orderby=Modified%20desc&$expand=lastModifiedBy"; // "_api/web/lists/getbytitle('news_mei')/items?$select=ID,Title,Body,Modified,AttachmentFiles,Author/Name,Author/Title&$orderby=Modified%20desc&$expand=AttachmentFiles,Author/Id";
            try
            {
                HttpWebRequest request = GetRequest(new Uri(_url_endpoint + query));
                request.Headers.Add("X-RequestDigest", _bearer);
                request.Accept = "application/json; odata=verbose";

                ResponseObject response = await GetResponse(request);
                switch (response.StatusCode)
                {
                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                        return SharepointAPIState.CONNECTION_LOST; //InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return;

                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                        if (relogin)
                        {
                            var loginState = await CreateLogin();
                            if (loginState == SharepointAPIState.LOGGED_IN)
                            {
                                return await RetrieveNewsFeed(doNotify, false, host);
                            }
                            return loginState; //InvokeStateChanged(loginState); return;
                        }

                        return SharepointAPIState.WRONG_LOGIN; // InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return;

                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                    default:

                        return SharepointAPIState.SERVER_ERROR; //InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return;

                    case ResponseObject.ResponseObjectStatusCode.OK:

                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                        {
                            string responseData = GetResponseData(response.Response); response.Close();

                            JSONObject feedDoc = new JSONObject(responseData);
                            var results = feedDoc.GetJSONObject("d").GetJSONArray("results");

                            for (int i = 0; i < results.Length(); i++)
                            {
                                var c = results.GetJSONObject(i);

                                const string TITLE = "Title";
                                const string ID = "Id";
                                const string MODIFIED = "Modified";
                                const string AUTHOR = "LastModifiedBy";
                                const string STATE = "PromotedState";
                                const string CONTENT = "CanvasJson1";
                                const string PAGEURL = "Url";

                                if (!c.Has(TITLE) || !c.Has(ID) || !c.Has(MODIFIED) || !c.Has(AUTHOR) || !c.Has(STATE) || !c.Has(PAGEURL)) { break; }

                                string title = c.GetString(TITLE);
                                string key = "#" + c.GetInt(ID).ToString() + "_" + title.Trim(' ').ToLower();

                                if (!DateTime.TryParse(c.GetString(MODIFIED), out DateTime date)) { date = DateTime.Now; }

                                string author = _context.GetString(Resource.String.feedentry_unknown);
                                if (c.GetJSONObject(AUTHOR).Has("Name")) { author = c.GetJSONObject(AUTHOR).GetString("Name"); }

                                string body = "";
                                //string body = _url_endpoint + "/" + host + c.GetString(PAGEURL);

                                bool isVisible = c.GetInt(STATE) == 2; //Promoted, sonst ausgeblendet
                                if (isVisible && c.Has(CONTENT))
                                {
                                    //Dokument parsen
                                    string contentText = c.GetString(CONTENT);
                                    var contentJson = GetJsonArray(contentText);
                                    if (contentJson == null) { break; }

                                    var attachmentList = new List<EntryAttachment>();
                                    for (int j = 0; j < contentJson.Length(); j++)
                                    {
                                        const string WEBPARTS = "webPartData";
                                        if (contentJson.GetJSONObject(j).Has(WEBPARTS))
                                        {

                                            var webPartData = contentJson.GetJSONObject(j).GetJSONObject(WEBPARTS);
                                            if (webPartData != null)
                                            {
                                                string webPartId = webPartData.GetString("id");

                                                switch (webPartId)
                                                {
                                                    case "b7dd04e1-19ce-4b24-9132-b60a1c2b910d":

                                                        //Eingebettetes Dokument --> Als Anhang speichern
                                                        if (!webPartData.Has("properties") || !webPartData.GetJSONObject("properties").Has("file")) { break; }

                                                        string fileUrl = webPartData.GetJSONObject("properties").GetString("file");
                                                        string fileName = Path.GetFileName(fileUrl);
                                                        attachmentList.Add(new EntryAttachment(fileName, fileUrl, false));

                                                        break;

                                                    case "6410b3b6-d440-4663-8744-378976dc041e":

                                                        //Link --> Wenn Datei als Anhang, sonst als Hyperlink
                                                        if (!webPartData.Has("serverProcessedContent") ||
                                                            !webPartData.GetJSONObject("serverProcessedContent").Has("links") ||
                                                            !webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("links").Has("url")) { break; }
                                                        if (!webPartData.GetJSONObject("serverProcessedContent").Has("searchablePlainTexts") ||
                                                            !webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("searchablePlainTexts").Has("title")) { break; }

                                                        string url = webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("links").GetString("url");
                                                        string link_title = webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("searchablePlainTexts").GetString("title");

                                                        if (url.StartsWith("/:")) { url = _url_malteserHost + url; }

                                                        attachmentList.Add(new EntryAttachment(link_title, url, true));

                                                        break;

                                                    case "d1d91016-032f-456d-98a4-721247c305e8":

                                                        //Bild --> Bild als Anhang aufnehmen
                                                        if (!webPartData.Has("serverProcessedContent") ||
                                                            !webPartData.GetJSONObject("serverProcessedContent").Has("imageSources") ||
                                                            !webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("imageSources").Has("imageSource")) { break; }

                                                        string pic_url = webPartData.GetJSONObject("serverProcessedContent").GetJSONObject("imageSources").GetString("imageSource");
                                                        if (pic_url.StartsWith("/")) { pic_url = _url_malteserHost + pic_url; }

                                                        string pic_filename = Path.GetFileName(pic_url);
                                                        if (pic_filename.ToLower().StartsWith("visualtemplateimage")) { break; }

                                                        attachmentList.Add(new EntryAttachment(pic_filename, pic_url, false));

                                                        break;

                                                    default:

                                                        string content = webPartData.ToString();
                                                        Console.WriteLine(content);

                                                        break;
                                                }
                                            }

                                        }
                                        else
                                        {
                                            if (contentJson.GetJSONObject(j).Has("innerHTML") &&
                                                contentJson.GetJSONObject(j).Has("controlType") && contentJson.GetJSONObject(j).GetInt("controlType") == 4)
                                            {
                                                string bodytext = contentJson.GetJSONObject(j).GetString("innerHTML");
                                                bodytext = Helper.Converter.GetPlainOfHtml(bodytext);
                                                body += bodytext;
                                            }
                                        }
                                    }

                                    FeedEntry entry = new FeedEntry(key, title, date, author, body, attachmentList);
                                    if (!string.IsNullOrEmpty(body) || attachmentList.Count > 0)
                                    {
                                        listFeed.Add(entry);
                                    }

                                }
                            }

                            TBL.UpdateEntries(listFeed, doNotify);

                            return SharepointAPIState.OK; //InvokeStateChanged(SharepointAPIState.OK); return;
                        }

                        return SharepointAPIState.SERVER_ERROR; //InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return;

                }
            }
            catch (Exception)
            {
                return SharepointAPIState.SERVER_ERROR; //InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return;
            }

        }
        
        public async void GetNewsFeedAttachment(EntryAttachment attachment, Action<Adapters.AttachmentRetrieveErrorReason> onError, Action<string> onDownloaded, bool relogin = true)
        {

            if (attachment.IsDownloaded)
            {
                onDownloaded(attachment.LocalFilePath);
                return;
            }
            int waitTimeout = (2 * REQUEST_TIMEOUT) / 200;

            //Dateinamen erstellen
            string filePath = attachment.LocalFilePath;
            var fileHandle = new Java.IO.File(filePath);
            if(!fileHandle.Exists()) {

                string attachFilename = Path.GetFileNameWithoutExtension(attachment.Title).Replace(" ", "_");
                string attachExtension = Path.GetExtension(attachment.RemoteURL);

                fileHandle = Java.IO.File.CreateTempFile(attachFilename, attachExtension, _context.CacheDir);
                filePath = fileHandle.Path;

            }
           
            //Request erstellen
            if (!IsOnline()) { onError(Adapters.AttachmentRetrieveErrorReason.CONNECTION_LOST); return; }
            while (State == SharepointAPIState.WORKING)
            {
                await Task.Delay(200);

                waitTimeout -= 1;
                if (waitTimeout < 0)
                {
                    fileHandle.Delete();

                    onError(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR);
                    return;
                }
            }

            if (State == SharepointAPIState.SERVER_ERROR || State == SharepointAPIState.WRONG_LOGIN)
            {
                fileHandle.Delete();

                onError(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR);
                return;
            }

            //schmutziger Path 3.11, weil die Uri bei Links nicht korrekt erstellt wurde.
            var patch = attachment.RemoteURL.Replace("https:/m", "https://m");

            var client = new DownloadClient(new Uri(patch), filePath, CreateOAuthCookie(), _bearer);
            client.DownloadProgressChanged += (ss, ee) => { };
            client.DownloadFinished += async (ss, ee) =>
            {
                if (ee.DownloadFinished)
                {
                    if(State == SharepointAPIState.CONNECTION_LOST) { InvokeStateChanged(SharepointAPIState.LOGGED_IN); }

                    attachment.UpdateAttachmentDownloaded(filePath);

                    onDownloaded(filePath);
                    return;
                }

                if (relogin)
                {

                    onError(Adapters.AttachmentRetrieveErrorReason.RELOGIN_REQUIRED);
                    var result = await CreateLogin();

                    InvokeStateChanged(result);

                    if (result == SharepointAPIState.LOGGED_IN)
                    {
                        GetNewsFeedAttachment(attachment, onError, onDownloaded, false);
                        return;
                    }
                    else
                    {
                        if (result == SharepointAPIState.CONNECTION_LOST) { onError(Adapters.AttachmentRetrieveErrorReason.CONNECTION_LOST); }
                        else { onError(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR); }
                    }

                }
                else
                {
                    onError(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR);
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

        //########################################################

        private CookieContainer CreateOAuthCookie()
        {
            var c = new CookieContainer();
            c.Add(new Uri(_url_malteserHost), new Cookie("SPOIDCRL", _spOauth));
            return c;
        }

        //########################################################

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

        private class ResponseObject
        {
            public enum ResponseObjectStatusCode
            {
                OK,
                CONNECTION_LOST,
                FORBIDDEN,
                ERROR,
                UNSET = -1
            }

            //########################################################

            public ResponseObjectStatusCode StatusCode { get; private set; } = ResponseObjectStatusCode.UNSET;
            public HttpWebResponse Response { get; private set; } = null;

            //########################################################

            public ResponseObject(ResponseObjectStatusCode errorcode)
            {
                StatusCode = errorcode;
                Response = null;
            }
            public ResponseObject(HttpWebResponse response)
            {
                StatusCode = ResponseObjectStatusCode.OK;
                Response = response;
            }

            public void Close()
            {
                StatusCode = ResponseObjectStatusCode.UNSET;
                Response.Close();
            }
        }

        private async Task<ResponseObject> GetResponse(HttpWebRequest request)
        {
            try
            {
                HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
                if (response.StatusCode == HttpStatusCode.Found)
                {
                    response.Close();
                    request = GetRequest(response.ResponseUri, response.Headers.Get("Location"));
                    request.Referer = response.ResponseUri.ToString();
                    return await GetResponse(request);
                }
                return new ResponseObject(response);
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.NameResolutionFailure || e.Status == WebExceptionStatus.ProxyNameResolutionFailure || e.Status == WebExceptionStatus.Timeout || e.Status == WebExceptionStatus.SendFailure || e.Status == WebExceptionStatus.ReceiveFailure)
                {
                    return new ResponseObject(ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST);
                }

                return new ResponseObject(ResponseObject.ResponseObjectStatusCode.FORBIDDEN);
            }
        }
        private string GetResponseData(HttpWebResponse response)
        {
            StreamReader stream = new StreamReader(response.GetResponseStream());
            string result = stream.ReadToEnd();
            stream.Close();

            return result;
        }

        //########################################################

        private JSONArray GetJsonArray(string json)
        {
            try
            {
                return new JSONArray(json);
            }
            catch (JSONException)
            {
                return null;
            }
        }

        //########################################################

        private class DownloadClientFinishedEventArgs
        {
            public DownloadClientFinishedEventArgs(bool finished)
            {
                DownloadFinished = finished;
            }
            public bool DownloadFinished { get; }
        }
        private class DownloadClient
        {
            public event EventHandler<int> DownloadProgressChanged;
            public event EventHandler<DownloadClientFinishedEventArgs> DownloadFinished;

            //##################################################################################

            private readonly string _filePath;
            private readonly Uri _remotePath;
            private readonly CookieContainer _cookieJar;
            private readonly string _digest;

            //##################################################################################

            public DownloadClient(Uri remotePath, string localPath, CookieContainer jar, string bearer)
            {
                _filePath = localPath;
                _remotePath = remotePath;
                _cookieJar = jar;
                _digest = bearer;
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
                        if (WriteFile(response))
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

            private bool WriteFile(HttpWebResponse response)
            {

                long received = 0;

                try
                {
                    byte[] buffer = new byte[1024];
                    using (Stream input = response.GetResponseStream())
                    {
                        long total = response.ContentLength;
                        var test = new Java.IO.File(_filePath);

                        //TODO: Check, ob File bereits existiert & gleiche Größe, wie der Stream hat >> SKIP

                        FileStream fileStream = File.OpenWrite(_filePath);
                        int size = input.Read(buffer, 0, buffer.Length);
                        while (size > 0)
                        {
                            fileStream.Write(buffer, 0, size);
                            received += size;

                            int perc = (int)(received / total) * 100; if (perc < 0) { perc = 0; }
                            if (perc > 100) { perc = 100; }
                            DownloadProgressChanged?.Invoke(this, perc);

                            size = input.Read(buffer, 0, buffer.Length);
                        }

                        fileStream.Flush();
                        fileStream.Close();
                    }

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
        OFFLINE,
        WORKING,
        LOGGED_IN,
        OK,
        WRONG_LOGIN,
        CONNECTION_LOST,
        SERVER_ERROR
    }
    public class SharepointAPIStateChangedEventArgs : EventArgs
    {
        public SharepointAPIStateChangedEventArgs(SharepointAPIState state)
        {
            State = state;
        }
        public SharepointAPIState State { get; }
    }

}