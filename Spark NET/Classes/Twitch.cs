using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Models;
using TwitchLib.Client;
using WinFormsApp1;
using WinFormsApp1.Classes;
using WinFormsApp1.Designs;
using System.Reflection.Metadata;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using System.Net;
using System.Diagnostics;
using static System.Net.WebRequestMethods;
using Microsoft.Win32;
using System.Net.Http.Json;
using NHttp;
using TwitchLib.Client.Extensions;
using TwitchLib.PubSub.Models.Responses.Messages.AutomodCaughtMessage;
using System.Text.Json.Serialization;
using NAudio.CoreAudioApi;
using System.Text.Json;
using System.Text.Json.Nodes;
using TwitchLib.Api.ThirdParty.AuthorizationFlow;
using TwitchLib.Client.Events;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Core.HttpCallHandlers;

// Fix Commands not working
// Add support for Connecting to Non-Broadcaster Channels
// Optimize and Refine everything, maybe implement Threading
// Add support for Same-Date Twitch Logs
// Add more Twitch Logging (Chat Commands, Unbans, Channel Points, Followers, Subs, Title Changes, Category Changes)
// Add Channel Bot Support?

namespace WinFormsApp1.Classes
{
    public class Twitch
    {
        bool logMessages = true;

        string DirectURL = "http://localhost:3000";

        string scopes = "clips:edit moderator:manage:banned_users moderator:read:banned_users user:bot moderator:read:chatters moderator:read:followers moderator:read:moderators moderation:read channel:moderate channel:bot chat:read chat:edit bits:read channel:read:redemptions channel:read:ads channel:read:editors user:write:chat";


        string channelName = "%null%";
        string channelID = "%null%";

        string botUsername = "SPARK_NET_BOT";
        string clientID = "%null%";
        string clientSecret = "%null%";

        string accessToken = "%null%";
        string refreshToken = "%null%";

        readonly MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        NHttp.HttpServer WebServer;
        Spark Spark = Classes.Spark;


