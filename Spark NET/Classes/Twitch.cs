using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NHttp;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.EventArgs.User;
using TwitchLib.EventSub.Websockets.Extensions;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using WinFormsApp1.Designs;

// Add Chat Command Support
// Add support for Connecting to Non-Broadcaster Channels
// Add support for Same-Date Twitch Logs
// Add more Twitch Logging (Chat Commands)
// Add Channel Bot Support?

namespace WinFormsApp1.Classes
{
    public class Twitch
    {
        readonly public string moduleName = "Twitch";


        bool logMessages = true;
        bool offlineLogging = true;

        string subscriptionService = "eventsub"; // EventSub, PubSub, None

        string DirectURL = "http://localhost:3000";

        string scopes = "channel:read:predictions moderator:read:followers channel:manage:polls channel:read:polls channel:read:goals channel:read:redemptions channel:read:hype_train channel:read:charity channel:read:ads user:read:chat user:bot moderator:manage:shoutouts moderator:read:shoutouts moderator:read:followers channel:manage:raids user:read:chat user:bot channel:bot user:read:chat user:write:chat chat:read chat:edit channel:manage:redemptions channel:manage:raids channel:read:subscriptions channel:read:vips channel:manage:vips moderation:read moderator:read:banned_users moderator:manage:banned_users moderator:read:blocked_terms moderator:read:chat_messages moderator:manage:chat_messages moderator:read:chat_settings moderator:read:chatters moderator:read:followers moderator:read:moderators moderator:read:shoutouts moderator:manage:shoutouts moderator:read:vips user:bot user:read:broadcast user:read:follows user:read:subscriptions user:read:subscriptions user:manage:chat_color moderator:manage:shoutouts moderator:read:shoutouts moderator:read:moderators moderator:read:chatters moderator:read:followers moderator:manage:chat_messages moderator:read:chat_messages moderator:manage:banned_users moderation:read channel:read:subscriptions channel:read:redemptions bits:read clips:edit moderator:manage:banned_users moderator:read:banned_users user:bot moderator:read:chatters moderator:read:followers moderator:read:moderators moderation:read channel:moderate channel:bot chat:read chat:edit bits:read channel:read:ads channel:read:editors user:write:chat";
        // https://dev.twitch.tv/docs/authentication/scopes/

        string commandPrefix = "!"; // The prefix for chat commands.
        Dictionary<string, Dictionary<string, Tuple<bool, TriggerConditions>>> triggers = new();

        string channelName = "";
        string channelID = "";

        string botUsername = "SPARK_NET_BOT";
        string clientID = "";
        string clientSecret = "";

        string accessToken = "%null%";
        string refreshToken = "%null%";

        bool disposeTokens = false;

        MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        NHttp.HttpServer WebServer;
        Spark Spark = Classes.Spark;

        bool alternateClient = true;

        bool autoConnectClient = true; // Automatically connect the Twitch Client on startup.
        bool autoConnectSubscriptionService = true; // Automatically connect the EventSub service on startup.


        #region Paths
        static public string twitchPath = Path.Combine(Path.Combine(Classes.Spark.documentsPath, Classes.Spark.dataPath), "Twitch");
        public string clientDataPath = Path.Combine(twitchPath, "Client.json");
        static public string twitchDataPath = Path.Combine(twitchPath, "Data");
        public string tokenPath = Path.Combine(twitchDataPath, "Tokens.json");
        public string twitchLogsPath = Path.Combine(twitchPath, "Twitch Logs");
        public string triggersPath = Path.Combine(twitchDataPath, "Triggers");
        #endregion

        #region Logs
        List<string> streamLogs = new();
        List<string> twitchLogs = new();
        List<string> messageLogs = new();
        List<string> moderationLogs = new();
        List<string> channelPointLogs = new();
        List<string> viewerLogs = new();
        List<string> chatCommandLogs = new();

        List<string> whoVisited = new();
        List<string> excludedUsers = new(); // Users that are excluded from the viewer logs.

        List<Tuple<string, List<string>>> logList = new();
        #endregion


        Dictionary<string, string> chatCommands = new();
        string oldScopes = "%null%";
        string RecentUser = "%null%";

        readonly TwitchClient twitchClient = new();
        readonly Client client = new();
        readonly public TwitchAPI API = new();
        readonly public IHost eventSub = new HostBuilder().ConfigureServices((hostContext, services) =>
        {
            services.AddTwitchLibEventSubWebsockets();
            services.AddHostedService<EventSubService>();
        }).Build();
        readonly PubSubService PubSub = new();

        int currentViewers = 0;

        Dictionary<string, List<string>> requiredScopes = new();
        List<string> usedScopes = new();
        List<string> unneededScopes = new();

        
        #region Methods

        public void Shoutout(string userId)
        {
            try
            {
                API.Helix.Chat.SendShoutoutAsync(GetBroadcaster().Id, userId, GetBroadcaster().Id, accessToken);
            }
            catch (Exception e)
            {
                Log("Failed to send shoutout! Exception: " + e.Message);
            }
        }

        /// <summary>
        /// Creates a clip of the approximately last 30 seconds.
        /// </summary>
        public void CreateClip()
        {
            try
            {
                var Clip = API.Helix.Clips.CreateClipAsync(GetBroadcaster().Id, accessToken);
                PlaySound("ClipCreated.mp3");
                Log("Clip Created!");
            }
            catch (Exception e)
            {
                Log("Failed to create clip! Exception: " + e.Message);
            }
        }

        public void JoinChannel(string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                try
                {
                    if (Channel == null)
                    {
                        Channel = GetBroadcaster().Login;
                    }

                    channelName = Channel;
                    twitchClient.JoinChannel(Channel);
                }
                catch (Exception e)
                {
                    Log("Failed to join channel! Exception: " + e.Message);
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
                try
                {
                    if (Channel == null)
                    {
                        Channel = GetBroadcaster().Login;
                    }
                    twitchClient.LeaveChannel(Channel);
                }
                catch (Exception e)
                {
                    Log("Failed to leave channel! Exception: " + e.Message);
                }
            }

            else
            {
                Log("Failed to leave channel! Twitch Client is not connected!");
            }
        }



