// Copyright 2009-2013 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Net.Sockets;
using System.Linq;
using GemsCraft.Events;
using GemsCraft.fSystem;
using GemsCraft.Configuration;
using GemsCraft.Network;
using GemsCraft.Utils;
using JetBrains.Annotations;

namespace GemsCraft.Network
{
    /// <summary> Static class responsible for sending heartbeats. </summary>
    public static class Heartbeat
    {
        static readonly Uri ClassiCubeNetUri;

        /// <summary> Delay between sending heartbeats. Default: 25s </summary>
        public static TimeSpan Delay { get; set; }

        /// <summary> Request timeout for heartbeats. Default: 10s </summary>
        public static TimeSpan Timeout { get; set; }

        /// <summary> Secret string used to verify players' names.
        /// Randomly generated at startup.
        /// Known only to this server, heartbeat servers, and webpanel. </summary>
        public static string Salt { get; internal set; }

        /// <summary> Second salt.
        /// Used if server is running a dual heartbeat</summary>
        public static string Salt2 { get; internal set; }

        // Dns lookup, to make sure that IPv4 is preferred for heartbeats
        static readonly Dictionary<string, IPAddress> TargetAddresses = new Dictionary<string, IPAddress>();
        static DateTime nextDnsLookup = DateTime.MinValue;
        static readonly TimeSpan DnsRefreshInterval = TimeSpan.FromMinutes(30);


        static IPAddress RefreshTargetAddress([NotNull] Uri requestUri)
        {
            if (requestUri == null) throw new ArgumentNullException("requestUri");

            string hostName = requestUri.Host.ToLowerInvariant();
            IPAddress targetAddress;
            if (!TargetAddresses.TryGetValue(hostName, out targetAddress) || DateTime.UtcNow >= nextDnsLookup)
            {
                try
                {
                    // Perform a DNS lookup on given host. Throws SocketException if no host found.
                    IPAddress[] allAddresses = Dns.GetHostAddresses(requestUri.Host);
                    // Find a suitable IPv4 address. Throws InvalidOperationException if none found.
                    targetAddress = allAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                }
                catch (SocketException ex)
                {
                    Logger.Log(LogType.Error,
                               "Heartbeat.RefreshTargetAddress: Error looking up heartbeat server URLs: {0}",
                               ex);
                }
                catch (InvalidOperationException)
                {
                    Logger.Log(LogType.Warning,
                               "Heartbeat.RefreshTargetAddress: {0} does not have an IPv4 address!", requestUri.Host);
                }
                TargetAddresses[hostName] = targetAddress;
                nextDnsLookup = DateTime.UtcNow + DnsRefreshInterval;
            }
            return targetAddress;
        }


        static Heartbeat()
        {
            ClassiCubeNetUri = new Uri("http://www.classicube.net/heartbeat.jsp");
            Delay = TimeSpan.FromSeconds(45);
            Timeout = TimeSpan.FromSeconds(10);
            Salt = Server.GetRandomString(32);
            Salt2 = Server.GetRandomString(32);
            Server.ShutdownBegan += OnServerShutdown;
        }

        static void OnServerShutdown(object sender, ShutdownEventArgs e)
        {
            if (minecraftNetRequest != null)
            {
                minecraftNetRequest.Abort();
            }
        }


        internal static void Start()
        {
            Scheduler.NewBackgroundTask(Beat).RunForever(Delay);
        }


        static void Beat(SchedulerTask scheduledTask)
        {
            if (Server.IsShuttingDown) return;

            if (ConfigKey.HeartbeatEnabled.Enabled())
            {
                SendClassiCubeBeat();
                HbSave();
            }
            else
            {
                // If heartbeats are disabled, the server data is written
                // to a text file instead (heartbeatdata.txt)
                string[] data = new[]{
                    Salt,
                    Server.InternalIP.ToString(),
                    Server.Port.ToString(),
                    Server.CountPlayers( false ).ToString(),
                    ConfigKey.MaxPlayers.GetString(),
                    ConfigKey.ServerName.GetString(),
                    ConfigKey.IsPublic.GetString(),
                };
                const string tempFile = Paths.HeartbeatDataFileName + ".tmp";
                File.WriteAllLines(tempFile, data, Encoding.ASCII);
                Paths.MoveOrReplace(tempFile, Paths.HeartbeatDataFileName);
            }
        }

        static HttpWebRequest minecraftNetRequest;

        static void SendClassiCubeBeat()
        {
            HeartbeatData data = new HeartbeatData(ClassiCubeNetUri);
            if (!RaiseHeartbeatSendingEvent(data, ClassiCubeNetUri, true))
            {
                return;
            }
            minecraftNetRequest = CreateRequest(data.CreateUri(Salt2));
            var state = new HeartbeatRequestState(minecraftNetRequest, data, true);
            minecraftNetRequest.BeginGetResponse(ResponseCallback, state);
        }

