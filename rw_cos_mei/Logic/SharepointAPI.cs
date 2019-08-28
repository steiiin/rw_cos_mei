﻿using Android.Content;
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

        private readonly string _url_malteserHost = "https://maltesercloud.sharepoint.com";
        private readonly string _url_getAdfs = "https://login.microsoftonline.com/GetUserRealm.srf";
        private readonly string _url_getSpToken = "https://login.microsoftonline.com/rst2.srf";

        private readonly string _url_endpoint = "https://maltesercloud.sharepoint.com/sites/mhd/DD/RD/RW_Mei/";

        //#########################################################

        private const int REQUEST_TIMEOUT = 12000;

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

        public async Task<bool> CreateLogin()
        {

            InvokeStateChanged(SharepointAPIState.WORKING);

            if (!IsOnline()) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return false; }
            if (string.IsNullOrWhiteSpace(_username)) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }
            if (string.IsNullOrWhiteSpace(_password)) { InvokeStateChanged(SharepointAPIState.WRONG_LOGIN); return false; }

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
                if (request == null) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return false; }

                ResponseObject response = await GetResponse(request);
                switch (response.StatusCode)
                {
                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                        InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                        return false;

                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                        InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                        return false;

                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                    default:

                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                        return false;

                    case ResponseObject.ResponseObjectStatusCode.OK:

                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                        {

                            string responseData = GetResponseData(response.Response); response.Close();

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
                            if (request == null) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return false; }

                            response = await GetResponse(request);
                            switch (response.StatusCode)
                            {
                                case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                    InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                                    return false;

                                case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:
                                
                                    InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                                    return false;

                                case ResponseObject.ResponseObjectStatusCode.ERROR:
                                case ResponseObject.ResponseObjectStatusCode.UNSET:
                                default:

                                    InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                                    return false;

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
                                        if (request == null) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return false; }

                                        response = await GetResponse(request);
                                        switch (response.StatusCode)
                                        {
                                            case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                                InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                                                return false;

                                            case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                                                return false;

                                            case ResponseObject.ResponseObjectStatusCode.ERROR:
                                            case ResponseObject.ResponseObjectStatusCode.UNSET:
                                            default:

                                                InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                                                return false;

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

                                                            InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                                                            return false;

                                                        case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                            InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                                                            return false;

                                                        case ResponseObject.ResponseObjectStatusCode.ERROR:
                                                        case ResponseObject.ResponseObjectStatusCode.UNSET:
                                                        default:

                                                            InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                                                            return false;

                                                        case ResponseObject.ResponseObjectStatusCode.OK:

                                                            if (response.Response?.StatusCode == HttpStatusCode.OK)
                                                            {

                                                                //#############################################################################################################################
                                                                //Digest beantragen

                                                                Uri digestUri = new Uri(_url_malteserHost + "/_vti_bin/sites.asmx");
                                                                string digestRequest = @"<?xml version='1.0' encoding='utf-8'?><soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'><soap:Body><GetUpdatedFormDigestInformation xmlns='http://schemas.microsoft.com/sharepoint/soap/' /></soap:Body></soap:Envelope>";

                                                                request = await GetRequest_POSTAsync(GetRequest(digestUri), RequestContentType.XML, RequestContentType.ALL, digestRequest);
                                                                if (request == null) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return false; }

                                                                request.Headers.Add("X-RequestForceAuthentication", "true");
                                                                request.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
                                                                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                                                                request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sharepoint/soap/GetUpdatedFormDigestInformation");

                                                                response = await GetResponse(request);
                                                                switch (response.StatusCode)
                                                                {
                                                                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                                                                        InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                                                                        return false;

                                                                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                                                                        InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                                                                        return false;

                                                                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                                                                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                                                                    default:

                                                                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                                                                        return false;

                                                                    case ResponseObject.ResponseObjectStatusCode.OK:

                                                                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                                                                        {

                                                                            responseData = GetResponseData(response.Response); response.Close();
                                                                            x = XElement.Parse(responseData);

                                                                            _bearer = x.Descendants().Where(xg => xg.Name.LocalName == "DigestValue").First().Value;
                                                                            _spOauth = _cookieJar.GetCookies(new Uri(_url_malteserHost))["SPOIDCRL"].Value;

                                                                            TBL.UpdateTokens(_bearer, _spOauth);

                                                                            InvokeStateChanged(SharepointAPIState.LOGGED_IN);
                                                                            return true;

                                                                        }
                                                                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;

                                                                }
                                                                
                                                            }
                                                            InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;

                                                    }
                                                    
                                                }
                                                InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;

                                        }

                                    }
                                    InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;

                            }
                            
                        }
                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;

                }
                
            }
            catch (Exception)
            {
                InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return false;
            }

        }

        //########################################################

        public async Task UpdateNewsFeed()
        {
            await UpdateNewsFeed(false, true);
        }
        public async Task UpdateNewsFeed(bool doNotify, bool relogin)
        {
           
            InvokeStateChanged(SharepointAPIState.WORKING);
            
            if (!IsOnline()) { InvokeStateChanged(SharepointAPIState.CONNECTION_LOST); return; }
            if (!string.IsNullOrWhiteSpace(_spOauth)) { _cookieJar = CreateOAuthCookie(); } else { _cookieJar = new CookieContainer(); }

            string query = "_api/web/lists/getbytitle('Neues%20aus%20der%20Rettungswache')/items?$select=ID,Title,Body,Modified,AttachmentFiles,Author/Name,Author/Title&$orderby=Modified%20desc&$expand=AttachmentFiles,Author/Id";
            try
            {

                HttpWebRequest request = GetRequest(new Uri(_url_endpoint + query));
                request.Headers.Add("X-RequestDigest", _bearer);
                request.Accept = "application/json; odata=verbose";

                ResponseObject response = await GetResponse(request);
                switch (response.StatusCode)
                {
                    case ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST:

                        InvokeStateChanged(SharepointAPIState.CONNECTION_LOST);
                        return;

                    case ResponseObject.ResponseObjectStatusCode.FORBIDDEN:

                        if(relogin)
                        {
                            if (await CreateLogin())
                            { 
                                await UpdateNewsFeed(doNotify, false);
                            }
                            return;
                        }
                        InvokeStateChanged(SharepointAPIState.WRONG_LOGIN);
                        return;

                    case ResponseObject.ResponseObjectStatusCode.ERROR:
                    case ResponseObject.ResponseObjectStatusCode.UNSET:
                    default:

                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                        return;

                    case ResponseObject.ResponseObjectStatusCode.OK:

                        if (response.Response?.StatusCode == HttpStatusCode.OK)
                        {

                            string responseData = GetResponseData(response.Response); response.Close();

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
                                if (string.IsNullOrWhiteSpace(entry.Body) && entry.Attachments.Count == 0) { }
                                else { listFeed.Add(entry); }

                            }

                            TBL.UpdateEntries(listFeed, doNotify);

                            InvokeStateChanged(SharepointAPIState.OK);
                            return;

                        }
                        InvokeStateChanged(SharepointAPIState.SERVER_ERROR); return;

                }
                
                

            }
            catch (Exception)
            {
                InvokeStateChanged(SharepointAPIState.SERVER_ERROR);
                return;
            }
                        
        }

        public async void GetNewsFeedAttachment(EntryAttachment attachment, Action<Adapters.AttachmentRetrieveErrorReason> onError, Action<string> onDownloaded, bool relogin = true)
        {
            
            string tmpRaw = Path.GetTempFileName();
            string nameEsc = Path.GetFileNameWithoutExtension(attachment.FileName).Replace(" ", "_");

            string extension = Path.GetExtension(attachment.FileRemoteUrl);
            string filePath = Path.GetDirectoryName(tmpRaw) + Path.DirectorySeparatorChar + nameEsc + Path.GetFileNameWithoutExtension(tmpRaw) + extension;
            Uri fileAdress = new Uri(_url_malteserHost + attachment.FileRemoteUrl);
            
            //Request erstellen
            if (!IsOnline()) { onError(Adapters.AttachmentRetrieveErrorReason.CONNECTION_LOST); return; }
            while (State == SharepointAPIState.WORKING)
            {
                await Task.Delay(200);
            }

            if (State == SharepointAPIState.SERVER_ERROR || State == SharepointAPIState.WRONG_LOGIN)
            {
                onError(Adapters.AttachmentRetrieveErrorReason.RETRIEVE_ERROR);
                return;
            }
            
            var client = new DownloadClient(fileAdress, filePath, CreateOAuthCookie(), _bearer);
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
                    onError(Adapters.AttachmentRetrieveErrorReason.RELOGIN_REQUIRED);
                    await CreateLogin();
                    GetNewsFeedAttachment(attachment, onError, onDownloaded, false);
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
                if(((HttpWebResponse)e.Response)?.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new ResponseObject(ResponseObject.ResponseObjectStatusCode.FORBIDDEN);
                }
                if(e.Response?.ContentLength>0)
                {
                    if(GetResponseData((HttpWebResponse)e.Response).Contains("FailedAuthentication")) { return new ResponseObject(ResponseObject.ResponseObjectStatusCode.FORBIDDEN); }
                }
                
                return new ResponseObject(ResponseObject.ResponseObjectStatusCode.ERROR);
            }
            catch (Exception)
            {
                return new ResponseObject(ResponseObject.ResponseObjectStatusCode.CONNECTION_LOST);
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
        public SharepointAPIStateChangedEventArgs(SharepointAPIState state) { State = state; }
        public SharepointAPIState State { get; }
    }
    
}