        public void SendAnnouncement(string Message, string? Channel = null)
        {
            try
            {
                if (Channel == null)
                {
                    Channel = GetBroadcaster().Id;
                }
                API.Helix.Chat.SendChatAnnouncementAsync(Channel, Channel, Message);
            }
            catch (Exception e)
            {
                Log("Failed to send announcement! Exception: " + e.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="channel"></param>
        /// <param name="requiredFollowTime">How long someone has to be followed to send messages. In hours.</param>
        public void FollowersOnly(bool value, string? channel = null, int requiredFollowTime = 24)
        {
            if (channel == null)
            {
                channel = GetBroadcaster().Id;
            }
            try
            {
                API.Helix.Chat.UpdateChatSettingsAsync(channel, channel, new TwitchLib.Api.Helix.Models.Chat.ChatSettings.ChatSettings
                {
                    FollowerMode = value,
                    FollowerModeDuration = requiredFollowTime
                }, accessToken);
            }
            catch (Exception e)
            {
                Log("Failed to set followers only status! Exception: " + e.Message);
            }
        }

        /// <summary>
        /// Returns channel information based on the type provided.
        /// </summary>
        /// <remarks>If no identifier is provided, will get the broadcaster channel.</remarks>
        /// <param name="type">What information of the channel to return. Values: id, username, login.</param>
        /// <param name="login">Find channel by login.</param>
        /// <param name="id">Find channel by id.</param>
        private string GetChannele(string type = "login", string? login = null, string? id = null)
        {
            try
            {
                string Channel = null;
                Spark.DebugLog($"2) Client ID: {clientID}");
                Spark.DebugLog($"2) Access Token: {accessToken}");
                API.Settings.ClientId = clientID;
                API.Settings.AccessToken = accessToken;
                var User = API.Helix.Users.GetUsersAsync();

                if (login != null)
                {
                    User = API.Helix.Users.GetUsersAsync([login]);
                }
                else if (id != null)
                {
                    User = API.Helix.Users.GetUsersAsync(ids: [id]);
                }


                switch (type.ToLower())
                {
                    case "id":
                        Channel = User.Result.Users[0].Id.ToString();
                        break;
                    case "username":
                        Channel = User.Result.Users[0].DisplayName.ToString();
                        break;
                    case "login":
                        Channel = User.Result.Users[0].Login.ToString();
                        break;
                    default:
                        Spark.DebugLog("Invalid GetChannel type, defaulting to login.");
                        Channel = User.Result.Users[0].Login.ToString();
                        break;
                }
                return Channel;
            }
            catch (Exception e)
            {
                Spark.DebugLog("Exception thrown when getting channel: " + e.Message + " at: " + e.InnerException);
                return null;
            }
        }

        private void LoadAPI()
        {
            API.Settings.AccessToken = accessToken;
            API.Settings.ClientId = clientID;
            API.Settings.Secret = clientSecret;
        }

        /// <summary>
        /// Connects the Twitch Client to the current channel.
        /// </summary>
        /// <returns></returns>
        public async Task ConnectClient(bool autoJoin = true)
        {
            if (alternateClient)
            {
                if (!client.twitchClient.IsConnected)
                {
                    await client.Start();
                }
            }
            else
            {
                if (!twitchClient.IsConnected)
                {
                    try
                    {
                        Classes.Spark.DebugLog("Used Token: -  " + accessToken);
                        ConnectionCredentials Credentials = new ConnectionCredentials(botUsername, accessToken);


                        twitchClient.Initialize(Credentials);

                        twitchClient.OnJoinedChannel += JoinedChannel;
                        twitchClient.OnLeftChannel += LeftChannel;
                        twitchClient.OnUserJoined += ViewerJoined;
                        twitchClient.OnUserLeft += ViewerLeft;

                        twitchClient.Connect();

                        Classes.Twitch.ExcludeUser(Classes.Twitch.GetUserFromID(Classes.Twitch.GetBroadcaster().Id));
                        Classes.Twitch.Log("Connected To Twitch!");
                        if (autoJoin == true)
                        {
                            JoinChannel();
                        }
                    }
                    catch (Exception e)
                    {
                        Classes.Twitch.Log($"Exception thrown while starting Twitch Client: {e.Message} at: {e.Source}");
                    }
                }
                else
                {
                    Classes.Twitch.Log($"Failed to Connect Twitch Client: Client is already Connected!");
                }
            }
        }


        /// <summary>
        /// Bans a user from the channel via UserID.
        /// </summary>
        /// <param name="userID">The UserID of the user getting banned.</param>
        /// <param name="duration">How long in seconds for the user to be banned. Set to null or -1 for infinite duration.</param>
        /// <param name="Reason">Optional reason for why the user was banned.</param>
        public async void BanUser(string userID, int? duration = -1, string? Reason = "")
        {
            userID = userID.ToLower();

            if (duration <= -1)
            {
                duration = null;
            }

            try
            {
                await API.Helix.Moderation.BanUserAsync(
                GetBroadcaster().Id,                 // broadcasterId: *which* channel to ban the user from; make sure you're a mod there!
                GetBroadcaster().Id,                 // moderatorId: *who* is running the ban.
                new TwitchLib.Api.Helix.Models.Moderation.BanUser.BanUserRequest
                {
                    UserId = userID,    // *who* is getting banned
                    Reason = Reason,    // (optional) *why* are they getting banned
                    Duration = duration // (optional) how long to time them out for, or `null` a for perma-ban
                },
                      accessToken                  // accessToken: (optional) required to ban *as* someone else, or *if* skipped above
                );
                Log("Banned Twitch User: " + GetUserFromID(userID) + "for " + duration);
                StoreLog(moderationLogs, GetUserFromID(userID) + " has been banned for " + duration + " seconds!");
            }
            catch (Exception)
            {
                Log("Ban Failed! Invalid Ban Credentials!");
            }
        }


        /// <summary>
        /// Sends a message to the specific Twitch channel, otherwise uses the currently connected channel.
        /// </summary>
        /// <param name="Message">The message to be sent.</param>
        /// <param name="Channel"></param>
        public void SendMessage(string Message, string? Channel = null)
        {
            if (twitchClient.IsConnected)
            {
                if (Message != null && Message != "")
                {
                    try
                    {
                        if (Channel == null)
                        {
                            Channel = GetBroadcaster().Login;
                        }
                        twitchClient.SendMessage(Channel, Message);
                    }
                    catch (Exception e)
                    {
                        Log("Failed to send message! Exception: " + e.Message);
                    }
                }
                else
                {
                    Spark.DebugLog("Failed to send message! Message is empty!");
                }
            }
            else
            {
                Log("Failed to send message! Twitch Client is not connected!");
            }
        }


        /// <summary>
        /// Bans the user that sent the most recent message in the chat.
        /// </summary>
        public void BanRecentUser()
        {
            BanUser(RecentUser, -1, "Banned by SPARK");
            PlaySound("BannedRecentUser.wav");
        }

        /// <summary>
        /// Adds a Twitch chat command.
        /// </summary>
        /// <param name="Command">The command to be ran.</param>
        /// <param name="ActionID">What action this command will call.</param>
        private void AddCommand(string Command, string ActionID)
        {
            Command = Command.ToLower();
            ActionID = ActionID.ToLower();
            chatCommands.Add(Command, ActionID);
        }


        /// <summary>
        /// Logs a message to the console box.
        /// </summary>
        /// <param name="Text">The text to log to console.</param>
        /// <param name="Force">Whether to ignore the enableLogging rule.</param>
        public void Log(string Text, bool Force = false)
        {
            Spark.Log($"{moduleName}: {Text}", Color.MediumPurple, Force);
        }

        /// <summary>
        /// Sends a warning to the console box.
        /// </summary>
        /// <param name="Text"></param>
        public void Warn(string Text)
        {
            Spark.Warn($"Warning from {moduleName}: {Text}");
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

                if (clientID == "" || clientID == null || clientSecret == "" || clientSecret == null)
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
                                WebServer.Stop();
                                WebServer.Dispose();
                                await API.Helix.Users.GetUsersAsync(accessToken: accessToken);
                                if (autoConnectClient)
                                {
                                    await ConnectClient();
                                }
                                if (autoConnectSubscriptionService)
                                {
                                    await StartService();
                                }
                            }
                        }
                    };

                    async void RequestTokens()
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
                    

                    ValidateAccessTokenResponse tokenResponse = await API.Auth.ValidateAccessTokenAsync(accessToken);

                    if (tokenResponse == null || oldScopes != scopes || accessToken == "")
                    {
                        if (oldScopes != scopes || accessToken == "")
                        {
                            RequestTokens();
                        }
                        else
                        {
                            try
                            {
                                RefreshResponse refreshResponse = await RefreshTokens();
                                if (refreshResponse != null)
                                {
                                    await API.Helix.Users.GetUsersAsync(accessToken: accessToken);
                                    if (autoConnectClient)
                                    {
                                        await ConnectClient();
                                    }
                                    if (autoConnectSubscriptionService)
                                    {
                                        await StartService();
                                    }
                                    Log("Access Token was refreshed!");
                                }
                                else
                                {
                                    Log("Error occurred while refreshing tokens!");
                                }
                            }
                            catch (Exception e)
                            {
                                Log("Exception Thrown: " + e.Message + " at: " + e.Source);
                                RequestTokens();
                            }
                        }
                    }
                    else
                    {
                        await API.Helix.Users.GetUsersAsync(accessToken: accessToken);
                        if (autoConnectClient)
                        {
                            await ConnectClient();
                        }
                        if (autoConnectSubscriptionService)
                        {
                            await StartService();
                        }
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
                Spark.DebugLog("User has already been excluded!");
            }
        }

