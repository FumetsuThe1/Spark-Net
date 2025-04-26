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

// Fix Commands Not Working
// Add support for connecting to non-broadcaster channels
// Optimize everything, maybe implement threading

namespace WinFormsApp1.Classes
{
    public class Twitch
    {
        NHttp.HttpServer WebServer;
        readonly MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        Spark Spark = Classes.Spark;

        string BotUsername = "SPARK_NET_BOT";
        string clientID = "%null%";
        string clientSecret = "%null%";

        string accessToken = "%null%";
        string refreshToken = "%null%";

        bool LogMessages = true;

        string DirectURL = "http://localhost:3000";

        string Scopes = "moderator:manage:banned_users moderator:read:banned_users user:bot moderator:read:chatters moderator:read:followers moderator:read:moderators moderation:read channel:moderate channel:bot chat:read chat:edit bits:read channel:read:redemptions channel:read:ads channel:read:editors user:write:chat";

        string ChannelID = "%null%";
        string ChannelName = "fumetsuthe1";

        const string Browser = @"C:\Program Files\Mozilla Firefox\firefox.exe";

        Dictionary<string, string> ChatCommands = new Dictionary<string, string>();
        List<string> ChatMessages = new List<string>();
        static string TwitchPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Classes.Spark.DataPath), "Twitch");
        public string clientDataPath = Path.Combine(TwitchPath, "Client.json");
        string MessageLogPath = Path.Combine(TwitchPath, "MessageLogs");
        string OldScopes = "%null%";

        TwitchClient twitchClient = new TwitchClient();
        public TwitchAPI API = new TwitchAPI();

        string RecentUser = "%null%";


        public void JoinChannel(string? Channel = null)
        {
            if (Channel == null)
            {
                Channel = GetChannel();
                ChannelName = Channel;
                twitchClient.JoinChannel(Channel);
            }
            else
            {
                ChannelName = Channel;
                twitchClient.JoinChannel(Channel);
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
            if (LogMessages)
            {
                Spark.Log(Message.DisplayName + ": " + Message.Message, Color.MediumPurple);
            }
            string String = " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + " - " + Message.DisplayName + ": " + Message.Message;
            StoreMessage(String);
        }

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

        private void ConnectClient()
        {
            ConnectionCredentials Credentials = new ConnectionCredentials(BotUsername, accessToken);

            // Connected Events
            twitchClient.OnJoinedChannel += JoinedChannel;
            twitchClient.OnLeftChannel += LeftChannel;

            twitchClient.OnMessageReceived += MessageReceived;
            // // // // // //

            twitchClient.Initialize(Credentials);

            twitchClient.Connect();

            Log("Connected To Twitch!");
            JoinChannel();
        }

        /// <summary>
        /// Stores a message in the message log, ready for saving.
        /// </summary>
        /// <param name="Message"></param>
        private void StoreMessage(string Message)
        {
            int MessageLimit = 5000;
            if (ChatMessages.Count >= MessageLimit)
            {
                ChatMessages.RemoveRange(0, MessageLimit);
            }
            ChatMessages.Add(Message);
        }

        // Add Support for Same-Date Message Logs
        private async Task SaveData(bool ForceSave = false)
        {
            var data = new List<ClientData>
            {
                new ClientData
                {
                    Client_ID = clientID,
                    Client_Secret = clientSecret,

                    Refresh_Token = refreshToken,
                    Access_Token = accessToken,
                    Scopes = Scopes
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

            if (LogMessages && ChatMessages.Count >= 1 || ForceSave)
            {
                string FilePath = Path.Combine(MessageLogPath + @"\ChatLog   " + DateTime.Now.Day + ";" + DateTime.Now.Month + ";" + DateTime.Now.Year + ".txt");
                using var file = System.IO.File.OpenWrite(FilePath);
                await using (StreamWriter sw = new StreamWriter(file))
                {
                    foreach (string Message in ChatMessages)
                    {
                        await sw.WriteLineAsync(Message);
                    }
                }
            }
        }

        public string GetBroadcasterID()
        {
            API.Settings.AccessToken = accessToken;
            API.Settings.ClientId = clientID;
            var User = API.Helix.Users.GetUsersAsync();
            ChannelID = User.Result.Users[0].Id.ToString();
            return ChannelID;
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
                Channel = ChannelName;
            }
            Spark.DebugLog(ChannelName);
            twitchClient.SendMessage(ChannelName, Message);
        }

        /// <summary>
        /// Bans the most recent user that sent a message in the chat.
        /// </summary>
        public void BanRecent()
        {
            BanUser(RecentUser);
        }

        public async Task AppClosing()
        {
            if (Spark.TwitchConnection)
            {
                await SaveData();
            }
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

        private void BuildLibrary()
        {
            AddCommand("!say", "say");
        }

        private void AddCommand(string Command, string ActionID)
        {
            Command = Command.ToLower();
            ActionID = ActionID.ToLower();
            ChatCommands.Add(Command, ActionID);
        }

        private void CommandLibrary(string Command, string Parameter, string CasedParameter)
        {
            switch (Command)
            {
                case "say":
                    Spark.Say(CasedParameter); break;
            }
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
            ChatCommands.TryGetValue(Words.GetValue(0).ToString(), out string? String);


            Spark.DebugLog(String);

            if (ChatCommands.TryGetValue(Words.GetValue(0).ToString(), out string? _))
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

                OldScopes = Json[0].Scopes;
                accessToken = Json[0].Access_Token;
                refreshToken = Json[0].Refresh_Token;
                clientID = Json[0].Client_ID;
                clientSecret = Json[0].Client_Secret;
            }
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

        /// <summary>
        /// Loads the connection to Twitch.
        /// </summary>
        /// <param name="forceLoad"></param>
        /// <returns></returns>
        public async Task LoadConnection(bool forceLoad = false)
        {
            if (Spark.TwitchConnection || forceLoad == true)
            {
                API.Settings.Secret = clientSecret; API.Settings.AccessToken = accessToken; API.Settings.ClientId = clientID;

                if (clientID == "%null%" || clientID == null || clientSecret == "%null%" || clientSecret == null)
                {
                    Spark.Warn("Twitch ClientID or ClientSecret Is Missing!");
                    Spark.TwitchConnection = false;
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
                                ConnectClient();
                                WebServer.Stop();
                                WebServer.Dispose();
                            }
                        }
                    };
                    API.Settings.AccessToken = accessToken;
                    ValidateAccessTokenResponse TokenResponse = await API.Auth.ValidateAccessTokenAsync(accessToken);

                    if (TokenResponse == null || OldScopes != Scopes)
                    {
                        var Values = new Dictionary<string, string>
                {
                { "client_id", clientID },
                { "force_verify", "false" },
                { "redirect_uri", DirectURL },
                { "response_type", "code" },
                { "scope", Scopes},
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
                        ConnectClient();
                    }
                }
            }
        }


        public class ClientData
        {
            public string Client_ID { get; set; }
            public string Client_Secret { get; set; }

            public string Refresh_Token { get; set; }
            public string Access_Token { get; set; }

            public string Scopes { get; set; }
        }
    }
}
