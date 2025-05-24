using NAudio.Vorbis;
using NAudio.Wave;
using Spark_NET.Classes;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WinFormsApp1.Designs;
using WinFormsApp1.Classes;


// Add Global Keybind Support
// Optimize and Refine everything, maybe implement Threading
// Add OBS Connection

namespace WinFormsApp1.Classes
{
    public class Spark
    {
        public bool twitchConnection = true;
        public bool enableRecognition = true;
        public bool obsConnection = false;
        public bool commandConnection = true;

        bool enableSounds = true;

        readonly MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        Twitch Twitch = Classes.Twitch;
        Recognition Recognition = Classes.Recognition;
        Command Command = Classes.Command;

        public bool powered = false;
        bool noActions = true;
        public bool sparkPowered = false;

        public bool exiting = false;

        public bool enableLogging = true;
        public bool resetLogs = false;

        public const string osName = "Spark";
        public const string osOpen = osName + "open";
        public const string osStart = osName + "start";

        const string steamPath = @"E:\Tools\Windows\Steam\steam.exe";
        const string epicGames = @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe";

        public readonly Color defaultColor = Color.White;
        public readonly Color responseColor = Color.FromArgb(36, 183, 237);

        public readonly Color speechColor = Color.FromArgb(103, 36, 237);
        public readonly Color warningColor = Color.FromArgb(237, 36, 36);
        public readonly Color utilColor = Color.FromArgb(237, 183, 36);

        public readonly Color commandColor = Color.FromArgb(184, 118, 39);
        public readonly Color paramColor = Color.Pink;
        readonly Color processColor = Color.FromArgb(65, 156, 54);

        const string NoActionString = " No actions have been logged..";

        public DateTime startupTime { get; private set; } = DateTime.Now;
        public TimeSpan loadTime { get; private set; } = TimeSpan.Zero;


        public string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET");
        public string binData = Path.Combine(Environment.CurrentDirectory, "Data");
        public string optionsPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET"), "Settings.json");
        public string soundsPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET"), "Sounds");

        public Dictionary<string, string> responses = new();


        public void PlaySound(string Sound, string? soundPath = null, int volume = 100, bool forceplay = false)
        {
            if (enableSounds || forceplay)
            {
                if (soundPath == null)
                {
                    soundPath = soundsPath;
                }

                if (File.Exists(Path.Combine(soundPath, Sound)))
                {
                    if (volume <= 100 && volume > 0)
                    {
                        string extensionType = Path.GetExtension(Path.Combine(soundPath, Sound));

                        var waveOut = new WaveOut();
                        float trueVolume = (volume / 100f);
                        waveOut.Volume = trueVolume;

                        switch (extensionType)
                        {
                            case ".wav":
                                using (var wavreader = new WaveFileReader(Path.Combine(soundPath, Sound)))
                                {
                                    waveOut.Init(wavreader);
                                    waveOut.Play();
                                }
                                break;
                            case ".mp3":
                                using (var mp3reader = new Mp3FileReader(Path.Combine(soundPath, Sound)))
                                {
                                    waveOut.Init(mp3reader);
                                    waveOut.Play();
                                }
                                break;
                            case ".ogg":
                                using (var oggreader = new VorbisWaveReader(Path.Combine(soundPath, Sound)))
                                {
                                    waveOut.Init(oggreader);
                                    waveOut.Play();
                                }
                                break;
                            default:
                                Warn("Tried to play unsupported sound type: " + extensionType);
                                break;
                        }
                    }
                    else
                    {
                        DebugLog("Failed to play sound! Invalid volume!");
                    }
                }
                else
                {
                    DebugLog("Failed to play sound! Sound doesn't exist!");
                }
            }
        }

        public string CurrentTime()
        {
            DateTime now = DateTime.Now;
            string time = now.ToString("HH:mm:ss");
            return time;
        }

