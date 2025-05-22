using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHttp;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedEvents;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.EventArgs.User;
using TwitchLib.EventSub.Websockets.Extensions;
using WinFormsApp1.Designs;

// Add Chat Command Support
// Add support for Connecting to Non-Broadcaster Channels
// Add support for Same-Date Twitch Logs
// Add more Twitch Logging (Chat Commands, Title Changes, Category Changes)
// Add Channel Bot Support?

namespace WinFormsApp1.Classes
{
    public class Twitch
    {
        IHost _host = new HostBuilder().ConfigureServices((hostContext, services) =>
        {
            services.AddTwitchLibEventSubWebsockets();
            services.AddHostedService<EventSubService>();
        }).Build();

        bool logMessages = true;

        string DirectURL = "http://localhost:3000";

        string scopes = "channel:manage:polls channel:read:polls channel:read:hype_train channel:read:goals channel:read:charity channel:read:ads user:read:chat user:bot moderator:manage:shoutouts moderator:read:shoutouts moderator:read:followers channel:manage:raids user:read:chat user:bot channel:bot user:read:chat user:write:chat chat:read chat:edit channel:manage:redemptions channel:read:redemptions channel:manage:raids channel:read:subscriptions channel:read:vips channel:manage:vips moderation:read moderator:read:banned_users moderator:manage:banned_users moderator:read:blocked_terms moderator:read:chat_messages moderator:manage:chat_messages moderator:read:chat_settings moderator:read:chatters moderator:read:followers moderator:read:moderators moderator:read:shoutouts moderator:manage:shoutouts moderator:read:vips user:bot user:read:broadcast user:read:follows user:read:subscriptions user:read:subscriptions user:manage:chat_color moderator:manage:shoutouts moderator:read:shoutouts moderator:read:moderators moderator:read:chatters moderator:read:followers moderator:manage:chat_messages moderator:read:chat_messages moderator:manage:banned_users moderation:read channel:read:subscriptions channel:read:redemptions bits:read clips:edit moderator:manage:banned_users moderator:read:banned_users user:bot moderator:read:chatters moderator:read:followers moderator:read:moderators moderation:read channel:moderate channel:bot chat:read chat:edit bits:read channel:read:redemptions channel:read:ads channel:read:editors user:write:chat";


        string channelName = "%null%";
        string channelID = "%null%";

        string botUsername = "SPARK_NET_BOT";
        string clientID = "%null%";
        string clientSecret = "%null%";

        string accessToken = "%null%";
        string refreshToken = "%null%";

        bool disposeTokens = false;

        MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        NHttp.HttpServer WebServer;
        Spark Spark = Classes.Spark;