        public void Disconnect(string[]? disconnects = null)
        { 
            if (disconnects != null)
            {
                foreach (string disconnect in disconnects)
                {
                    switch (disconnect.ToLower())
                    {
                        case "client":
                            DisconnectClient();
                            break;
                        case "eventsub":
                            DisconnectEventsub();
                            break;
                        case "server":
                            StopServer();
                            break;
                        default:
                            Spark.DebugLog("Unknown Disconnect Type: " + disconnect);
                            break;
                    }
                }
            }
            else
            {
                DisconnectEventsub();
            }
        }

        public void DisconnectClient()
        {
            if (twitchClient.IsConnected)
            {
                twitchClient.Disconnect();
                Log("Disconnected from Twitch Client!");
            }
            else
            {
                Spark.DebugLog("Twitch Client is already disconnected!");
            }
        }

        public async Task DisconnectEventsub()
        {
             await eventSub.StopAsync(TimeSpan.FromSeconds(5));
        }

        private void StopServer()
        {
            if (WebServer != null)
            {
                if (WebServer.State == HttpServerState.Started || WebServer.State == HttpServerState.Starting)
                {
                    Spark.DebugLog("Stopped Web Server!");
                    WebServer.Stop();
                    WebServer.Dispose();
                }
            }
        }

        private async Task StartService(string? service = null)
        {
            if (service == null)
            {
                service = subscriptionService;
            }

            switch (service.ToLower())
            {
                case "eventsub":
                    await eventSub.StartAsync();
                    return;
                case "pubsub":
                    PubSub.Start();
                    return;
                case "none":
                    Spark.DebugLog("No service specified to start!");
                    return;
            }
        }

        private async Task StopService(string? service = null)
        {
            if (service == null)
            {
                service = subscriptionService;
            }

            switch (service.ToLower())
            {
                case "eventsub":
                    await eventSub.StopAsync();
                    return;
                case "pubsub":
                    PubSub.Stop();
                    return;
            }
        }




        #endregion


        #region ReturnMethods

        public long CurrentViewers(string? Channel = null)
        {
            if (Channel == null)
            {
                Channel = GetBroadcaster().Id;
            }

            return GetUser(Channel).ViewCount;
        }