        public string Encrypt(string text)
        {
            if (text == null || text == "%null%")
            {
                return "%null%";
            }
            else
            {
                if (text.Length >= 1)
                {
                    string encrypted = "";
                    string firstHalf = text.Substring(0, text.Length / 2);
                    string secondHalf = text.Substring((text.Length / 2) - 3);
                    string lastLetters = text.Substring(text.Length - 3);
                    foreach (char c in text)
                    {
                        encrypted += (char)(c + 2);
                    }
                    return encrypted;
                }
                else
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// Checks if the JSON file contains the specified keys.
        /// </summary>
        /// <param name="jsonPath"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public bool JsonContainsKeys(string jsonPath, string[] keys)
        {
            var json = File.ReadAllText(jsonPath);
            if (json.Length >= 1)
            {
                JsonNode? node = JsonObject.Parse(json);

                if (node is JsonArray arr && arr.Count > 0 && arr[0] is JsonObject obj)
                {
                    bool allKeysPresent = keys.All(key => obj.ContainsKey(key));

                    if (allKeysPresent)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public string Decrypt(string text)
        {
            if (text == null || text == "%null%")
            {
                return "%null%";
            }
            else
            {
                if (text.Length >= 1)
                {
                    string decrypted = "";
                    string firstHalf = text.Substring(0, text.Length / 2);
                    string secondHalf = text.Substring((text.Length / 2) - 3);
                    string lastLetters = text.Substring(text.Length - 3);
                    foreach (char c in text)
                    {
                        decrypted += (char)(c - 2);
                    }
                    return decrypted;
                }
                else
                {
                    return "";
                }
            }
        }

        public async Task HandleExit()
        {
            exiting = true;
            MainForm.Visible = false;
            Shutdown();
            MainForm.CommandBar.ReadOnly = true;
            MainForm.PowerButton.Enabled = false;
            MainForm.ClearButton.Enabled = false;
            await Twitch.AppClosing();

            MainForm.ExitComplete = true;
            System.Windows.Forms.Application.Exit();
        }

        private async Task SaveData()
        {
            await CreateFile(optionsPath);
            var data = new List<Options>
            {
                new Options
                {
                    enableLogging = enableLogging,
                    resetLogs = resetLogs,
                    twitchConnection = twitchConnection
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            string jsonString = JsonSerializer.Serialize(data, options);

            await using (StreamWriter swc = new StreamWriter(optionsPath))
            {
                await swc.WriteLineAsync(jsonString);
            }
        }

        private async Task LoadData()
        {
            Options options = new Options();
            if (System.IO.File.Exists(optionsPath))
            {
                var clientJson = await System.IO.File.ReadAllTextAsync(optionsPath);
                var Json = JsonSerializer.Deserialize<Options[]>(clientJson);

                twitchConnection = Json[0].twitchConnection;
                enableLogging = Json[0].enableLogging;
                resetLogs = Json[0].resetLogs;
            }
        }

        public async Task CreatePath(string Path)
        {
            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
            }
        }

        public async Task CreateFile(string Path, string? FileName = null)
        {
            if (FileName != null)
            {
                FileName = System.IO.Path.Combine(Path, FileName);
            }
            if (!File.Exists(Path))
            {
                var file = File.OpenWrite(Path);
                await file.FlushAsync();
                file.Close();
            }
        }

        public async Task AppLoad()
        {
            DateTime startTime = DateTime.Now;

            #region FilePaths
            await CreatePath(dataPath);
            await CreatePath(Path.Combine(dataPath, "Logs"));
            await CreatePath(Twitch.twitchPath);
            await CreatePath(Path.Combine(Twitch.twitchPath, "Twitch Logs"));
            await CreatePath(soundsPath);
            await CreatePath(Path.Combine(soundsPath, "Twitch"));
            await CreatePath(Path.Combine(Twitch.twitchPath, "Data"));
            #endregion

            #region Files
            await CreateFile(Path.Combine(Twitch.twitchPath, "Data", "README.txt"));
            #endregion

            await using (StreamWriter swc = new StreamWriter(Path.Combine(Twitch.twitchPath, "Data", "README.txt")))
            {
                await swc.WriteLineAsync("THE DATA WITHIN THIS FOLDER IS HIGHLY SENSITIVE, DO NOT SHARE IT WITH ANYONE!");
            }

            ResetLog();

            DebugLog("Spark Loaded!");
            BuildLibrary();
            Command.AppLoad();
            await Twitch.AppLoad();
            PlaySound("Startup.mp3");

            DateTime endTime = DateTime.Now;
            startupTime = endTime;
            loadTime = endTime - startTime;
            DebugLog($"Spark Loaded in: {loadTime.TotalSeconds} Seconds!");
        }

        private void BuildLibrary()
        {
            #region VoiceRecognition

            #region Choices

            AddChoice($"{osName} ban that guy", "twitch_ban_recent", [$"{osName} ban that man", $"{osName} ban that fucker"]);
            AddChoice($"{osName} clip that", "clip_that");
            AddChoice($"{osName} release the monkeys", "release_monkeys");

            #endregion

            #region Responses

            AddResponse("burger", "[E:\\Testo.txt]");
            AddResponse("fence", "This isn't a fence..");
            AddResponse("bank", "Timble Bonk");

            #endregion

            #region Processes

            AddProcess("Test Process", "Big Path?");

            #endregion

            #endregion


            #region Commands
            AddCommand("Exit");
            AddCommand("Crash");

            AddCommand("Start");
            AddCommand("Stop");
            AddCommand("Restart");

            AddCommand("Say");
            AddCommand("Warn");
            AddCommand("Debug");
            AddCommand("Parameter");

            AddCommand("Command");
            AddCommand("Twitch");
            #endregion


            #region AntiMixup
            AddMixup("Clip That");
            AddMixup("Ban That Guy");
            AddMixup("Ban That Man");
            AddMixup("Ban That Fucker");
            AddMixup("Mods Ban That Guy");
            AddMixup("Jarvis Clip That");
            AddMixup("Jarvis Ban That Guy");
            #endregion
        }


        public void Shutdown()
        {
            PlaySound("Shutdown.mp3");
            if (Classes.Recognition.recognitionLoaded)
            {
                MainForm.PowerButton.Text = "Stopping";
                MainForm.PowerButton.Enabled = false;

                string api = Recognition.recognitionModel.ToLower();
                if (powered)
                {
                    Classes.Recognition.waveIn.StopRecording();
                    Classes.Recognition.waveIn.Dispose();
                    if (api == "windows" || api == "vosk backing")
                    {
                        Classes.Recognition.winRec.RecognizeAsyncStop();
                        Classes.Recognition.winRec.Dispose();
                    }
                    powered = false;
                    MainForm.PowerButton.Text = "Start";
                    MainForm.PowerButton.Enabled = true;
                    if (sparkPowered)
                    {
                        sparkPowered = false;
                        Log("Spark has been shutdown.", utilColor);
                    }
                }
            }
            Command.RunCommand("say big testo biggo!");
        }

        private void AddCommand(string String, bool VA = false)
        {
            Command.Add(String, VA);
        }

        public void Respond(string Phrase)
        {
            string Response;
            if (responses.TryGetValue(Phrase, out var Line))
            {
                Response = responses[Phrase].ToString();
                var isCommandArray = Regex.IsMatch(Response, @"^\[.*?\]$");
                if (isCommandArray)
                {
                    Response = Response.Substring(1);
                    Response = Response.Remove(Response.Length - 1);
                    if (File.Exists(Response))
                    {
                        Say(File.ReadAllText(Response));
                    }
                    else
                    {
                        Warn("Failed to respond! File does not exist!");
                    }
                }
                else
                {
                    Say(Response);
                }
            }
        }

        private void AddResponse(string Phrase, string Response)
        {
            Phrase = Phrase.ToLower();
            responses.TryAdd(Phrase, Response);
            AddChoice(Phrase, "respond");
        }

        private void AddMixup(string Phrase)
        {
            Phrase = Phrase.ToLower();
            if (!Classes.Recognition.antiMixups.Contains(Phrase) && !Classes.Recognition.phrases.ContainsKey(Phrase))
            {
                Classes.Recognition.antiMixups.Add(Phrase);
                Classes.Recognition.choices.Add(Phrase);
            }
        }

        private void AddChoice(string Phrase, string ActionID = "%null%", List<string>? aliases = null)
        {
            Phrase = Phrase.ToLower();
            Classes.Recognition.phrases.TryAdd(Phrase, ActionID);
            Classes.Recognition.choices.Add(Phrase);

            if (aliases != null)
            {
                foreach (string alias in aliases)
                {
                    string aliass = alias.ToLower();
                    DebugLog(alias);
                    DebugLog(ActionID);
                    Classes.Recognition.phrases.TryAdd(aliass, ActionID);
                    Classes.Recognition.choices.Add(aliass);
                }
            }
        }

        private void AddProcess(string Phrase, string FilePath, string Params = "")
        {
            Phrase = Phrase.ToLower();
            Classes.Recognition.processes.TryAdd(Phrase, new Tuple<string, string>(FilePath, Params));
            AddChoice(Phrase, "run");
        }

        public void DebugLog(string Text)
        {
            Debug.WriteLine(Text);
        }

        public void Log(string Text, Color color, bool Force = false)
        {
            if (enableLogging || Force)
            {
                if (noActions)
                {
                    ClearLog();
                }
                DebugLog(Text);
                MainForm.ConsoleBox.AppendText(" - " + Text, color);
                MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
                noActions = false;
            }
        }

        public void ResetLog()
        {
            ClearLog();
            MainForm.ConsoleBox.AppendText(NoActionString, defaultColor);
            MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
            noActions = true;
            Decrypt(Encrypt("Hello, it is a good day to test encryption today!"));
        }

        public void ClearLog()
        {
            if (MainForm.InvokeRequired) {MainForm.ConsoleBox.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => MainForm.ConsoleBox.Clear())); }
            else { MainForm.ConsoleBox.Clear(); }
        }

        public void Say(string Response)
        {
            if (noActions)
            {
                ClearLog();
            }
            noActions = false;
            DebugLog(Response);
            MainForm.ConsoleBox.AppendText(" - SPARK: " + Response, responseColor);
            MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
            PlaySound("Response.mp3");
        }

        public void Warn(string Text)
        {
            if (noActions)
            {
                ClearLog();
            }
            Log("Warning! " + Text, warningColor);
            noActions = false;
            PlaySound("Warning.mp3");
        }

        public void SparkStarted()
        {
            Log(Classes.Emotion.FormulateSentence("Spark has been started!"), utilColor);

            Classes.Recognition.recognitionLoaded = true;
            sparkPowered = true;
            MainForm.PowerButton.Text = "Stop";
            MainForm.PowerButton.Enabled = true;
            PlaySound("Started.mp3");
        }

        public void SendCommand(string String)
        {
            Command.RunCommand(String);
        }

        public void Startup()
        {
            if (enableRecognition)
            {
                if (!powered)
                {
                    MainForm.PowerButton.Text = "Starting";
                    MainForm.PowerButton.Enabled = false;
                    powered = true;
                    if (resetLogs || noActions)
                    {
                        ClearLog();
                    }
                    Classes.Recognition.Load();
                }
            }
            else
            {
                Warn("Recognition Module is not loaded!");
            }
        }

        public void TogglePower()
        {
            if (powered)
            {
                Shutdown();
            }
            else
            {
                Startup();
            }
        }

        public void Restart()
        {
            if (powered)
            {
                Shutdown();
                Startup();
            }
            else
            {
                Startup();
            }
        }
    }


    public static class RichTextBoxExtensions
    {
        private static void Append(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }

        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            if (box.InvokeRequired)
            {
                box.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => Append(box, text, color)));
            }
            else
            {
                Append(box, text, color);
            }
        }
    }

    public class Options
    {
        public bool enableLogging { get; set; }
        public bool resetLogs { get; set; }
        public bool twitchConnection { get; set; }
    }

    public class Classes
    {
        public static MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        public static Spark Spark = new();
        public static Recognition Recognition = new();
        public static Emotion Emotion = new();
        public static Twitch Twitch = new();
        public static Command Command = new();
        public static OBS OBS = new();
    }
}
