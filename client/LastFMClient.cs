using com.okitoki.wobblefm.messages;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.IO;
using System.Threading;

namespace com.okitoki.wobblefm.client
{
    public class LastFMClient
    {
        private Guid id = Guid.NewGuid();
        private string apiKey;
        private string secret;
        private string userAgentName;
        private HttpClient client;

        private LastFMSession session;
        private bool createSessionAutomatically = false;

        public LastFMClient(string apiKey, string secret, string userAgentName = null)
        {
            this.apiKey = apiKey;
            this.secret = secret;

            if(userAgentName == null || userAgentName.Length <= 4)
            {
                userAgentName = "WobbleFM_Client_";
            }

            this.userAgentName = userAgentName + id;

            client = new HttpClient();
            client.BaseAddress = new Uri("http://ws.audioscrobbler.com/2.0/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", this.userAgentName);

            try
            {
                this.LoadSession();
            }
            catch(Exception e) { }
        }

        public bool CreateSessionAutomatically
        {
            get { return this.createSessionAutomatically; }
            set { this.createSessionAutomatically = value; }
        }

        public string GetToken()
        {
            FormUrlEncodedContent urlParams = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "method", "auth.gettoken" },
                { "api_key", apiKey },
                { "format", "json" }
            });

            Task<string> urlStringTask = urlParams.ReadAsStringAsync();
            urlStringTask.Wait();
            string urlString = urlStringTask.Result;

            Console.WriteLine("URL: " + urlString);

            Task<HttpResponseMessage> responseTask = client.GetAsync("?" + urlString);
            responseTask.Wait();

            HttpResponseMessage response = responseTask.Result;
            Task<string> responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();

            LastFMTokenMessage tokenMessage = JsonSerializer.Deserialize<LastFMTokenMessage>(responseBodyTask.Result);
            return tokenMessage.Token;
        }

        public string GetAuthorizationURL(string token)
        {
            FormUrlEncodedContent urlParams = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "api_key", apiKey },
                { "token", token }
            });

            Task<string> urlStringTask = urlParams.ReadAsStringAsync();
            urlStringTask.Wait();
            string urlString = urlStringTask.Result;

            return "http://www.last.fm/api/auth/?" + urlString;
        }

        public LastFMSession CreateSession(string token, bool saveSession = true)
        {
            try
            {
                string apiSig = "api_key" + apiKey + "methodauth.getSessiontoken" + token + secret;
                MD5 md5 = MD5.Create();
                byte[] apiSigHashBytes = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(apiSig));
                string apiSigHash = BitConverter.ToString(apiSigHashBytes).Replace("-", "");

                Console.WriteLine("API HASH: " + apiSigHash);

                FormUrlEncodedContent urlParams = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "method", "auth.getSession" },
                    { "token", token },
                    { "api_key", apiKey },
                    { "api_sig", apiSigHash },
                    { "format", "json" }
                });

                Task<string> urlStringTask = urlParams.ReadAsStringAsync();
                urlStringTask.Wait();
                string urlString = urlStringTask.Result;

                Console.WriteLine("URL: " + client.BaseAddress + "?" + urlString);

                Task<HttpResponseMessage> responseTask = client.GetAsync("?" + urlString);
                responseTask.Wait();

                HttpResponseMessage response = responseTask.Result;
                Task<string> responseBodyTask = response.Content.ReadAsStringAsync();
                responseBodyTask.Wait();

                LastFMSessionMessage sessionMessage = JsonSerializer.Deserialize<LastFMSessionMessage>(responseBodyTask.Result);
                this.session = sessionMessage.Session;
            }
            catch(Exception e)
            {
                Console.WriteLine("An error occurred when attempting to create a new LastFM session: " + e);
                return null;
            }

            //Save the session locally if saveSession = true
            if(saveSession)
            {
                SaveSession(session);
            }

            return this.session;
        }

        public void ClearSession(string path = "lfmSession.json")
        {
            if(File.Exists(path))
            {
                File.Delete(path);
            }

            session = null;
        }

        public void LoadSession(string path = "lfmSession.json")
        {
            if(File.Exists(path))
            {
                string sessionJson = File.ReadAllText(path);
                LastFMSession session = JsonSerializer.Deserialize<LastFMSession>(sessionJson);

                if(session != null)
                {
                    this.session = session;
                }
                else
                {
                    throw new Exception("An error occurred when attempting to load LastFM session from path '" + path + "'");
                }
            }
            else
            {
                throw new Exception("Unable to load LastFM session. No session file found at path '" + path + "'");
            }
        }

        public void SaveSession(LastFMSession session, string path = "lfmSession.json")
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                StreamWriter writer = File.CreateText(path);
                writer.Write(JsonSerializer.Serialize(session));
                writer.Flush();
                writer.Close();
            }
            catch(Exception e)
            {
                Console.Out.WriteLine("Unable to save LastFM session information to disk. User re-authorization will be required the next time this program runs: " + e);
            }
        }

        public void SetSession(LastFMSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Sets up the LastFM client to automatically attempt to authorize the user and create a session for communicating with LastFM.
        /// </summary>
        /// <param name="timeAllowanceInSeconds">The amount of time in seconds which the user will have to authorize the application to use their 
        /// account. (A web browser page will be launched prompting for the user to click an authorization button. Default is 30 seconds. If the user
        /// fails to authorize the application, authorization will fail and the API will automatically restart this process until it succeeds, the application closes,
        /// or 'CreateSessionAutomatically' is set to false on the LastFMClient.</param>
        public void AttemptAuthorization(int timeAllowanceInSeconds = 30)
        {
            createSessionAutomatically = true;
            StartAuthorizationThread(timeAllowanceInSeconds);
        }

        private void StartAuthorizationThread(int timeAllowanceInSeconds = 30)
        {
            Thread sessionCreationThread = new Thread(() =>
            {
                while (!HasSession() && createSessionAutomatically)
                {
                    //Get an authorization token from LastFM.
                    string token = GetToken();

                    //Launch webpage to get authorization from the user.
                    string authUrl = GetAuthorizationURL(token);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authUrl) { UseShellExecute = true });

                    //Wait for the time specified before attempting to create a session.
                    Thread.Sleep(timeAllowanceInSeconds * 1000);

                    //Attempt to create a session. If the user fails to authorize the application, this will fail.
                    CreateSession(token);
                }
            });

            sessionCreationThread.Name = "LastFM Auto-Session Creation Thread";
            sessionCreationThread.Start();
        }

        public bool HasSession()
        {
            return this.session != null;
        }

        /// <summary>
        /// Scrobble the specified Track to LastFM.
        /// </summary>
        /// <param name="artist">The artist of the track.</param>
        /// <param name="track">The name of the track.</param>
        /// <param name="startTimestampInSeconds">The Unix timestamp (in seconds since UTC) at which the song started playing.</param>
        public void Scrobble(string artist, string track, long startTimestampInSeconds)
        {
            string apiSig = "api_key" + apiKey + "artist[0]" + artist + "methodtrack.scrobblesk" + session.Key + "timestamp[0]" + startTimestampInSeconds + "track[0]" + track + secret;
            MD5 md5 = MD5.Create();
            byte[] apiSigHashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(apiSig));
            string apiSigHash = BitConverter.ToString(apiSigHashBytes).Replace("-", "");

            Console.WriteLine("API HASH: " + apiSigHash);

            Dictionary<string, string> methodParams = new Dictionary<string, string>()
                {
                    { "method", "track.scrobble" },
                    { "artist[0]", artist },
                    { "track[0]", track },
                    { "timestamp[0]", "" + startTimestampInSeconds },
                    { "api_key", apiKey },
                    { "api_sig", apiSigHash },
                    { "sk", session.Key },
                    { "format", "json" }
                };

            FormUrlEncodedContent content = new FormUrlEncodedContent(methodParams);

            Task<HttpResponseMessage> responseMessageTask = client.PostAsync(client.BaseAddress, content);
            responseMessageTask.Wait();

            HttpResponseMessage responseMessage = responseMessageTask.Result;
            Task<string> contentTask = responseMessage.Content.ReadAsStringAsync();
            contentTask.Wait();

            string contentValue = contentTask.Result;

            Console.WriteLine("Scrobble completed.");
        }
    }
}