        /// <summary>
        /// Refreshes Twitch Tokens using the provided RefreshToken.
        /// </summary>
        /// <param name="RefreshToken"></param>
        /// <remarks>Returns Null on failure to refresh.</remarks>
        /// <returns>RefreshResponse</returns>
        private async Task<RefreshResponse> RefreshTokens(string? RefreshToken = null)
        {
            if (RefreshToken == null)
            {
                RefreshToken = refreshToken;
            }

            try
            {
                var response = await API.Auth.RefreshAuthTokenAsync(RefreshToken, clientSecret, clientID);
                accessToken = response.AccessToken;
                refreshToken = response.RefreshToken;

                return response;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the display name of a user from their userID.
        /// </summary>
        /// <param name="userID"></param>
        /// <remarks>Returns Null if user is invalid.</remarks>
        /// <returns>DisplayName String</returns>
        public string GetUserFromID(string userID)
        {
            return GetUser(userID, "id").DisplayName;
        }

        /// <summary>
        /// Retrieves a User from Helix.
        /// </summary>
        /// <param name="user">User to retrieve.</param>
        /// <param name="type">What to find user by. Accepted values: id, login</param>
        /// <remarks>Returns Null if no user is found.</remarks>
        /// <returns>User Object</returns>
        public User GetUser(string user, string type = "id")
        {
            user = user.ToLower();
            try
            {
                switch (type.ToLower())
                {
                    case "id":
                        {
                            var Users = API.Helix.Users.GetUsersAsync(ids: new List<string> { user }, accessToken: accessToken).Result.Users;
                            var Test = API.Helix.Streams.GetStreamsAsync(userLogins: new() { "fumetsuthe1" }, accessToken: accessToken).Result;
                            if (Users != null && Users.Length >= 1)
                            {
                                var User = Users[0];
                                return User;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    case "login":
                        {
                            var Users = API.Helix.Users.GetUsersAsync(logins: new List<string> { user }, accessToken: accessToken).Result.Users;
                            if (Users != null && Users.Length >= 1)
                            {
                                var User = Users[0];
                                return User;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    default:
                        {
                            Spark.DebugLog("Unknown GetUser type provided! Defaulting to: id");
                            var Users = API.Helix.Users.GetUsersAsync(ids: new List<string> { user }, accessToken: accessToken).Result.Users;
                            if (Users != null && Users.Length >= 1)
                            {
                                var User = Users[0];
                                return User;
                            }
                            else
                            {
                                return null;
                            }
                        }
                }
            }
            catch (Exception e)
            {
                Log($"Exception thrown from GetUsers: {e.Message} at: {e.Source}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves an array of Users from Helix.
        /// </summary>
        /// <param name="users">List of Users to retrieve.</param>
        /// <param name="type">What to find the users by. Accepted values: id, login</param>
        /// <remarks>Returns Null if no users are found.</remarks>
        /// <returns>User Array</returns>
        public User[] GetUsers(List<string> users, string type = "id")
        {
            try
            {
                switch (type.ToLower())
                {
                    case "id":
                        {
                            var Users = API.Helix.Users.GetUsersAsync(ids: users, accessToken: accessToken).Result.Users;
                            if (Users != null && Users.Length >= 1)
                            {
                                return Users;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    case "login":
                        {
                            var Users = API.Helix.Users.GetUsersAsync(logins: users, accessToken: accessToken).Result.Users;
                            if (Users != null && Users.Length >= 1)
                            {
                                return Users;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    default:
                        {
                            Spark.DebugLog("Unknown GetUser type provided! Defaulting to: id");
                            var Users = API.Helix.Users.GetUsersAsync(ids: users, accessToken: accessToken).Result.Users;
                            if (Users != null && Users.Length >= 1)
                            {
                                return Users;
                            }
                            else
                            {
                                return null;
                            }
                        }
                }
            }
            catch (Exception e)
            {
                Log($"Exception thrown from GetUsers: {e.Message} at: {e.Source}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves a user's ID via Username.
        /// </summary>
        /// <param name="user">User's Login</param>
        /// <remarks>Returns Null if user is invalid.</remarks>
        /// <returns>UserID String</returns>
        public string GetUserID(string user)
        {
            return GetUser(user.ToLower(), "login").Id;
        }

        /// <summary>
        /// Gets array of chatters.
        /// </summary>
        /// <returns>Chatters Array</returns>
        public Chatter[] GetChatters()
        {
            try
            {
                var users = API.Helix.Chat.GetChattersAsync(GetBroadcaster().Id, GetBroadcaster().Id, accessToken: accessToken).Result.Data;
                return users;
            }
            catch (Exception e)
            {
                Spark.DebugLog("Exception thrown when getting users: " + e.Message + " at: " + e.InnerException);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the broadcaster.
        /// </summary>
        /// <returns>Broadcaster Object</returns>
        public User GetBroadcaster()
        {
            try
            {
                var broadcaster = API.Helix.Users.GetUsersAsync(accessToken: accessToken).Result.Users[0];
                return broadcaster;
            }
            catch (Exception e)
            {
                Log($"Exception thrown while getting Broadcaster: {e.Message} at: {e.Source}");
                return null;
            }
        }

        public TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream GetStream(string userID, string? accessToken = null)
        {
            try
            {
                var Stream = API.Helix.Streams.GetStreamsAsync(userIds: new() { userID }, accessToken: accessToken).Result.Streams[0];
                return Stream;
            }
            catch (Exception e)
            {
                Log($"Exception thrown while Getting Stream: {e.Message} at: {e.Source}");
                return null;
            }
        }


        /// <summary>
        /// Retrieves Twitch Tokens from Twitch Servers.
        /// </summary>
        /// <param name="Code"></param>
        /// <returns>Tuple containing AccessToken and RefreshToken</returns>
        async Task<Tuple<string, string>> GetTokens(string Code)
        {
            try
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
            catch (Exception e)
            {
                Spark.DebugLog("Exception thrown when getting tokens: " + e.Message + " at: " + e.InnerException);
                return null;
            }
        }

        #endregion


        #region Events

        private async void ViewerLeft(object? sender, TwitchLib.Client.Events.OnUserLeftArgs e)
        {

        }

        private async void ViewerJoined(object? sender, TwitchLib.Client.Events.OnUserJoinedArgs e)
        {

        }

        private async void JoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            
        }

        private async void LeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            Log("Left Channel " + e.Channel);
        }

        private async void OnConnected()
        { 

        }

        #endregion


        #region AppHandling

        private void PlaySound(string Sound, int volume = 100)
        {
            Spark.PlaySound(Sound, Path.Combine(Classes.Spark.soundsPath, "Twitch"), volume);
        }

        /// <summary>
        /// Stores a viewer in the viewer log, ready for saving.
        /// </summary>
        /// <param name="Viewer"></param>
        private void StoreViewer(string Viewer)
        {
            if (!excludedUsers.Contains(Viewer))
            {
                EnsureLimit(viewerLogs, 9999, true);
                StoreLog(viewerLogs, " " + Spark.CurrentTime() + "  -  " + Viewer + " has joined the stream!");

                if (!whoVisited.Contains(Viewer))
                {
                    EnsureLimit(viewerLogs, 9999999, true);
                    StoreLog(whoVisited, Viewer, storeGlobally: false);
                }
            }
        }

        private async Task SaveData(bool ForceSave = false)
        {
            LoadLogList();


            string _accessToken = "";
            string _refreshToken = "";

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

            var date = DateTime.Now;
            string logPath = Path.Combine(twitchLogsPath, $"[{date.Year}.{date.Month}.{date.Day}]   [{date.Hour}.{date.Minute}.{date.Second}]");
            foreach (Tuple<string, List<string>> list in logList)
            {
                if (!IsEmpty(list.Item2) || ForceSave)
                {
                    await Spark.CreatePath(logPath);
                    string filePath = Path.Combine(logPath, list.Item1) + ".txt";
                    using var file = File.OpenWrite(filePath);

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
            logList.Add(new Tuple<string, List<string>>("Chat Commands", chatCommandLogs));
        }

        /// <summary>
        /// Checks if the provided list is empty.
        /// </summary>
        /// <param name="list">List to check.</param>
        /// <returns>Bool representing empty status.</returns>
        public static bool IsEmpty<T>(List<T> list)
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
        /// Checks if the provided dictionary is empty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dictionary">Dictionary to check.</param>
        /// <returns>Bool representing empty status.</returns>
        public static bool IsEmpty<T>(Dictionary<T, T> dictionary)
        {
            if (dictionary.Count <= 0)
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
                EnsureLimit(twitchLogs, 9999999, true);
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
            Spark.DebugLog("Performing Check!!");
            string[] words = scopes.Split(' ');
            string[] distinctWords = words.Distinct().ToArray();
            Array.Sort(distinctWords);
            string output = string.Join(' ', distinctWords);
            foreach (string scope in usedScopes)
            {
                Spark.DebugLog("Scope checked: " + scope);
                if (!words.Contains(scope))
                {
                    Spark.DebugLog("Warning! Not all Scope Requirements are met!");
                    break;
                }
            }
            foreach (string scope in words)
            {
                if (!usedScopes.Contains(scope) && !unneededScopes.Contains(scope))
                {
                    unneededScopes.Add(scope);
                    Spark.DebugLog($"Unneeded Scope: {scope}");
                }
            }

            return output;
        }

        public async Task DisposeTokens(string? AccessToken = null)
        {
            if (AccessToken == null)
            {
                AccessToken = accessToken;
            }

            try
            {
                HttpClient client = new HttpClient();
                Uri URL = new Uri(DirectURL);
                await client.PostAsync("https://id.twitch.tv/oauth2/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientID },
                    { "token", AccessToken }
                }));
                client.Dispose();
            }
            catch (Exception e)
            {
                Log($"Exception thrown while Disposing Tokens: {e.Message} at: {e.Source}");
            }
        }

        public async Task AppClosing()
        {
            await SaveData();
            StopServer();
            if (disposeTokens)
            {
                await DisposeTokens();
            }

            await DisconnectEventsub();
        }


        /// <summary>
        /// Adds a required scope to a list.
        /// </summary>
        /// <param name="type">Key containing all required scopes.</param>
        /// <param name="scopes">List of required scopes.</param>
        private void AddRequiredScope(string type, List<string> scopes)
        {
            if (!requiredScopes.ContainsKey(type))
            {
                requiredScopes.Add(type, scopes);
            }
            else
            {
                requiredScopes[type] = scopes;
                Spark.DebugLog("Twitch: Replacing old Required Scopes with new ones!");
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
            ExcludeUser("Sery_Bot");
            #endregion

            #region RequiredScopes
            AddRequiredScope("automod.message.hold", new() { "moderator:manage:automod" });
            AddRequiredScope("automod.message.update", new() { "moderator:manage:automod" });
            AddRequiredScope("automod.settings.update", new() { "moderator:read:automod_settings" });
            AddRequiredScope("automod.terms.update", new() { "moderator:manage:automod" });
            AddRequiredScope("channel.bits.use", new() { "bits:read" });
            AddRequiredScope("channel.follow", new() { "moderator:read:followers" });
            AddRequiredScope("channel.ad_break.begin", new() { "channel:read:ads" });
            AddRequiredScope("channel.chat.clear", new() { "channel:bot", "user:read:chat", "user:bot" });
            AddRequiredScope("channel.chat.clear_user_messages", new() { "channel:bot", "user:bot" });
            AddRequiredScope("channel.chat.message", new() { "user:read:chat", "user:bot", "channel:bot" });
            AddRequiredScope("channel.chat.notification", new() { "user:read:chat", "user:bot", "channel:bot" });
            AddRequiredScope("channel.chat_settings.update", new() { "user:read:chat", "user:bot", "channel:bot" });
            AddRequiredScope("channel.chat.user_message_hold", new() { "user:read:chat", "user:bot" });
            AddRequiredScope("channel.chat.user_message_update", new() { "user:read:chat", "user:bot" });
            AddRequiredScope("channel.subscribe", new() { "channel:read:subscriptions" });
            AddRequiredScope("channel.subscription.end", new() { "channel:read:subscriptions" });
            AddRequiredScope("channel.subscription.gift", new() { "channel:read:subscriptions" });
            AddRequiredScope("channel.subscription.message", new() { "channel:read:subscriptions" });
            AddRequiredScope("channel.cheer", new() { "bits:read" });
            AddRequiredScope("channel.ban", new() { "channel:moderate" });
            AddRequiredScope("channel.unban", new() { "channel:moderate" });
            AddRequiredScope("channel.unban_request.create", new() { "moderator:read:unban_requests" });
            AddRequiredScope("hannel.unban_request.resolve", new() { "moderator:read:unban_requests" });
            AddRequiredScope("channel.moderate", new() { "moderator:read:blocked_terms", "moderator:read:chat_settings", "moderator:read:unban_requests", "moderator:read:banned_users", "moderator:read:chat_messages", "moderator:read:warnings", "moderator:read:moderators", "moderator:read:vips" });
            AddRequiredScope("channel.moderator.add", new() { "moderation:read" });
            AddRequiredScope("channel.moderator.remove", new() { "moderation:read" });
            AddRequiredScope("channel.guest_star_session.begin", new() { "channel:read:guest_star" });
            AddRequiredScope("channel.guest_star_session.end", new() { "channel:read:guest_star" });
            AddRequiredScope("channel.guest_star_guest.update", new() { "channel:read:guest_star" });
            AddRequiredScope("channel.guest_star_settings.update", new() { "channel:read:guest_star" });
            AddRequiredScope("channel.channel_points_automatic_reward_redemption.add", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.channel_points_custom_reward.add", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.channel_points_custom_reward.update", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.channel_points_custom_reward.remove", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.channel_points_custom_reward_redemption.add", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.channel_points_custom_reward_redemption.update", new() { "channel:read:redemptions" });
            AddRequiredScope("channel.poll.begin", new() { "channel:read:polls" });
            AddRequiredScope("channel.poll.progress", new() { "channel:read:polls" });
            AddRequiredScope("channel.poll.end", new() { "channel:read:polls" });
            AddRequiredScope("channel.prediction.begin", new() { "channel:read:predictions" });
            AddRequiredScope("channel.prediction.progress", new() { "channel:read:predictions" });
            AddRequiredScope("channel.prediction.lock", new() { "channel:read:predictions" });
            AddRequiredScope("channel.poll.end", new() { "channel:read:predictions" });
            AddRequiredScope("channel.suspicious_user.message", new() { "moderator:read:suspicious_users" });
            AddRequiredScope("channel.suspicious_user.update", new() { "moderator:read:suspicious_users" });
            AddRequiredScope("channel.vip.add", new() { "channel:read:vips" });
            AddRequiredScope("channel.vip.remove", new() { "channel:read:vips" });
            AddRequiredScope("channel.warning.acknowledge", new() { "moderator:read:warnings" });
            AddRequiredScope("channel.warning.send", new() { "moderator:read:warnings" });
            AddRequiredScope("channel.charity_campaign.donate", new() { "channel:read:charity" });
            AddRequiredScope("channel.charity_campaign.start", new() { "channel:read:charity" });
            AddRequiredScope("channel.charity_campaign.progress", new() { "channel:read:charity" });
            AddRequiredScope("channel.charity_campaign.stop", new() { "channel:read:charity" });
            AddRequiredScope("channel.goal.begin", new() { "channel:read:goals" });
            AddRequiredScope("channel.goal.progress", new() { "channel:read:goals" });
            AddRequiredScope("channel.goal.end", new() { "channel:read:goals" });
            AddRequiredScope("channel.hype_train.begin", new() { "channel:read:hype_train" });
            AddRequiredScope("channel.hype_train.begin", new() { "channel:read:hype_train" });
            AddRequiredScope("channel.hype_train.progress", new() { "channel:read:hype_train" });
            AddRequiredScope("channel.hype_train.end", new() { "channel:read:hype_train" });
            AddRequiredScope("channel.shield_mode.begin", new() { "moderator:read:shield_mode" });
            AddRequiredScope("channel.shield_mode.end", new() { "moderator:read:shield_mode" });
            AddRequiredScope("channel.shoutout.create", new() { "moderator:read:shoutouts" });
            AddRequiredScope("channel.shoutout.receive", new() { "moderator:read:shoutouts" });
            AddRequiredScope("user.whisper.message", new() { "user:read:whispers" });
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
                    Spark.DebugLog("Client Data Invalid!");
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
                    Spark.DebugLog("Token Data Invalid!");
                }
            }
            else
            {
                await Spark.CreateFile(tokenPath);
                Spark.DebugLog("Token Data Missing!");
            }
        }

        #endregion



        #region Services
        public class EventSubService : IHostedService
        {
            bool connected = false;

            private readonly EventSubWebsocketClient _eventSubWebsocketClient;
            private readonly TwitchAPI _twitchAPI = Classes.Twitch.API;
            private string _userId;

            Twitch Twitch = Classes.Twitch;
            Spark Spark = Classes.Spark;

            public int subscriptionCount { get; private set; } = 0;


            #region Events

            #region Stream
            private async Task OnChannelRaid(object? sender, ChannelRaidArgs e)
            {
                bool stop = true;
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.ToBroadcasterUserName;
                string raider = eventData.FromBroadcasterUserName;
                int viewers = eventData.Viewers;

                Twitch.Log($"{raider} is raiding with {viewers} viewers!");
                Twitch.StoreLog(Twitch.streamLogs, $"{raider} is raiding with {viewers} viewers!");
            }

            private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
            {
                bool stop = true;
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.UserName} just followed!");
                Twitch.StoreLog(Twitch.streamLogs, $"{eventData.UserName} just followed!");
            }


            private async Task OnShoutoutReceived(object? sender, ChannelShoutoutReceiveArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.BroadcasterUserName;
                string receiver = eventData.ToBroadcasterUserId;

                Twitch.Log($"You have received a shoutout from {user}");
                Twitch.StoreLog(Twitch.streamLogs, $"You have received a shoutout from {user}");
            }

            private async Task ShoutoutGiven(object? sender, ChannelShoutoutCreateArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.ModeratorUserName;
                string receiver = eventData.ToBroadcasterUserName;

                Twitch.Log($"{eventData.ModeratorUserName} gave a shoutout to {eventData.ToBroadcasterUserName}");
                Twitch.StoreLog(Twitch.streamLogs, $"{eventData.ModeratorUserName} gave a shoutout to {eventData.ToBroadcasterUserName}");
            }


            private async Task OnStreamOnline(object? sender, StreamOnlineArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.BroadcasterUserName} is now live!");
                Twitch.StoreLog(Twitch.streamLogs, $"You have started streaming at {Spark.CurrentTime()}");
            }

            private async Task OnStreamOffline(object? sender, StreamOfflineArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string broadcaster = eventData.BroadcasterUserName;

                Twitch.Log($"{eventData.BroadcasterUserName} is no longer live!");
                Twitch.StoreLog(Twitch.streamLogs, $"You have stopped streaming at {Spark.CurrentTime()}");
            }

            private async Task OnChannelUpdate(object? sender, ChannelUpdateArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string title = eventData.Title;
                string category = eventData.CategoryName;

                Twitch.Log($"The channel has been updated with title ({title}) and category ({category})");
                Twitch.StoreLog(Twitch.streamLogs, $"The channel has been updated with title ({title}) and category ({category})");
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
                        Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has started a new follower goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        break;
                    case "subscriptions":
                        Twitch.Log($"{eventData.BroadcasterUserName} has started a new subscriber goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
                        Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has started a new subscriber goal: {eventData.CurrentAmount} / {eventData.TargetAmount}");
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
                        Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has completed a follower goal: {currentamount} / {goal}");
                        break;
                    case "subscriptions":
                        Twitch.Log($"{eventData.BroadcasterUserName} has completed a new subscriber goal: {currentamount} / {goal}");
                        Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has completed a subscriber goal: {currentamount} / {goal}");
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
                            Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has ended a follower goal: {currentamount} / {goal}");
                            break;
                        case "subscriptions":
                            Twitch.Log($"{eventData.BroadcasterUserName} has ended a subscriber goal: {currentamount} / {goal}");
                            Twitch.StoreLog(Twitch.streamLogs, $"{eventData.BroadcasterUserName} has ended a subscriber goal: {currentamount} / {goal}");
                            break;
                    }
                }
            }


            private async Task HypeTrainBegin(object? sender, ChannelHypeTrainBeginArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;

                Twitch.Log($"A hype train has started at {level}!");
                Twitch.StoreLog(Twitch.streamLogs, $"A hype train has started!");
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
                Twitch.StoreLog(Twitch.streamLogs, $"The hype train has progressed to level {level}!");
            }

            private async Task HypeTrainEnd(object? sender, ChannelHypeTrainEndArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                int level = eventData.Level;
                int total = eventData.Total;

                Twitch.Log($"A hype train has ended at level {level} with {total} total points!");
                Twitch.StoreLog(Twitch.streamLogs, $"A hype train has ended at level {level} with {total} total points!");
            }


            private async Task ChannelVipAdded(object? sender, ChannelVipArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been given VIP status!");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has been given VIP status!");
            }

            private async Task ChannelVipRemoved(object? sender, ChannelVipArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;

                Twitch.Log($"{user} has been revoked VIP status!");
                Twitch.StoreLog(Twitch.streamLogs, $"{user} has been revoked VIP status!");
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


            #region Viewers
            private async Task ChannelPointRedemption(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
            {
                bool stop = true;
                var eventData = e.Notification.Payload.Event;
                string reward = eventData.Reward.Title;
                string user = eventData.UserName;

                if (!stop)
                {
                    Twitch.SendMessage(user + " Redeemed " + reward + " for " + eventData.Reward.Cost + " Poinks!");
                }
                Twitch.Log($"{user} Redeemed {reward} for {eventData.Reward.Cost} Poinks!");
                Twitch.StoreLog(Twitch.channelPointLogs, $"{user} Redeemed {reward} for {eventData.Reward.Cost} Channel Points!");
            }

            private async Task MessageReceived(object? sender, ChannelChatMessageArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.ChatterUserName;
                string broadcaster = eventData.BroadcasterUserName;
                string message = eventData.Message.Text;
                string messageId = eventData.MessageId;

                if (message.Substring(0, Twitch.commandPrefix.Length) == Twitch.commandPrefix) { ChatCommandReceived(e); }
                else
                {
                    Spark.Log(eventData.ChatterUserName + ": " + eventData.Message.Text, Color.MediumPurple);
                    Twitch.StoreLog(Twitch.messageLogs, $"{eventData.ChatterUserName}: {eventData.Message.Text}");
                    Twitch.PlaySound("MessageReceived.mp3", 85);
                }
            }

            private bool ChatCommandReceived(ChannelChatMessageArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string casedCommand = eventData.Message.Text.Substring(Twitch.commandPrefix.Length);
                string command = casedCommand.ToLower();
                string user = eventData.ChatterUserName;
                string broadcaster = eventData.BroadcasterUserName;

                Spark.DebugLog("Command received!" + "   " + eventData.Message.Text);
                Twitch.StoreLog(Twitch.chatCommandLogs, $"[{Spark.CurrentTime()}]  {user} used command: {casedCommand}");

                if (Twitch.chatCommands.ContainsKey(command))
                {
                    return true;
                }
                else
                {
                    return false;
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
                        Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been banned by {moderator} for {reason}");
                    }
                    else
                    {
                        Twitch.Log($"{user} has been banned by {moderator}");
                        Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been banned by {moderator}");
                    }
                }
                else
                {
                    if (eventData.Reason != "")
                    {
                        Twitch.Log($"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds for {reason}!");
                        Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds for {reason}!");
                    }
                    else
                    {
                        Twitch.Log($"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds!");
                        Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been timed out by {moderator} for {eventData.EndsAt.Value.Second - eventData.BannedAt.Second} seconds!");
                    }
                }
            }

            private async Task OnUserUnbanned(object? sender, ChannelUnbanArgs e)
            {
                var eventData = e.Notification.Payload.Event;
                string user = eventData.UserName;
                string moderator = eventData.ModeratorUserName;

                Twitch.Log($"{user} has been unbanned by {moderator}");
                Twitch.StoreLog(Twitch.moderationLogs, $"{user} has been unbanned by {moderator}");
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
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} just Tier 1 subscribed!");
                        break;
                    case "2000":
                        Twitch.Log($"{user} just Tier 2 subscribed!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} just Tier 2 subscribed!");
                        break;
                    case "3000":
                        Twitch.Log($"{user} just Tier 3 subscribed!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} just Tier 3 subscribed!");
                        break;
                    default:
                        Twitch.Log($"{user} is a mysterious subscriber!");
                        Twitch.StoreLog(Twitch.streamLogs, $"{user} is a mysterious subscriber!");
                        break;
                }
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
            public EventSubService(EventSubWebsocketClient eventSubWebsocketClient)
            {
                _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));

                _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
                _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;
                _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
                _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;

                _twitchAPI.Settings.ClientId = Twitch.clientID;
                _twitchAPI.Settings.AccessToken = Twitch.accessToken;
                _twitchAPI.Settings.Secret = Twitch.clientSecret;


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


                #region Viewers
                _eventSubWebsocketClient.ChannelChatMessage += MessageReceived;
                _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += ChannelPointRedemption;
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

                if (!e.IsRequestedReconnect && connected == false)
                {
                    connected = true;
                    var condition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "moderator_user_id", _userId } };
                    var broadcasterCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId } };
                    var messageCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "user_id", _userId } };
                    var raidCondition = new Dictionary<string, string> { { "to_broadcaster_user_id", _userId } };

                    #region Subscriptions

                    #region Stream
                    AddSubscription("channel.follow", 2, condition);
                    AddSubscription("channel.raid", 1, raidCondition);
                    #endregion


                    #region Viewers
                    AddSubscription("channel.chat.message", 1, messageCondition);
                    #endregion


                    #region Moderation
                    #endregion


                    #region Earnings
                    #endregion
                    await AddSubscriptionBatch(new() {"ban", "vip", "subs", "bits", "hype_train", "stream", "goals", "mod", "predictions", "shoutout", "channel_points"});
                    #endregion

                    Twitch.ValidateScopes();
                }
            }


            /// <summary>
            /// Adds an EventSub subscription.
            /// </summary>
            /// <param name="type">What event to listen for.</param>
            /// <param name="version">What event version to use.</param>
            public async Task AddSubscription(string type, int version, Dictionary<string, string> condition, string? accessToken = null, string? clientID = null)
            {
                string versionString = version.ToString();

                await AddSubscription(type, versionString, condition, accessToken, clientID);
            }

            /// <summary>
            /// Adds an EventSub subscription.
            /// </summary>
            /// <param name="type">What event to listen for.</param>
            /// <param name="version">What event version to use.</param>
            public async Task AddSubscription(string type, string version, Dictionary<string, string> condition, string? accessToken = null, string? clientID = null)
            {
                if (accessToken == null)
                {
                    accessToken = Twitch.accessToken;
                }
                if (clientID == null)
                {
                    clientID = Twitch.clientID;
                }

                await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(type, version, condition, EventSubTransportMethod.Websocket,
                         _eventSubWebsocketClient.SessionId, accessToken: accessToken);

                if (Classes.Twitch.requiredScopes.ContainsKey(type))
                {
                    foreach (string scope in Twitch.requiredScopes[type])
                    {
                        if (!Classes.Twitch.usedScopes.Contains(type))
                        {
                            Spark.DebugLog("Added Used Scope!");
                            Classes.Twitch.usedScopes.Add(scope);
                        }
                    }
                }
                subscriptionCount++;
            }

            /// <summary>
            /// Subscribes to a batch of EventSub subscriptions based on the provided types.
            /// </summary>
            /// <param name="types">List of the batch types to subscribe to.</param>
            public async Task AddSubscriptionBatch(List<string> types, string? accessToken = null, string? clientID = null)
            {
                var condition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "moderator_user_id", _userId } };
                var broadcasterCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId } };
                var messageCondition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }, { "user_id", _userId } };
                var raidCondition = new Dictionary<string, string> { { "to_broadcaster_user_id", _userId } };