        // Creates an asynchrnous HTTP request to the given URL
        static HttpWebRequest CreateRequest(Uri uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.ServicePoint.BindIPEndPointDelegate = new BindIPEndPoint(Server.BindIPEndPointCallback);
            request.Method = "GET";
            request.Timeout = (int)Timeout.TotalMilliseconds;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache);
            if (uri.Scheme == "http")
            {
                request.Proxy = new WebProxy("http://" + RefreshTargetAddress(uri) + ":" + uri.Port);
            }
            return request;
        }
        public static string HbData;
        public static void HbSave()
        {
            try
            {
                const string SaverFile = "heartbeatdata.txt";

                if (File.Exists(SaverFile))
                {
                    File.Delete(SaverFile);
                }
                if (Salt == null) return;

                if (Server.CountPlayers(false).ToString() == null) return;
                string[] data = new[] {
                    Salt,
                    Server.InternalIP.ToString(),
                    Server.Port.ToString(),
                    Server.CountPlayers( false ).ToString(),
                    ConfigKey.MaxPlayers.GetString(),
                    ConfigKey.ServerName.GetString(),
                    ConfigKey.IsPublic.GetString(),
                    Salt2
                    };

                //"port=" + Server.Port.ToString() + "&max=" + ConfigKey.MaxPlayers.GetString() + "&name=" +
                //Uri.EscapeDataString(ConfigKey.ServerName.GetString()) +
                //"&public=True" + "&salt=" + Salt + "&users=" + Server.CountPlayers(false).ToString();
                const string tempFile = Paths.HeartbeatDataFileName + ".tmp";
                File.WriteAllLines(tempFile, data, Encoding.ASCII);
                Paths.MoveOrReplace(tempFile, Paths.HeartbeatDataFileName);
            }
            catch (Exception ex) { Logger.Log(LogType.Error, "" + ex); }
        }


