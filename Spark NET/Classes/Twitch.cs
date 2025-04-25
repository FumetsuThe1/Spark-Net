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

        string Scopes = "channel:moderate channel:bot chat:read chat:edit bits:read channel:read:redemptions channel:read:ads channel:read:editors";

        string ChannelID = "%null%";
        string ChannelName = "%null%";

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
                twitchClient.JoinChannel(Channel);
            }
            else
            {
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
            RecentUser = e.ChatMessage.Username;
            TwitchLib.Client.Models.ChatMessage Message = e.ChatMessage;
            Spark.DebugLog(Message.Message);
            if (LogMessages)
            {
                Spark.Log(Message.DisplayName + ": " + Message.Message, Color.MediumPurple);
            }
            string String = " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + " - " + Message.DisplayName + ": " + Message.Message;
            StoreMessage(String);
        }



        private void ConnectClient()
        {
            ConnectionCredentials Credentials = new ConnectionCredentials(BotUsername, accessToken);

            // Connected Events
            twitchClient.OnJoinedChannel += JoinedChannel;
            twitchClient.OnLeftChannel += LeftChannel;

            twitchClient.OnMessageReceived += MessageReceived;
            // // // //

            twitchClient.Initialize(Credentials);

            twitchClient.Connect();

            Log("Connected To Twitch!");
            JoinChannel();
        }


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


        public void BanUser(string User)
        {
            twitchClient.BanUser(ChannelName, User);
            Log("Banned Twitch User: " + User);
        }

        private void SendMessage(string Message)
        {
            twitchClient.SendMessage(ChannelName, Message);
        }

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

        public async Task LoadConnection()
        {
            if (Spark.TwitchConnection)
            {
                await AuthApp();

                API.Settings.Secret = clientSecret; API.Settings.AccessToken = accessToken; API.Settings.ClientId = clientID;
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

        public async Task AuthApp()
        {
            if (clientID == "%null%" || clientSecret == "%null%")
            {
                Spark.Warn("Twitch ClientID or ClientSecret Was Invalid!");
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