        static string twitchPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Classes.Spark.dataPath), "Twitch");
        public string clientDataPath = Path.Combine(twitchPath, "Client.json");
        public string twitchLogsPath = Path.Combine(twitchPath, "Twitch Logs");

        List<string> streamLogs = new List<string>();
        List<string> twitchLogs = new List<string>();
        List<string> messageLogs = new List<string>();
        List<string> moderationLogs = new List<string>();
        List<string> channelPointLogs = new List<string>();
        List<string> viewerLogs = new List<string>();

        List<Tuple<string, List<string>>> logList = new List<Tuple<string, List<string>>>();

        Dictionary<string, string> chatCommands = new Dictionary<string, string>();
        string oldScopes = "%null%";
        string RecentUser = "%null%";

        TwitchClient twitchClient = new TwitchClient();
        public TwitchAPI API = new TwitchAPI();


        #region Methods

        public void CreateClip()
        {
            API.Settings.ClientId = clientID;
            API.Settings.AccessToken = accessToken;
            var Clip = API.Helix.Clips.CreateClipAsync(GetBroadcasterID(), accessToken);
            Log("Clip Created!");
        }

        public void JoinChannel(string? Channel = null)
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

        public void LeaveChannel(string? Channel = null)
        {
            if (Channel == null)
            {
                Channel = GetChannel();
            }
            twitchClient.LeaveChannel(Channel);
        }



        public void SendAnnouncement(string Message, string? Channel = null)
        {
            if (Channel == null)
            {
                Channel = GetChannel();
            }
            twitchClient.Announce(Channel, Message);
        }

        public void FollowersOnly(bool Value, string? Channel = null)
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
        public async Task BanUser(string userID, int duration = -1)
        {
            API.Settings.AccessToken = accessToken;
            API.Settings.ClientId = clientID;
            try
            {
                await API.Helix.Moderation.BanUserAsync(
                GetBroadcasterID(),                 // broadcasterId: *which* channel to ban the user from; hope you're a mod there!
                GetBroadcasterID(),                 // moderatorId: *who* is running the ban. This should be the same as the broadcasterId
                new TwitchLib.Api.Helix.Models.Moderation.BanUser.BanUserRequest
                {
                    UserId = userID,    // *who* is getting banned
                    Reason = "test",    // (optional) *why* are they getting banned
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


        /// <summary>
        /// Sends a message to the specific Twitch channel, otherwise uses the currently connected channel.
        /// </summary>
        /// <param name="Message"></param>
        public void SendMessage(string Message, string? Channel = null)
        {
            if (Channel == null)
            {
                Channel = channelName;
            }
            Spark.DebugLog(channelName);
            twitchClient.SendMessage(channelName, Message);
        }


        /// <summary>
        /// Bans the most recent user that sent a message in the chat.
        /// </summary>
        public void BanRecentUser()
        {
            BanUser(RecentUser);
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
                            }
                        }
                    };
                    API.Settings.AccessToken = accessToken;
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
                    }
                }
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
            API.Settings.AccessToken = accessToken;
            API.Settings.ClientId = clientID;
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
            Spark.DebugLog(accessToken);
            client.CancelPendingRequests();
            client.Dispose();
            return new Tuple<string, string>(Json["access_token"].ToString(), Json["refresh_token"].ToString());
        }

        #endregion


        #region Events

        private void RaidDetected(object? sender, TwitchLib.Client.Events.OnRaidNotificationArgs e)
        {
            string raider = e.RaidNotification.DisplayName;
            string viewerCount = e.RaidNotification.MsgParamViewerCount;
            Log(raider + " is raiding with " + viewerCount + " viewers!");
            StoreLog(streamLogs, " " + Spark.GetCurrentTime() + "  -  " + raider + " is raiding with " + viewerCount + " viewers!");
        }

        private void ViewerLeft(object? sender, TwitchLib.Client.Events.OnUserLeftArgs e)
        {
            string Viewer = e.Username;
            StoreLog(viewerLogs, " " + Spark.GetCurrentTime() + "  -  " + Viewer + " has left the stream!");
            Log("Viewer Left: " + Viewer);
        }

        private void ViewerJoined(object? sender, TwitchLib.Client.Events.OnUserJoinedArgs e)
        {
            string Viewer = e.Username;
            StoreViewer(Viewer);
            StoreLog(streamLogs, " " + Spark.GetCurrentTime() + "  -  " + Viewer + " joined the stream!");
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

        private void MessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            RecentUser = e.ChatMessage.UserId;
            TwitchLib.Client.Models.ChatMessage Message = e.ChatMessage;
            Spark.DebugLog(Message.Message);
            if (logMessages)
            {
                Spark.Log(Message.DisplayName + ": " + Message.Message, Color.MediumPurple);
            }
            string String = " " + Spark.GetCurrentTime() + "  -  " + Message.DisplayName + ": " + Message.Message;
            StoreLog(messageLogs, String);
        }

        private void UserBanned(object? sender, TwitchLib.Client.Events.OnUserBannedArgs e)
        {
            string viewer = e.UserBan.Username;
            string banReason = e.UserBan.BanReason;
            StoreLog(moderationLogs, " " + Spark.GetCurrentTime() + "  -  " + viewer + " has been banned for " + banReason);
            Log("User Banned: " + viewer);
        }

        private void UserTimedOut(object? sender, TwitchLib.Client.Events.OnUserTimedoutArgs e)
        {
            string viewer = e.UserTimeout.Username;
            int timeoutDuration = e.UserTimeout.TimeoutDuration;
            string timeoutReason = e.UserTimeout.TimeoutReason;
            StoreLog(moderationLogs, viewer + " has been timed out for " + timeoutDuration + " seconds for " + timeoutReason);
            Log("User Timed Out: " + viewer);
        }

        #endregion


        #region AppHandling

        private void ConnectEvents()
        {
            twitchClient.OnUserBanned += UserBanned;
            twitchClient.OnRaidNotification += RaidDetected;
            twitchClient.OnJoinedChannel += JoinedChannel;
            twitchClient.OnUserJoined += ViewerJoined;
            twitchClient.OnUserLeft += ViewerLeft;
            twitchClient.OnMessageReceived += MessageReceived;
            twitchClient.OnUserTimedout += UserTimedOut;
        }

        /// <summary>
        /// Stores a viewer in the viewer log, ready for saving.
        /// </summary>
        /// <param name="Viewer"></param>
        private void StoreViewer(string Viewer)
        {
            if (!viewerLogs.Contains(Viewer))
            {
                EnsureLimit(viewerLogs, 5000, true);
                viewerLogs.Add(Viewer);
            }
        }


        // Add Support for Same-Date Message Logs
        private async Task OldSaveData(bool ForceSave = false)
        {
            var data = new List<ClientData>
            {
                new ClientData
                {
                    Client_ID = clientID,
                    Client_Secret = clientSecret,

                    Refresh_Token = refreshToken,
                    Access_Token = accessToken,
                    Scopes = scopes
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            string jsonString = JsonSerializer.Serialize(data, options);

            await using (StreamWriter swc = new StreamWriter(clientDataPath))
            {
                await swc.WriteLineAsync(jsonString);
            }

            int dataCount = messageLogs.Count + viewerLogs.Count + twitchLogs.Count + moderationLogs.Count + channelPointLogs.Count + streamLogs.Count;
            if (ForceSave || dataCount > 0)
            {
                string logPath = twitchLogsPath + @"\" + DateTime.Now.Day + ";" + DateTime.Now.Month + ";" + DateTime.Now.Year;
                await Spark.CreatePath(logPath);

                if (!IsEmpty(twitchLogs) || ForceSave && twitchLogs.Count >= 1)
                {
                    viewerLogs.Sort();
                    using var viewerfile = System.IO.File.OpenWrite(logPath + @"\Viewers.txt");
                    await using (StreamWriter swv = new StreamWriter(viewerfile))
                    {
                        foreach (string Viewer in viewerLogs)
                        {
                            await swv.WriteLineAsync(Viewer);
                        }
                    }
                }

                if (viewerLogs.Count >= 1 || ForceSave && viewerLogs.Count >= 1)
                {
                    viewerLogs.Sort();
                    using var viewerfile = System.IO.File.OpenWrite(logPath + @"\Viewers.txt");
                    await using (StreamWriter swv = new StreamWriter(viewerfile))
                    {
                        foreach (string Viewer in viewerLogs)
                        {
                            await swv.WriteLineAsync(Viewer);
                        }
                    }
                }

                if (logMessages && messageLogs.Count >= 1 || ForceSave && messageLogs.Count >= 1)
                {
                    string FilePath = twitchLogsPath + @"\ChatLog   " + DateTime.Now.Day + ";" + DateTime.Now.Month + ";" + DateTime.Now.Year + ".txt";
                    using var messagefile = System.IO.File.OpenWrite(logPath + @"\Messages.txt");
                    await using (StreamWriter swm = new StreamWriter(messagefile))
                    {
                        foreach (string Message in messageLogs)
                        {
                            await swm.WriteLineAsync(Message);
                        }
                    }
                }
            }
        }

        private async Task SaveData(bool ForceSave = false)
        {
            LoadLogList();

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
            viewerLogs.Sort();
            logList.Add(new Tuple<string, List<string>>("Stream", streamLogs));
            logList.Add(new Tuple<string, List<string>>("Twitch Log", twitchLogs));
            logList.Add(new Tuple<string, List<string>>("Messages", messageLogs));
            logList.Add(new Tuple<string, List<string>>("Moderation", moderationLogs));
            logList.Add(new Tuple<string, List<string>>("Channel Points", channelPointLogs));
            logList.Add(new Tuple<string, List<string>>("Viewers", viewerLogs));
        }

        /// <summary>
        /// Checks if the provided list is empty.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public bool IsEmpty(List<string> list)
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
        public void StoreLog(List<string> list, string data, int dataLimit = 5000)
        {
            EnsureLimit(list, dataLimit, true);
            list.Add(data);
            twitchLogs.Add(data);
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



        public async Task AppClosing()
        {
            if (Spark.twitchConnection)
            {
                await SaveData();
            }
            if (WebServer.State == NHttp.HttpServerState.Started || WebServer.State == HttpServerState.Starting)
            {
                WebServer.Stop();
                WebServer.Dispose();
            }
        }


        private void BuildLibrary()
        {
            AddCommand("!say", "say");
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
            if (System.IO.File.Exists(clientDataPath))
            {
                var clientJson = System.IO.File.ReadAllText(clientDataPath);
                var Json = JsonSerializer.Deserialize<ClientData[]>(clientJson);

                oldScopes = Json[0].Scopes;
                accessToken = Json[0].Access_Token;
                refreshToken = Json[0].Refresh_Token;
                clientID = Json[0].Client_ID;
                clientSecret = Json[0].Client_Secret;
            }
        }

        #endregion




        public class ClientData
        {
            public string Client_ID { get; set; }
            public string Client_Secret { get; set; }
            public string Channel_Name { get; set; }

            public string Refresh_Token { get; set; }
            public string Access_Token { get; set; }

            public string Scopes { get; set; }
        }
    }
}