                foreach(string type in types)
                {
                    switch (type.ToLower())
                    {
                        case "message":
                            await AddSubscription("channel.chat.message", 1, messageCondition, accessToken, clientID);
                            await AddSubscription("channel.chat.clear_user_messages", 1, messageCondition, accessToken, clientID);
                            await AddSubscription("channel.chat.message_delete", 1, messageCondition, accessToken, clientID);
                            await AddSubscription("channel.chat.notification", 1, messageCondition, accessToken, clientID);
                            break;
                        case "ban":
                            await AddSubscription("channel.ban", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.unban", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "hype_train":
                            await AddSubscription("channel.hype_train.begin", 2, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.hype_train.progress", 2, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.hype_train.end", 2, broadcasterCondition, accessToken, clientID);
                            break;
                        case "subs":
                            await AddSubscription("channel.subscribe", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.subscription.end", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.subscription.gift", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.subscription.message", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "goals":
                            await AddSubscription("channel.goal.begin", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.goal.progress", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.goal.end", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "predictions":
                            await AddSubscription("channel.prediction.begin", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.prediction.progress", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.prediction.lock", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.prediction.end", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "vip":
                            await AddSubscription("channel.vip.add", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.vip.remove", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "mod":
                            await AddSubscription("channel.moderator.add", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.moderator.remove", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "warning":
                            await AddSubscription("channel.warning.acknowledge", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.warning.send", 1, condition, accessToken, clientID);
                            break;
                        case "charity":
                            await AddSubscription("channel.charity_campaign.donate", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.charity_campaign.start", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.charity_campaign.donate", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.charity_campaign.stop", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "stream":
                            await AddSubscription("stream.online", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("stream.offline", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.update", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "shoutout":
                            await AddSubscription("channel.shoutout.create", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.shoutout.receive", 1, condition, accessToken, clientID);
                            break;
                        case "suspicious_user":
                            await AddSubscription("channel.suspicious_user.message", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.suspicious_user.update", 1, condition, accessToken, clientID);
                            break;
                        case "automod":
                            await AddSubscription("automod.message.hold", 2, condition, accessToken, clientID);
                            await AddSubscription("automod.message.update", 2, condition, accessToken, clientID);
                            await AddSubscription("automod.settings.update", 1, condition, accessToken, clientID);
                            await AddSubscription("automod.terms.update", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.chat.user_message_update", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.chat.user_message_hold", 1, condition, accessToken, clientID);
                            break;
                        case "bits":
                            await AddSubscription("channel.bits.use", 1, broadcasterCondition, accessToken ,clientID);
                            await AddSubscription("channel.cheer", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "channel_points":
                            await AddSubscription("channel.channel_points_automatic_reward_redemption.add", 2, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.channel_points_custom_reward.add", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.channel_points_custom_reward.update", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.channel_points_custom_reward.remove", 1, broadcasterCondition, accessToken ,clientID);
                            await AddSubscription("channel.channel_points_custom_reward_redemption.add", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.channel_points_custom_reward_redemption.update", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "guest_star":
                            await AddSubscription("channel.guest_star_session.begin", "beta", condition, accessToken, clientID);
                            await AddSubscription("channel.guest_star_session.end", "beta", condition, accessToken, clientID);
                            await AddSubscription("channel.guest_star_guest.update", "beta", condition, accessToken, clientID);
                            await AddSubscription("channel.guest_star_settings.update", "beta", condition, accessToken, clientID);
                            break;
                        case "shield_mode":
                            await AddSubscription("channel.shield_mode.begin", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.shield_mode.end", 1, condition, accessToken, clientID);
                            break;
                        case "shared_chat":
                            await AddSubscription("channel.shared_chat.begin", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.shared_chat.update", 1, broadcasterCondition, accessToken, clientID);
                            await AddSubscription("channel.shared_chat.end", 1, broadcasterCondition, accessToken, clientID);
                            break;
                        case "unban_request":
                            await AddSubscription("channel.unban_request.create", 1, condition, accessToken, clientID);
                            await AddSubscription("channel.unban_request.resolve", 1, condition, accessToken, clientID);
                            break;
                        default:
                            Spark.DebugLog($"Invalid Subscription Batch type!  ({type.ToLower()})");
                            break;
                    }
                }
            }

            /// <summary>
            /// Unsubscribes from an EventSub subscription.
            /// </summary>
            /// <param name="type">Event to unsubscribe from.</param>
            /// <returns>Bool representing deletion result.</returns>
            public async Task<bool> RemoveSubscription(string type, string? accessToken = null, string? clientID = null)
            {
                if (accessToken == null)
                {
                    accessToken = Twitch.accessToken;
                }
                if (clientID == null)
                {
                    clientID = Twitch.clientID;
                }

                return await _twitchAPI.Helix.EventSub.DeleteEventSubSubscriptionAsync(type, clientID, accessToken);
            }


            public async Task StartAsync(CancellationToken cancellationToken)
            {
                _eventSubWebsocketClient.ConnectAsync();
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                _eventSubWebsocketClient.DisconnectAsync();
            }


            private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
            {
                connected = false;
                int reconnectDelay = 1; // Delay before attempting to reconnect, in seconds.
                int delayIncrement = 1; // Amount to increase the reconnect delay by each attempt, In seconds.
                string incrementType = "add"; // Add, multiply, square.

                int maxRetries = 6; // Maximum number of retries before giving up.
                Twitch.Log("Websocket Disconnected!");

                int retryCount = 0;
                while (!await _eventSubWebsocketClient.ReconnectAsync() && retryCount < maxRetries)
                {
                    if ((retryCount + 1) >= maxRetries)
                    {
                        Twitch.Log($"Websocket failed to reconnect!");
                        retryCount++;
                    }
                    else
                    {
                        Twitch.Log($"Websocket failed to reconnect! Trying again in {reconnectDelay} seconds..");
                        await Task.Delay(reconnectDelay * 1000);
                        retryCount++;
                        Increment();
                    }
                }

                void Increment()
                {
                    string type = incrementType.ToLower();

                    switch(type)
                    {
                        case "add":
                            reconnectDelay += delayIncrement;
                            break;
                        case "multiply":
                            reconnectDelay *= delayIncrement;
                            break;
                        case "square":
                            reconnectDelay *= reconnectDelay;
                            break;
                        default:
                            Spark.DebugLog("Invalid increment type specified. Using addition by default.");
                            reconnectDelay += delayIncrement;
                            break;
                    }
                }
            }

            private async Task OnWebsocketReconnected(object? sender, EventArgs e)
            {
                connected = true;
                Twitch.Log("Websocket Reconnected!");
            }

            private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
            {
                Spark.DebugLog(e.Exception.Source);
                connected = false;
                Twitch.Log("An error has occured within the Websocket!");
            }
        }

        public class PubSubService
        {
            private static TwitchPubSub client;
            bool connected = false;

            Twitch Twitch = Classes.Twitch;


            // Add Subscriptions and Events here!
            public bool Start()
            {
                if (connected == false)
                {
                    try
                    {
                        client = new TwitchPubSub();
                        client.OnPubSubServiceConnected += Connected;
                        client.OnPubSubServiceClosed += Disconnected;
                        client.OnPubSubServiceError += ErrorOccurred;


                        #region Events
                        client.OnChannelPointsRewardRedeemed += ChannelPointRedemption;
                        #endregion


                        #region Subscriptions
                        client.ListenToChannelPoints(Classes.Twitch.GetBroadcaster().Id);
                        #endregion


                        client.Connect();
                        return true;
                    }
                    catch (Exception e)
                    {
                        Twitch.Warn($"Exception thrown while starting PubSub Service: {e.Message} at: {e.Source}");
                        return false;
                    }
                }
                else
                {
                    Twitch.Log("PubSub Service is already connected!");
                    return false;
                }
            }


            public void Stop()
            {
                try
                {
                    client.Disconnect();

                    client = null;
                }
                catch (Exception e)
                {
                    Twitch.Warn($"Exception thrown while disconnecting from PubSub Service: {e.Message} at: {e.Source}");
                }
            }


            #region Events
            private void ChannelPointRedemption(object? sender, OnChannelPointsRewardRedeemedArgs e)
            {
                var eventData = e.RewardRedeemed.Redemption;

                Twitch.Log($"{eventData.User} Redeemed {eventData.Reward}!");
            }
            #endregion
            

            private void Connected(object? sender, EventArgs e)
            {
                connected = true;
                Classes.Twitch.Log("PubSub Service Connected!");
            }

            private void Disconnected(object? sender, EventArgs e)
            {
                connected = false;
                Classes.Twitch.Log("PubSub Service Disconnected!");
            }

            private void ErrorOccurred(object? sender, EventArgs e)
            {
                connected = false;
                Classes.Twitch.Log("Error occurred within PubSub Service!");
            }
        }

        public class Client
        {
            public SettingsData Settings { get; } = new();
            public EventClass Events { get; }

            public Client()
            {
                Events = new(this);
            }

            public TwitchClient twitchClient { get; } = new();

            public string? User { get; private set; } = null;

            /// <summary>
            /// Connects the Twitch Client to Twitch.
            /// </summary>
            /// <param name="channel">The channel to connect to.</param>
            /// <param name="accessToken">The User's Access Token.</param>
            /// <param name="autoJoin">Whether or not to join the channel automatically after connection completion.</param>
            /// <returns>Bool representing whether the connection attempt was successful.</returns>
            public async Task Start(string? channel = null, string? accessToken = null)
            {
                if (!twitchClient.IsConnected)
                {
                    try
                    {
                        Settings.AccessToken ??= Classes.Twitch.accessToken;
                        accessToken ??= Settings.AccessToken;
                        if (channel == null)
                        {
                            Settings.Channel ??= Classes.Twitch.GetBroadcaster().Login;
                            channel = Settings.Channel;
                        }
                        else
                        {
                            channel = channel.ToLower();
                        }

                        Classes.Spark.DebugLog("Used Token: -  " + accessToken);
                        ConnectionCredentials Credentials = new ConnectionCredentials(channel, accessToken);


                        twitchClient.Initialize(Credentials);

                        twitchClient.OnConnected += Events.Connected;

                        twitchClient.Connect();

                        Classes.Twitch.ExcludeUser(Classes.Twitch.GetUserFromID(Classes.Twitch.GetBroadcaster().Id));
                        Classes.Twitch.Log("Connected To Twitch!");
                        if (Settings.AutoJoin == true)
                        {
                            JoinChannel(channel);
                        }
                        User = channel;
                        Events.ConnectionFinished();
                    }
                    catch (Exception e)
                    {
                        Classes.Twitch.Log($"Exception thrown while starting Twitch Client ({User}): {e.Message} at: {e.Source}");
                    }
                }
                else
                {
                    Classes.Twitch.Log($"Failed to Connect Twitch Client ({User}): Client is already Connected!");
                }
            }

            /// <summary>
            /// Disconnects the Twitch Client from Twitch.
            /// </summary>
            public void Stop()
            {
                if (!twitchClient.IsConnected)
                {
                    twitchClient.Disconnect();
                }
                else
                {
                    Classes.Twitch.Log($"Failed to Disconnect Twitch Client ({User}): Client is not Connected!");
                }
            }

            /// <summary>
            /// Joins the specified Channel.
            /// </summary>
            /// <param name="Channel">The Channel Username to join.</param>
            private void JoinChannel(string? Channel = null)
            {
                if (twitchClient.IsConnected)
                {
                    try
                    {
                        if (Channel == null)
                        {
                            Channel = Classes.Twitch.GetBroadcaster().Login;
                        }
                        else
                        {
                            Channel = Channel.ToLower();
                        }

                        twitchClient.JoinChannel(Channel);
                    }
                    catch (Exception e)
                    {
                        Classes.Twitch.Log($"Exception thrown while Joining Channel: {e.Message} at: {e.Source}");
                    }
                }
                else
                {
                    Classes.Twitch.Log($"Failed to Join Channel {Channel}: Twitch Client ({User}) is not connected!)");
                }
            }

            internal void ConnectEvents()
            {
                twitchClient.OnJoinedChannel += Events.JoinedChannel;
                twitchClient.OnLeftChannel += Events.LeftChannel;
                twitchClient.OnUserJoined += Events.ViewerJoined;
                twitchClient.OnUserLeft += Events.ViewerLeft;
            }

            internal void DisconnectEvents()
            {
                twitchClient.OnJoinedChannel -= Events.JoinedChannel;
                twitchClient.OnLeftChannel -= Events.LeftChannel;
                twitchClient.OnUserJoined -= Events.ViewerJoined;
                twitchClient.OnUserLeft -= Events.ViewerLeft;
            }

            #region Events
            public class EventClass
            {
                private readonly Client _client;

                public EventClass(Client client)
                {
                    _client = client;
                }

                internal void ConnectionFinished()
                {
                    
                }

                internal void Connected(object? sender, OnConnectedArgs e)
                {
                    Classes.Twitch.Log($"Twitch Client ({_client.Settings.Channel}) Connected!");

                    _client.ConnectEvents();
                }

                internal void Disconnected(object? sender, OnDisconnectedEventArgs e)
                {
                    Classes.Twitch.Log($"Twitch Client ({_client.Settings.Channel}) Disconnected!");

                    _client.DisconnectEvents();
                }

                internal void ConnectionError(object? sender, OnConnectionErrorArgs e)
                {
                    Classes.Twitch.Log($"Twitch Client ({_client.Settings.Channel}) Connection Error: {e.Error}");
                }

                internal async void JoinedChannel(object? sender, OnJoinedChannelArgs e)
                {
                    Classes.Twitch.Log($"Joined Channel: {e.Channel}");
                }

                internal async void LeftChannel(object? sender, OnLeftChannelArgs e)
                {
                    Classes.Twitch.Log($"Left Channel: {e.Channel}");
                }

                internal async void ViewerJoined(object? sender, OnUserJoinedArgs e)
                {
                    Classes.Twitch.currentViewers = +1;
                    string Viewer = e.Username;
                    Classes.Twitch.Log("Viewer Joined: " + Viewer);
                    if (!Classes.Twitch.excludedUsers.Contains(Viewer))
                    {
                        Classes.Twitch.StoreLog(Classes.Twitch.viewerLogs, " " + Classes.Spark.CurrentTime() + "  -  " + Viewer + " has joined the stream!");
                    }
                }

                internal async void ViewerLeft(object? sender, OnUserLeftArgs e)
                {
                    Classes.Twitch.currentViewers = -1;
                    string Viewer = e.Username;
                    Classes.Twitch.Log("Viewer Left: " + Viewer);
                    if (!Classes.Twitch.excludedUsers.Contains(Viewer))
                    {
                        Classes.Twitch.StoreLog(Classes.Twitch.viewerLogs, " " + Classes.Spark.CurrentTime() + "  -  " + Viewer + " has left the stream!");
                    }
                }
            }


            #endregion

            public class SettingsData
            {
                public string? Channel { get; set; } = null;
                public string? AccessToken { get; set; } = null;

                /// <summary>
                /// Automatically joins the channel after connection completion.
                /// </summary>
                public bool AutoJoin { get; set; } = true;
            }
        }
        #endregion


        public class TriggerConditions
        {
            public class ChannelFollowCondition
            {
                public string Broadcaster_User_ID { get; set; }
                public string Moderator_User_ID { get; set; }
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