        // Called when the heartbeat server responds.
        static void ResponseCallback(IAsyncResult result)
        {
            if (Server.IsShuttingDown) return;
            HeartbeatRequestState state = (HeartbeatRequestState)result.AsyncState;
            try
            {
                string responseText;
                using (HttpWebResponse response = (HttpWebResponse)state.Request.EndGetResponse(result))
                {
                    // ReSharper disable AssignNullToNotNullAttribute
                    using (StreamReader responseReader = new StreamReader(response.GetResponseStream()))
                    {
                        // ReSharper restore AssignNullToNotNullAttribute
                        responseText = responseReader.ReadToEnd();
                    }
                    RaiseHeartbeatSentEvent(state.Data, response, responseText);
                }

                // try parse response as server Uri, if needed
                if (state.GetServerUri)
                {
                    string replyString = responseText.Trim();
                    if (replyString.StartsWith("bad heartbeat", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log(LogType.Error, "Heartbeat: {0}", replyString);
                    }
                    else
                    {
                        try
                        {
                            Uri newUri = new Uri(replyString);
                            Uri oldUri = Server.Uri;
                            if (newUri != oldUri)
                            {
                                Server.Uri = newUri;
                                RaiseUriChangedEvent(oldUri, newUri);
                            }
                        }
                        catch (UriFormatException)
                        {
                            Logger.Log(LogType.Error,
                                        "Heartbeat: Server replied with: {0}",
                                        replyString);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException || ex is IOException)
                {
                    Logger.Log(LogType.Warning,
                                "Heartbeat: {0} is probably down ({1})",
                                state.Request.RequestUri.Host,
                                ex.Message);
                }
                else
                {
                    Logger.Log(LogType.Error, "Heartbeat: {0}", ex);
                }
            }
        }


        #region Events

        /// <summary> Occurs when a heartbeat is about to be sent (cancellable). </summary>
        public static event EventHandler<HeartbeatSendingEventArgs> Sending;

        /// <summary> Occurs when a heartbeat has been sent. </summary>
        public static event EventHandler<HeartbeatSentEventArgs> Sent;

        /// <summary> Occurs when the server Uri has been set or changed. </summary>
        public static event EventHandler<UriChangedEventArgs> UriChanged;


        static bool RaiseHeartbeatSendingEvent(HeartbeatData data, Uri uri, bool getServerUri)
        {
            var h = Sending;
            if (h == null) return true;
            var e = new HeartbeatSendingEventArgs(data, uri, getServerUri);
            h(null, e);
            return !e.Cancel;
        }

        static void RaiseHeartbeatSentEvent(HeartbeatData heartbeatData,
                                             HttpWebResponse response,
                                             string text)
        {
            var h = Sent;
            if (h != null)
            {
                h(null, new HeartbeatSentEventArgs(heartbeatData,
                                                     response.Headers,
                                                     response.StatusCode,
                                                     text));
            }
        }

        static void RaiseUriChangedEvent(Uri oldUri, Uri newUri)
        {
            var h = UriChanged;
            if (h != null) h(null, new UriChangedEventArgs(oldUri, newUri));
        }

        #endregion


        sealed class HeartbeatRequestState
        {
            public HeartbeatRequestState(HttpWebRequest request, HeartbeatData data, bool getServerUri)
            {
                Request = request;
                Data = data;
                GetServerUri = getServerUri;
            }
            public readonly HttpWebRequest Request;
            public readonly HeartbeatData Data;
            public readonly bool GetServerUri;
        }
    }


    public sealed class HeartbeatData
    {
        internal HeartbeatData([NotNull] Uri heartbeatUri)
        {
            if (heartbeatUri == null) throw new ArgumentNullException("heartbeatUri");
            IsPublic = ConfigKey.IsPublic.Enabled();
            MaxPlayers = ConfigKey.MaxPlayers.GetInt();
            PlayerCount = Server.CountPlayers(false);
            ServerIP = Server.InternalIP;
            Port = Server.Port;
            ProtocolVersion = Config.ProtocolVersion;
            Salt = Heartbeat.Salt;
            Salt2 = Heartbeat.Salt2;
            ServerName = ConfigKey.ServerName.GetString();
            CustomData = new Dictionary<string, string>();
            HeartbeatUri = heartbeatUri;
        }

        [NotNull]
        public Uri HeartbeatUri { get; private set; }
        public string Salt { get; set; }
        public string Salt2 { get; set; }
        public IPAddress ServerIP { get; set; }
        public int Port { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string ServerName { get; set; }
        public bool IsPublic { get; set; }
        public int ProtocolVersion { get; set; }
        public Dictionary<string, string> CustomData { get; private set; }

        public Uri CreateUri(string salt_)
        {
            UriBuilder ub = new UriBuilder(HeartbeatUri);
            StringBuilder sb = new StringBuilder();

            //if we are sending to CC
            if (salt_ == Salt2)
            {
                sb.AppendFormat("public={0}&max={1}&users={2}&port={3}&software={7}&version={4}&salt={5}&name={6}",
                 IsPublic,
                 MaxPlayers,
                 PlayerCount,
                 Port,
                 ProtocolVersion,
                 Uri.EscapeDataString(salt_),
                 Uri.EscapeDataString(ServerName),
                 "GemsCraft v" + Updater.LatestStable.ToString());
                foreach (var pair in CustomData)
                {
                    sb.AppendFormat("&{0}={1}",
                                     Uri.EscapeDataString(pair.Key),
                                     Uri.EscapeDataString(pair.Value));
                }
                ub.Query = sb.ToString();
            }
            else
            {
                sb.AppendFormat("public={0}&max={1}&users={2}&port={3}&version={4}&salt={5}&name={6}",
                                 IsPublic,
                                 MaxPlayers,
                                 PlayerCount,
                                 Port,
                                 ProtocolVersion,
                                 Uri.EscapeDataString(salt_),
                                 Uri.EscapeDataString(ServerName));
                foreach (var pair in CustomData)
                {
                    sb.AppendFormat("&{0}={1}",
                                     Uri.EscapeDataString(pair.Key),
                                     Uri.EscapeDataString(pair.Value));
                }
                ub.Query = sb.ToString();
            }
            return ub.Uri;
        }
    }
}


namespace GemsCraft.Events
{
    public sealed class HeartbeatSentEventArgs : EventArgs
    {
        internal HeartbeatSentEventArgs(HeartbeatData heartbeatData,
                                         WebHeaderCollection headers,
                                         HttpStatusCode status,
                                         string text)
        {
            HeartbeatData = heartbeatData;
            ResponseHeaders = headers;
            ResponseStatusCode = status;
            ResponseText = text;
        }

        public HeartbeatData HeartbeatData { get; private set; }
        public WebHeaderCollection ResponseHeaders { get; private set; }
        public HttpStatusCode ResponseStatusCode { get; private set; }
        public string ResponseText { get; private set; }
    }


    public sealed class HeartbeatSendingEventArgs : EventArgs, ICancellableEvent
    {
        internal HeartbeatSendingEventArgs(HeartbeatData data, Uri uri, bool getServerUri)
        {
            HeartbeatData = data;
            Uri = uri;
            GetServerUri = getServerUri;
        }

        public HeartbeatData HeartbeatData { get; private set; }
        public Uri Uri { get; set; }
        public bool GetServerUri { get; set; }
        public bool Cancel { get; set; }
    }


    public sealed class UriChangedEventArgs : EventArgs
    {
        internal UriChangedEventArgs(Uri oldUri, Uri newUri)
        {
            OldUri = oldUri;
            NewUri = newUri;
        }

        public Uri OldUri { get; private set; }
        public Uri NewUri { get; private set; }
    }
}