        static public string twitchPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Classes.Spark.dataPath), "Twitch");
        public string clientDataPath = Path.Combine(twitchPath, "Client.json");
        static public string twitchDataPath = Path.Combine(twitchPath, "Data");
        public string tokenPath = Path.Combine(twitchDataPath, "Tokens.json");
        public string twitchLogsPath = Path.Combine(twitchPath, "Twitch Logs");

        List<string> streamLogs = new List<string>();
        List<string> twitchLogs = new List<string>();
        List<string> messageLogs = new List<string>();
        List<string> moderationLogs = new List<string>();
        List<string> channelPointLogs = new List<string>();
        List<string> viewerLogs = new List<string>();

        List<string> whoVisited = new List<string>();
        List<string> excludedUsers = new List<string>(); // Users that are excluded from the viewer logs.

        List<Tuple<string, List<string>>> logList = new List<Tuple<string, List<string>>>();


        Dictionary<string, string> chatCommands = new Dictionary<string, string>();
        string oldScopes = "%null%";
        string RecentUser = "%null%";

        TwitchClient twitchClient = new TwitchClient();
        public TwitchAPI API = new TwitchAPI();

        int currentViewers = 0;


        #region Methods

        public void Shoutout(string userId)
        {
            API.Helix.Chat.SendShoutoutAsync(GetBroadcasterID(), userId, GetBroadcasterID(), accessToken);
        }

        public void CreateClip()
        {
            if (twitchClient.IsConnected)
            {
                var Clip = API.Helix.Clips.CreateClipAsync(GetBroadcasterID(), accessToken);
                PlaySound("ClipCreated.mp3");
                Log("Clip Created!");
            }
            else
            {
                Log("Failed to create clip! Twitch Client not connected!");
            }

        }

        public void JoinChannel(string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Channel == null)
                {
                    Channel = GetChannel();
                    channelName = Channel;
                    twitchClient.JoinChannel(Channel);
                }
                else
                {
                    channelName = Channel;
                    twitchClient.JoinChannel(Channel);
                }
            }
            else
            {
                Log("Failed to join channel! Twitch Client is not connected!");
            }
        }

        public void LeaveChannel(string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Channel == null)
                {
                    Channel = GetChannel();
                }
                twitchClient.LeaveChannel(Channel);
            }
            else
            {
                Log("Failed to leave channel! Twitch Client is not connected!");
            }
        }



        public void SendAnnouncement(string Message, string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Channel == null)
                {
                    Channel = GetChannel();
                }
                twitchClient.Announce(Channel, Message);
            }
            else
            {
                Log("Failed to send announcement! Twitch Client is not connected!");
            }
        }

        public void FollowersOnly(bool Value, string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Channel == null)
                {
                    Channel = GetChannel();
                }
                if (Value == true)
                {
                    twitchClient.FollowersOnlyOn(Channel, TimeSpan.FromHours(48));
                }
                else
                {
                    twitchClient.FollowersOnlyOff(Channel);
                }
            }
            else
            {
                Log("Failed to set followers only! Twitch Client is not connected!");
            }
        }


        private string GetChannel()
        {
            string Channel = null;
            API.Settings.ClientId = clientID;
            API.Settings.AccessToken = accessToken;
            var User = API.Helix.Users.GetUsersAsync();
            Channel = User.Result.Users[0].Login.ToString();
            return Channel;
        }


        public async Task ConnectClient()
        {
            ConnectionCredentials Credentials = new ConnectionCredentials(botUsername, accessToken);

            twitchClient.Initialize(Credentials);

            twitchClient.Connect();

            ConnectEvents();

            Log("Connected To Twitch!");
            JoinChannel();
        }


        /// <summary>
        /// Bans a user from the channel via UserID.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public async Task BanUser(string userID, int duration = -1, string? Reason = "")
        {
            if (twitchClient.IsConnected)
            {
                try
                {
                    await API.Helix.Moderation.BanUserAsync(
                    GetBroadcasterID(),                 // broadcasterId: *which* channel to ban the user from; hope you're a mod there!
                    GetBroadcasterID(),                 // moderatorId: *who* is running the ban. This should be the same as the broadcasterId
                    new TwitchLib.Api.Helix.Models.Moderation.BanUser.BanUserRequest
                    {
                        UserId = userID,    // *who* is getting banned
                        Reason = Reason,    // (optional) *why* are they getting banned
                        Duration = null // (optional) how long to time them out for, or `null` a for perma-ban
                    },
                          accessToken                  // accessToken: (optional) required to ban *as* someone else, or *if* skipped above
                    );
                    Log("Banned Twitch User: " + GetUserFromID(userID) + "for " + duration);
                    StoreLog(moderationLogs, GetUserFromID(userID) + " has been banned for " + duration + " seconds!");
                }
                catch (Exception)
                {
                    Log("Ban Failed! Invalid Ban Credentials!");
                    throw;
                }
            }
            else
            {
                Log("Failed to ban user! Twitch Client is not connected!");
            }
        }


        /// <summary>
        /// Sends a message to the specific Twitch channel, otherwise uses the currently connected channel.
        /// </summary>
        /// <param name="Message"></param>
        public void SendMessage(string Message, string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Channel == null)
                {
                    Channel = channelName;
                }
                Spark.DebugLog(channelName);
                twitchClient.SendMessage(channelName, Message);
            }
            else
            {
                Log("Failed to send message! Twitch Client is not connected!");
            }
        }


        /// <summary>
        /// Bans the most recent user that sent a message in the chat.
        /// </summary>
        public void BanRecentUser()
        {
            BanUser(RecentUser, -1, "Banned by SPARK");
            PlaySound("BannedRecentUser.wav");
        }


        private void AddCommand(string Command, string ActionID)
        {
            Command = Command.ToLower();
            ActionID = ActionID.ToLower();
            chatCommands.Add(Command, ActionID);
        }


        /// <summary>
        /// Logs a message to the console box.
        /// </summary>
        /// <param name="Text"></param>
        /// <param name="Force"></param>
        public void Log(string Text, bool Force = false)
        {
            Spark.Log("Twitch: " + Text, Color.MediumPurple, Force);
        }


        /// <summary>
        /// Runs a command with an optional parameter.
        /// </summary>
        /// <param name="Command"></param>
        /// <param name="Parameter"></param>
        public void RunCommand(string Command, string Parameter = "%null%")
        {
            string CasedParameter = Parameter;
            string CasedCommand = Command;

            Command = Command.ToLower();

            string[] Words = Command.Split(' ');
            string Result = string.Join(" ", Command.Split().Take(Words.Length));
            chatCommands.TryGetValue(Words.GetValue(0).ToString(), out string? String);


            Spark.DebugLog(String);

            if (chatCommands.TryGetValue(Words.GetValue(0).ToString(), out string? _))
            {
                Command = Words.GetValue(0).ToString();

                if (Words.Length > 1)
                {
                    CasedParameter = CasedCommand.Substring(Command.Length + 1);
                    Parameter = CasedParameter.ToLower();
                }

                CommandLibrary(Command, Parameter, CasedParameter);
            }
            else
            {
                Classes.Spark.Warn("Twitch Command Not Found!");
            }
        }

        /// <summary>
        /// Loads the connection to Twitch.
        /// </summary>
        /// <param name="forceLoad"></param>
        /// <returns></returns>
        public async Task LoadConnection(bool forceLoad = false)
        {
            if (Spark.twitchConnection || forceLoad == true)
            {
                API.Settings.Secret = clientSecret; API.Settings.AccessToken = accessToken; API.Settings.ClientId = clientID;

                if (clientID == "%null%" || clientID == null || clientSecret == "%null%" || clientSecret == null)
                {
                    Spark.Warn("Twitch ClientID or ClientSecret Is Missing!");
                    Spark.twitchConnection = false;
                }
                else
                {
                    WebServer = new HttpServer();
                    WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 3000);

                    WebServer.RequestReceived += async (s, e) =>
                    {
                        using (var Writer = new StreamWriter(e.Response.OutputStream))
                        {
                            if (e.Request.QueryString.AllKeys.Any("code".Contains))
                            {
                                var Code = e.Request.QueryString["code"];
                                var Tokens = await GetTokens(Code);
                                await ConnectClient();
                                WebServer.Stop();
                                WebServer.Dispose();
                                await _host.StartAsync();
                            }
                        }
                    };
                    Spark.DebugLog(accessToken);
                    ValidateAccessTokenResponse TokenResponse = await API.Auth.ValidateAccessTokenAsync(accessToken);

                    if (TokenResponse == null || oldScopes != scopes)
                    {
                        var Values = new Dictionary<string, string>
                        {
                  { "client_id", clientID },
                { "force_verify", "false" },
                { "redirect_uri", DirectURL },
                { "response_type", "code" },
                { "scope", scopes},
                        };
                        var Content = new FormUrlEncodedContent(Values);



                        WebServer.Start();

                        HttpClient client = new HttpClient();
                        Uri URL = new Uri(DirectURL);
                        client.BaseAddress = URL;
                        client.Timeout = System.TimeSpan.FromSeconds(24);

                        string FullURL = "https://id.twitch.tv/oauth2/authorize?" + Content.ReadAsStringAsync().Result;

                        Process.Start(new ProcessStartInfo(FullURL) { UseShellExecute = true });
                    }
                    else
                    {
                        await ConnectClient();
                        await _host.StartAsync();
                    }
                }
            }
        }

        public void ResetLogs()
        {
            if (twitchLogs.Count >= 1)
            {
                twitchLogs.Clear();
                messageLogs.Clear();
                moderationLogs.Clear();
                channelPointLogs.Clear();
                viewerLogs.Clear();
                streamLogs.Clear();
                whoVisited.Clear();
                Log("Twitch Logs Cleared!");
            }
            else
            {
                Log("No Twitch Logs To Clear!");
            }
        }

        public void ExcludeUser(string User)
        {
            string user = User.ToLower();

            if (!excludedUsers.Contains(user))
            {
                excludedUsers.Add(user);
            }
            else
            {
                Log("User has already been excluded!");
            }
        }



        #endregion


        #region GetMethods

        /// <summary>
        /// Gets the display name of a user from their userID.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public string GetUserFromID(string userID)
        {
            var Users = API.Helix.Users.GetUsersAsync(new List<string> { userID });
            string displayName = Users.Result.Users[0].DisplayName.ToString();
            return displayName;
        }


        public string GetBroadcasterID()
        {
            var User = API.Helix.Users.GetUsersAsync();
            channelID = User.Result.Users[0].Id.ToString();
            return channelID;
        }


        async Task<Tuple<string, string>> GetTokens(string Code)
        {
            HttpClient client = new HttpClient();
            var Values = new Dictionary<string, string>
            {
                { "client_id", clientID },
                { "client_secret", clientSecret },
                { "code", Code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", DirectURL },
            };
            var Content = new FormUrlEncodedContent(Values);

            var Response = await client.PostAsync("https://id.twitch.tv/oauth2/token", Content);

            var ResponseString = await Response.Content.ReadAsStringAsync();
            var Json = JsonObject.Parse(ResponseString);
            accessToken = Json["access_token"].ToString();
            refreshToken = Json["refresh_token"].ToString();
            client.CancelPendingRequests();
            client.Dispose();
            return new Tuple<string, string>(Json["access_token"].ToString(), Json["refresh_token"].ToString());
        }

        #endregion


        #region Events

        private void ViewerLeft(object? sender, TwitchLib.Client.Events.OnUserLeftArgs e)
        {
            currentViewers = -1;
            string Viewer = e.Username;
            StoreLog(viewerLogs, " " + Spark.CurrentTime() + "  -  " + Viewer + " has left the stream!");
            Log("Viewer Left: " + Viewer);
        }

        private void ViewerJoined(object? sender, TwitchLib.Client.Events.OnUserJoinedArgs e)
        {
            currentViewers = +1;
            string Viewer = e.Username;
            StoreViewer(Viewer);
            Log("Viewer Joined: " + Viewer);
        }

        private void JoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            Log("Joined Channel " + e.Channel);
        }

        private void LeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            Log("Left Channel " + e.Channel);
        }
        #endregion


        #region AppHandling

        private void PlaySound(string Sound)
        {
            Spark.PlaySound(Sound, Path.Combine(Classes.Spark.soundsPath, "Twitch"));
        }

        private void ConnectEvents()
        {
            twitchClient.OnJoinedChannel += JoinedChannel;
            twitchClient.OnUserJoined += ViewerJoined;
            twitchClient.OnUserLeft += ViewerLeft;
        }

        /// <summary>
        /// Stores a viewer in the viewer log, ready for saving.
        /// </summary>
        /// <param name="Viewer"></param>
        private void StoreViewer(string Viewer)
        {
            EnsureLimit(viewerLogs, 5000, true);
            StoreLog(viewerLogs, " " + Spark.CurrentTime() + "  -  " + Viewer + " has joined the stream!");

            if (!whoVisited.Contains(Viewer) && !excludedUsers.Contains(Viewer))
            {
                whoVisited.Add(Viewer);
            }
        }

        private async Task SaveData(bool ForceSave = false)
        {
            LoadLogList();


            string _accessToken = "%null%";
            string _refreshToken = "%null%";

            if (!disposeTokens)
            {
                _accessToken = accessToken;
                _refreshToken = refreshToken;
            }

                var clientData = new List<ClientData>
                {
                new ClientData
                {
                    Client_ID = clientID,
                    Client_Secret = clientSecret,
                }
            };


            var tokenData = new List<TokenData>
                {
                new TokenData
                {
                    Access_Token = Spark.Encrypt(accessToken),
                    Refresh_Token = Spark.Encrypt(refreshToken),

                    Scopes = scopes
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            string clientJson = JsonSerializer.Serialize(clientData, options);
            string tokenJson = JsonSerializer.Serialize(tokenData, options);

            await using (StreamWriter swcc = new StreamWriter(clientDataPath))
            {
                await swcc.WriteLineAsync(clientJson);
            }
            await using (StreamWriter swct = new StreamWriter(tokenPath))
            {
                await swct.WriteLineAsync(tokenJson);
            }

            foreach (Tuple<string, List<string>> list in logList)
            {
                if (!IsEmpty(list.Item2) || ForceSave)
                {
                    string logPath = twitchLogsPath + @"\" + DateTime.Now.Day + ";" + DateTime.Now.Month + ";" + DateTime.Now.Year;
                    await Spark.CreatePath(logPath);
                    string FilePath = logPath + @"\" + list.Item1 + ".txt";
                    using var file = System.IO.File.OpenWrite(FilePath);
                    await using (StreamWriter sw = new StreamWriter(file))
                    {
                        foreach (string log in list.Item2)
                        {
                            await sw.WriteLineAsync(log);
                        }
                    }
                }
            }
        }

        public void LoadLogList()
        {
            whoVisited.Sort();
            logList.Add(new Tuple<string, List<string>>("Stream", streamLogs));
            logList.Add(new Tuple<string, List<string>>("Twitch Log", twitchLogs));
            logList.Add(new Tuple<string, List<string>>("Messages", messageLogs));
            logList.Add(new Tuple<string, List<string>>("Moderation", moderationLogs));
            logList.Add(new Tuple<string, List<string>>("Channel Points", channelPointLogs));
            logList.Add(new Tuple<string, List<string>>("Viewers", viewerLogs));
            logList.Add(new Tuple<string, List<string>>("Who Visited", whoVisited));
        }

        /// <summary>
        /// Checks if the provided list is empty.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static bool IsEmpty(List<string> list)
        {
            if (list.Count <= 0)
            {
                return true;

            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Stores the provided data into the provided list, to be ready for saving.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="data"></param>
        /// <param name="dataLimit"></param>
        public void StoreLog(List<string> list, string data, int dataLimit = 5000, bool storeGlobally = true)
        {
            EnsureLimit(list, dataLimit, true);
            list.Add(data);
            if (storeGlobally)
            {
                EnsureLimit(twitchLogs, 999999, true);
                twitchLogs.Add(data);
            }
        }

        /// <summary>
        /// Ensures that the provided list does not exceed the provided limit.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="limit"></param>
        /// <param name="addingValue"></param>
        public static void EnsureLimit(List<string> list, int limit = 5000, bool addingValue = false)
        {
            if (list.Count >= limit)
            {
                if (addingValue)
                {
                    list.RemoveRange(0, limit - 1);
                }
                else
                {
                    list.RemoveRange(0, limit);
                }
            }
        }

        private string ValidateScopes()
        {
            string[] words = scopes.Split(' ');
            string[] distinctWords = words.Distinct().ToArray();
            Array.Sort(distinctWords);
            string output = string.Join(" ", distinctWords);
            Spark.DebugLog(output);
            return output;
        }

        public async Task AppClosing()
        {
            await SaveData();
            if (WebServer != null)
            {
                if (WebServer.State == NHttp.HttpServerState.Started || WebServer.State == HttpServerState.Starting)
                {
                    WebServer.Stop();
                    WebServer.Dispose();
                }
            }
            if (disposeTokens)
            {
                HttpClient client = new HttpClient();
                Uri URL = new Uri(DirectURL);
                await client.PostAsync("https://id.twitch.tv/oauth2/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientID },
                    { "token", accessToken }
                }));
                client.Dispose();
            }
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
        }


        private void BuildLibrary()
        {
            #region ChatCommands
            AddCommand("!say", "say");
            #endregion

            #region ExcludedUsers
            ExcludeUser("SPARK_NET_BOT");
            ExcludeUser("SPARK_NET");
            ExcludeUser("SPARK_NET_DEV");
            ExcludeUser("SPARK_NET_TEST");

            ExcludeUser("FumetsuTheBot");
            ExcludeUser("SoundAlerts");
            ExcludeUser("Streamlabs");
            ExcludeUser("StreamElements");
            ExcludeUser("Nightbot");
            ExcludeUser("Moobot");
            ExcludeUser("SullyGnome");
            ExcludeUser("SullyGnomeBot");
            #endregion
        }


        private void CommandLibrary(string Command, string Parameter, string CasedParameter)
        {
            switch (Command)
            {
                case "say":
                    Spark.Say(CasedParameter); break;
            }
        }


        public async Task AppLoad()
        {
            ValidateScopes();
            BuildLibrary();
            await LoadData();
            await LoadConnection();
        }


        private async Task LoadData()
        {
            ClientData clientData = new ClientData();
            TokenData tokenData = new TokenData();

            if (File.Exists(clientDataPath))
            {
                var clientJson = File.ReadAllText(clientDataPath);

                string[] clientKeys = { "Client_ID", "Client_Secret" };
                if (Spark.JsonContainsKeys(clientDataPath, clientKeys))
                {
                    var Json = JsonSerializer.Deserialize<ClientData[]>(clientJson);

                    clientID = Json[0].Client_ID;
                    clientSecret = Json[0].Client_Secret;
                }
                else
                {
                    await Spark.CreateFile(clientDataPath);
                    Spark.DebugLog("Client Data Missing!");
                }
            }
            else
            {
                await Spark.CreateFile(clientDataPath);
                Spark.DebugLog("Client Data Missing!");
            }


            if (File.Exists(tokenPath))
            {
                string[] tokenKeys = {"Access_Token", "Refresh_Token", "Scopes" };
                var tokenJson = File.ReadAllText(tokenPath);

                if (Spark.JsonContainsKeys(tokenPath, tokenKeys))
                {
                    var Json = JsonSerializer.Deserialize<TokenData[]>(tokenJson);

                    accessToken = Spark.Decrypt(Json[0].Access_Token);
                    refreshToken = Spark.Decrypt(Json[0].Refresh_Token);
                    oldScopes = Json[0].Scopes;
                }
                else
                {
                    await Spark.CreateFile(tokenPath);
                    Spark.DebugLog("Token Data Missing!");
                }
            }
            else
            {
                await Spark.CreateFile(tokenPath);
                Spark.DebugLog("Token Data Missing!");
            }
        }

        #endregion



        public class EventSubService : IHostedService
        {
            private readonly ILogger<EventSubService> _logger;
            private readonly EventSubWebsocketClient _eventSubWebsocketClient;
            private readonly TwitchAPI _twitchAPI = Classes.Twitch.API;
            private string _userId;

            Twitch Twitch = Classes.Twitch;
            Spark Spark = Classes.Spark;


            #region Events

            #region Viewers
            private async Task ChannelPointRedemption(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string reward = eventData.Reward.Title;
                string user = eventData.UserName;

                Twitch.Log($"{user} Redeemed {reward} for {eventData.Reward.Cost} Poinks!");
                Twitch.StoreLog(Classes.Twitch.channelPointLogs, $"{user} Redeemed {reward} for {eventData.Reward.Cost} Channel Points!");
                Twitch.SendMessage(user + " Redeemed " + reward + " for " + eventData.Reward.Cost + " Poinks!");
            }

            private async Task MessageReceived(object? sender, ChannelChatMessageArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.ChatterUserName;
                string broadcaster = eventData.BroadcasterUserName;
                string message = eventData.Message.Text;
                string messageId = eventData.MessageId;

                Spark.Log(eventData.ChatterUserName + ": " + eventData.Message.Text, Color.MediumPurple);
                Twitch.StoreLog(Classes.Twitch.messageLogs, $"{eventData.ChatterUserName}: {eventData.Message.Text}");
            }
            #endregion


            #region Stream
            private async Task OnChannelRaid(object? sender, ChannelRaidArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.ToBroadcasterUserName;
                string raider = eventData.FromBroadcasterUserName;
                int viewers = eventData.Viewers;

                Twitch.Log($"{raider} is raiding with {viewers} viewers!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"{raider} is raiding with {viewers} viewers!");
            }

            private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.UserName} just followed!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.UserName} just followed!");
            }


            private async Task OnShoutoutReceived(object? sender, ChannelShoutoutReceiveArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.BroadcasterUserName;
                string receiver = eventData.ToBroadcasterUserId;

                Twitch.Log($"You have received a shoutout from {user}");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"You have received a shoutout from {user}");
            }

            private async Task ShoutoutGiven(object? sender, ChannelShoutoutCreateArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.ModeratorUserName;
                string receiver = eventData.ToBroadcasterUserName;

                Twitch.Log($"{eventData.ModeratorUserName} gave a shoutout to {eventData.ToBroadcasterUserName}");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.ModeratorUserName} gave a shoutout to {eventData.ToBroadcasterUserName}");
            }


            private async Task OnStreamOnline(object? sender, StreamOnlineArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.BroadcasterUserName} is now live!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"You have started streaming at {Spark.CurrentTime()}");
            }

            private async Task OnStreamOffline(object? sender, StreamOfflineArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.BroadcasterUserName} is no longer live!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"You have stopped streaming at {Spark.CurrentTime()}");
            }

            private async Task OnChannelUpdate(object? sender, ChannelUpdateArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string category = eventData.CategoryName;

                Twitch.Log($"The channel has been updated with title ({title}) and category ({category})");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"The channel has been updated with title ({title}) and category ({category})");
            }


            private async Task OnChannelGoalBegin(object? sender, ChannelGoalBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int currentamount = eventData.CurrentAmount;
                int goal = eventData.TargetAmount;

                switch (eventData.Type)
                {
                    case "followers":
                        Twitch.Log($"{eventData.BroadcasterUserName} has started a new follower goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has started a new follower goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        break;
                    case "subscriptions":
                        Twitch.Log($"{eventData.BroadcasterUserName} has started a new subscriber goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has started a new subscriber goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        break;
                }
            }

            private async Task OnChannelGoalProgress(object? sender, ChannelGoalProgressArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int currentamount = eventData.CurrentAmount;
                int goal = eventData.TargetAmount;
            }

            private async Task OnChannelGoalCompleted(ChannelGoalEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string goalType = eventData.Type;
                int currentamount = eventData.CurrentAmount;
                int goal = eventData.TargetAmount;

                switch (goalType)
                {
                    case "followers":
                        Twitch.Log($"{eventData.BroadcasterUserName} has completed a new follower goal: {currentamount}  /  {goal}");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has completed a follower goal: {currentamount} / {goal}");
                        break;
                    case "subscriptions":
                        Twitch.Log($"{eventData.BroadcasterUserName} has completed a new subscriber goal: {currentamount} / {goal}");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has completed a subscriber goal: {currentamount} / {goal}");
                        break;
                }
            }

            private async Task OnChannelGoalEnd(object? sender, ChannelGoalEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int currentamount = eventData.CurrentAmount;
                int goal = eventData.TargetAmount;
                if (eventData.IsAchieved) { OnChannelGoalCompleted(e); }

                else
                {
                    switch (eventData.Type)
                    {
                        case "followers":
                            Twitch.Log($"{eventData.BroadcasterUserName} has ended a follower goal: {currentamount} / {goal}");
                            Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has ended a follower goal: {currentamount} / {goal}");
                            break;
                        case "subscriptions":
                            Twitch.Log($"{eventData.BroadcasterUserName} has ended a subscriber goal: {currentamount} / {goal}");
                            Twitch.StoreLog(Classes.Twitch.streamLogs, $"{eventData.BroadcasterUserName} has ended a subscriber goal: {currentamount} / {goal}");
                            break;
                    }
                }
            }


            private async Task HypeTrainBegin(object? sender, ChannelHypeTrainBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;

                Twitch.Log($"A hype train has started at {level}!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"A hype train has started!");
            }

            private async Task HypeTrainProgress(object? sender, ChannelHypeTrainProgressArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;
                int total = eventData.Total;
                int goal = eventData.Goal;
                if (total >= goal) { HypeTrainProgressLevel(e); }

                else
                {

                }
            }

            private async Task HypeTrainProgressLevel(ChannelHypeTrainProgressArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;
                int total = eventData.Total;

                Twitch.Log($"The hype train has progressed to level {level}!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"The hype train has progressed to level {level}!");
            }

            private async Task HypeTrainEnd(object? sender, ChannelHypeTrainEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;
                int total = eventData.Total;

                Twitch.Log($"A hype train has ended at level {level} with {total} total points!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"A hype train has ended at level {level} with {total} total points!");
            }


            private async Task ChannelVipAdded(object? sender, ChannelVipArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been given VIP status!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} has been given VIP status!");
            }

            private async Task ChannelVipRemoved(object? sender, ChannelVipArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been revoked VIP status!");
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} has been revoked VIP status!");
            }


            private async Task ChannelModeratorAdded(object? sender, ChannelModeratorArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been made a moderator!");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has been made a moderator!");
                Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been made a moderator!", storeGlobally: false);
            }

            private async Task ChannelModeratorRemoved(object? sender, ChannelModeratorArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been removed as a moderator!");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has been removed as a moderator!");
                Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been removed as a moderator!", storeGlobally: false);
            }


            private async Task ChannelPollBegan(object? sender, ChannelPollBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"A new poll has been started: {title}");
                Twitch.StoreLog(Twitch.streamLogs, $"A new poll has been started: {title}");
            }

            private async Task ChannelPollProgress(object? sender, ChannelPollProgressArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;
            }

            private async Task ChannelPollEnded(object? sender, ChannelPollEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;
                string status = eventData.Status;

                switch (status)
                {
                    case "completed":
                        Twitch.Log($"The poll has ended: {title}");
                        Twitch.StoreLog(Twitch.streamLogs, $"The poll has ended: {title}");
                        break;
                    case "terminated":
                        Twitch.Log($"The poll has been terminated: {title}");
                        Twitch.StoreLog(Twitch.streamLogs, $"The poll has been terminated: {title}");
                        break;
                    case "archived":
                        Twitch.Log($"The poll has been archived: {title}");
                        Twitch.StoreLog(Twitch.streamLogs, $"The poll has been archived: {title}");
                        break;
                }
            }


            private async Task ChannelPredictionBegan(object? sender, ChannelPredictionBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"A new prediction has been started: {title}");
                Twitch.StoreLog(Twitch.streamLogs, $"A new prediction has been started: {title}");
            }

            private async Task ChannelPredictionLocked(object? sender, ChannelPredictionLockArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;
            }

            private async Task ChannelPredictionProgress(object? sender, ChannelPredictionProgressArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;
            }

            private async Task ChannelPredictionEnded(object? sender, ChannelPredictionEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string broadcaster = eventData.BroadcasterUserName;
                string status = eventData.Status;

                switch (status)
                {
                    case "resolved":
                        Twitch.Log($"The prediction has ended: {title}");
                        Twitch.StoreLog(Twitch.streamLogs, $"The prediction has ended: {title}");
                        break;
                    case "canceled":
                        Twitch.Log($"The prediction has been cancelled: {title}");
                        Twitch.StoreLog(Twitch.streamLogs, $"The prediction has been cancelled: {title}");
                        break;
                }
            }

            #endregion


            #region Moderation
            private async Task OnUserBanned(object? sender, ChannelBanArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string moderator = eventData.ModeratorUserName;
                string reason = eventData.Reason;

                if (eventData.IsPermanent)
                {
                    if (eventData.Reason != "")
                    {
                        Twitch.Log($"{user} has been banned by {moderator} for {reason}");
                        Twitch.StoreLog(Classes.Twitch.moderationLogs, $"{user} has been banned by {moderator} for {reason}");
                    }
                    else
                    {
                        Twitch.Log($"{user} has been banned by {moderator}");
                        Twitch.StoreLog(Classes.Twitch.moderationLogs, $"{user} has been banned by {moderator}");
                    }
                }
                else
                {
                    if (eventData.Reason != "")
                    {
                        Twitch.Log($"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds for {reason}!");
                        Twitch.StoreLog(Classes.Twitch.moderationLogs, $"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds for {reason}!");
                    }
                    else
                    {
                        Twitch.Log($"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds!");
                        Twitch.StoreLog(Classes.Twitch.moderationLogs, $"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds!");
                    }
                }
            }

            private async Task OnUserUnbanned(object? sender, ChannelUnbanArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string moderator = eventData.ModeratorUserName;

                Twitch.Log($"{user} has been unbanned by {moderator}");
                Twitch.StoreLog(Classes.Twitch.moderationLogs, $"{user} has been unbanned by {moderator}");
            }


            private async Task ChannelWarningSent(object? sender, ChannelWarningSendArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string moderator = eventData.ModeratorUserName;
                string reason = eventData.Reason;

                Twitch.Log($"A warning has been to {user} by {moderator} for: {reason}");
                Twitch.StoreLog(Twitch.moderationLogs, $"A warning has been to {user} by {moderator} for: {reason}");
            }

            private async Task ChannelWarningAcknowledged(object? sender, ChannelWarningAcknowledgeArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"A warning has been acknowledged by {user}");
                Twitch.StoreLog(Twitch.moderationLogs, $"A warning has been acknowledged by {user}");
            }
            #endregion


            #region Earnings
            private async Task BitsCheered(object? sender, ChannelCheerArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string message = eventData.Message;
                int bits = eventData.Bits;

                Twitch.Log(eventData.UserName + " has cheered " + eventData.Bits + " bits!");
                Twitch.StoreLog(Twitch.streamLogs, $"{eventData.UserName} cheered {eventData.Bits} bits!");
            }
            private async Task ChannelAdBreakBegan(object? sender, ChannelAdBreakBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string requester = eventData.RequesterUserName;
                int duration = eventData.DurationSeconds;

                Twitch.Log($"An ad break has started by {requester} for {duration} seconds!");
                Twitch.StoreLog(Twitch.streamLogs, $"An ad break has started by {requester} for {duration} seconds!");
            }


            private async Task ReceivedSub(object? sender, ChannelSubscribeArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string tier = eventData.Tier;

                switch (tier)
                {
                    case "1000":
                        Twitch.Log($"{user} just Tier 1 subscribed!");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} just Tier 1 subscribed!");
                        break;
                    case "2000":
                        Twitch.Log($"{user} just Tier 2 subscribed!");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} just Tier 2 subscribed!");
                        break;
                    case "3000":
                        Twitch.Log($"{user} just Tier 3 subscribed!");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} just Tier 3 subscribed!");
                        break;
                    default:
                        Twitch.Log($"{user} is a mysterious subscriber!");
                        Twitch.StoreLog(Classes.Twitch.streamLogs, $"{user} is a mysterious subscriber!");
                        break;
                }
                Twitch.StoreLog(Classes.Twitch.streamLogs, $"The rest of the outside of the Subscriber Switch case still works!");
            }
            private async Task ChannelSubscriptionEnded(object? sender, ChannelSubscriptionEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                switch (eventData.Tier)
                {
                    case "1000":
                        Twitch.Log($"{user} has ended their Tier 1 subscription!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} has ended their Tier 1 subscription!");
                        break;
                    case "2000":
                        Twitch.Log($"{user} has ended their Tier 2 subscription!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} has ended their Tier 2 subscription!");
                        break;
                    case "3000":
                        Twitch.Log($"{user} has ended their Tier 3 subscription!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} has ended their Tier 3 subscription!");
                        break;
                }
            }

            private async Task ChannelSubscriptionGifted(object? sender, ChannelSubscriptionGiftArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                int total = eventData.Total;
                string tier = "??";

                switch (eventData.Tier)
                {
                    case "1000":
                        tier = "1";
                        break;
                    case "2000":
                        tier = "2";
                        break;
                    case "3000":
                        tier = "3";
                        break;
                }

                Twitch.Log($"{user} has gifted {total} Tier {tier} Subs!");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has gifted {total} Tier {tier} Subs!");
            }

            private async Task ChannelSubscriptionMessage(object? sender, ChannelSubscriptionMessageArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string message = eventData.Message.Text;
                int? streak = eventData.StreakMonths;
                int totalmonths = eventData.CumulativeMonths;

                Twitch.Log($"{user} has sent a subscription message: {message}");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has sent a subscription message: {message}");
            }
            #endregion


            private async Task UserWhisperMessageReceived(object? sender, UserWhisperMessageArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string fromuser = eventData.FromUserName;
                string touser = eventData.ToUserName;
                string message = eventData.Whisper.Text;

                Twitch.Log($"{fromuser} has sent you a whisper!");
            }

            #endregion


            // Add Event Connections Here!
            public EventSubService(ILogger<EventSubService> logger, EventSubWebsocketClient eventSubWebsocketClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));

                _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));
                _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
                _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
                _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
                _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;


                #region Viewers
                _eventSubWebsocketClient.ChannelChatMessage += MessageReceived;
                _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += ChannelPointRedemption;
                #endregion


                #region Stream
                _eventSubWebsocketClient.StreamOnline += OnStreamOnline;
                _eventSubWebsocketClient.StreamOffline += OnStreamOffline;
                _eventSubWebsocketClient.ChannelUpdate += OnChannelUpdate;

                _eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
                _eventSubWebsocketClient.ChannelRaid += OnChannelRaid;

                _eventSubWebsocketClient.ChannelShoutoutCreate += ShoutoutGiven;
                _eventSubWebsocketClient.ChannelShoutoutReceive += OnShoutoutReceived;

                _eventSubWebsocketClient.ChannelGoalBegin += OnChannelGoalBegin;
                _eventSubWebsocketClient.ChannelGoalProgress += OnChannelGoalProgress;
                _eventSubWebsocketClient.ChannelGoalEnd += OnChannelGoalEnd;

                _eventSubWebsocketClient.ChannelPollBegin += ChannelPollBegan;
                _eventSubWebsocketClient.ChannelPollProgress += ChannelPollProgress;
                _eventSubWebsocketClient.ChannelPollEnd += ChannelPollEnded;

                _eventSubWebsocketClient.ChannelHypeTrainBegin += HypeTrainBegin;
                _eventSubWebsocketClient.ChannelHypeTrainProgress += HypeTrainProgress;
                _eventSubWebsocketClient.ChannelHypeTrainEnd += HypeTrainEnd;

                _eventSubWebsocketClient.ChannelPredictionBegin += ChannelPredictionBegan;
                _eventSubWebsocketClient.ChannelPredictionLock += ChannelPredictionLocked;
                _eventSubWebsocketClient.ChannelPredictionProgress += ChannelPredictionProgress;
                _eventSubWebsocketClient.ChannelPredictionEnd += ChannelPredictionEnded;

                _eventSubWebsocketClient.ChannelModeratorAdd += ChannelModeratorAdded;
                _eventSubWebsocketClient.ChannelModeratorRemove += ChannelModeratorRemoved;

                _eventSubWebsocketClient.ChannelVipAdd += ChannelVipAdded;
                _eventSubWebsocketClient.ChannelVipRemove += ChannelVipRemoved;
                #endregion


                #region Moderation
                _eventSubWebsocketClient.ChannelBan += OnUserBanned;
                _eventSubWebsocketClient.ChannelUnban += OnUserUnbanned;

                _eventSubWebsocketClient.ChannelWarningSend += ChannelWarningSent;
                _eventSubWebsocketClient.ChannelWarningAcknowledge += ChannelWarningAcknowledged;
                #endregion


                #region Earnings
                _eventSubWebsocketClient.ChannelAdBreakBegin += ChannelAdBreakBegan;
                _eventSubWebsocketClient.ChannelCheer += BitsCheered;

                _eventSubWebsocketClient.ChannelSubscribe += ReceivedSub;
                _eventSubWebsocketClient.ChannelSubscriptionGift += ChannelSubscriptionGifted;
                _eventSubWebsocketClient.ChannelSubscriptionMessage += ChannelSubscriptionMessage;
                _eventSubWebsocketClient.ChannelSubscriptionEnd += ChannelSubscriptionEnded;
                #endregion


                _eventSubWebsocketClient.UserWhisperMessage += UserWhisperMessageReceived;


                _userId = _twitchAPI.Helix.Users.GetUsersAsync().Result.Users[0].Id;
            }


            // Add Eventsub Subscriptions Here!
            private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
            {
                Twitch.Log("Websocket Connected!");

                if (!e.IsRequestedReconnect)
                {
                    var condition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "moderator_user_id", _userId } };
                    var broadcasterCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId } };
                    var messageCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "user_id", _userId } };
                    var raidCondition = new Dictionary<string, string> { { "to_broadcaster_user_id", _userId } };

                    #region Subscriptions

                    #region Stream
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.follow", "2", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);

                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.shoutout.create", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.shoutout.receive", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);

                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.online", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.offline", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.update", "2", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);

                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.raid", "1", raidCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);

                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.goal.begin", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.goal.progress", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.goal.end", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    #endregion


                    #region Viewers
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.chat.message", "1", messageCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    #endregion


                    #region Moderation
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.ban", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.unban", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    #endregion


                    #region Earnings
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.cheer", "1", condition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);

                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.subscribe", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.subscription.gift", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.subscription.end", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.subscription.message", "1", broadcasterCondition, EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId, accessToken: Classes.Twitch.accessToken);
                    #endregion

                    #endregion
                }
            }





            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _eventSubWebsocketClient.ConnectAsync();
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _eventSubWebsocketClient.DisconnectAsync();
            }


            private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
            {
                int reconnectDelay = 1; // In seconds.
                Twitch.Log("Websocket Disconnected!");


                while (!await _eventSubWebsocketClient.ReconnectAsync() && reconnectDelay <= 10)
                {
                    if ((reconnectDelay + 1) <= 10)
                    {
                        Twitch.Log($"Websocket failed to reconnect!");
                    }
                    else
                    {
                        Twitch.Log($"Websocket failed to reconnect! Trying again in {reconnectDelay} seconds..");
                        await Task.Delay(reconnectDelay * 1000);
                        reconnectDelay += 1;
                    }
                }
            }

            private async Task OnWebsocketReconnected(object? sender, EventArgs e)
            {
                Twitch.Log("Websocket Reconnected!");
            }

            private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
            {
                Twitch.Log("An error has occured with the Websocket!");
            }
        }



        public class ClientData
        {
            public string Client_ID { get; set; }
            public string Client_Secret { get; set; }
        }

        public class TokenData
        {
            public string Access_Token { get; set; }
            public string Refresh_Token { get; set; }

            public string Scopes { get; set; }
        }
    }
}
