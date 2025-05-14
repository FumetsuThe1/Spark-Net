using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinFormsApp1.Designs;
using NAudio.Wave;
using Vosk;
using System.Speech.Recognition;
using TwitchLib.Client;
using System.Text.RegularExpressions;
using System.Diagnostics;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Security.Policy;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using System.Text.Json;
using static System.Formats.Asn1.AsnWriter;
using Spark_NET.Classes;
using NAudio.Vorbis;


// Add Global Keybind Support
// Optimize and Refine everything, maybe implement Threading
// Add Emotion Support
// Add OBS Connection

namespace WinFormsApp1.Classes
{
    public class Spark
    {
        public bool twitchConnection = true;
        public bool enableRecognition = true;
        public bool obsConnection = false;
        public bool commandConnection = true;

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

        public const string osName = "Spark ";
        public const string osOpen = osName + "open ";
        public const string osStart = osName + "start ";

        const string steamPath = @"E:\Tools\Windows\Steam\steam.exe";
        const string epicGames = @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe";

        public readonly Color defaultColor = Color.White;
        public readonly Color responseColor = Color.Aqua;

        public readonly Color speechColor = Color.FromArgb(103, 36, 237);
        public readonly Color warningColor = Color.Red;
        public readonly Color utilColor = Color.FromArgb(237, 183, 36);

        public readonly Color paramColor = Color.Pink;
        readonly Color processColor = Color.Green;

        const string NoActionString = " No actions have been logged..";


        public string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET");
        public string binData = Path.Combine(Environment.CurrentDirectory, "Data");
        public string twitchPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET", "Twitch");
        public string optionsPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET"), "Settings.json");
        public string soundsPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET"), "Sounds");

        public Dictionary<string, string> responses = new Dictionary<string, string>();

        public void PlaySound(string Sound, string? soundPath = null)
        {
            if (soundPath == null)
            {
                soundPath = soundsPath;
            }

            if (File.Exists(Path.Combine(soundPath, Sound)))
            {
                string extensionType = Path.GetExtension(Path.Combine(soundPath, Sound));
                var waveOut = new WaveOut();
                switch (extensionType)
                {
                    case ".wav":
                        var wavreader = new WaveFileReader(Path.Combine(soundPath, Sound));
                        waveOut.Init(wavreader);
                        waveOut.Play();
                        waveOut.PlaybackStopped += (s, e) =>
                        {
                            waveOut.Dispose();
                            wavreader.Dispose();
                        };
                        break;
                    case ".mp3":
                        var mp3reader = new Mp3FileReader(Path.Combine(soundPath, Sound));
                        waveOut.Init(mp3reader);
                        waveOut.Play();
                        waveOut.PlaybackStopped += (s, e) =>
                        {
                            waveOut.Dispose();
                            mp3reader.Dispose();
                        };
                        break;
                    case ".ogg":
                        var oggreader = new VorbisWaveReader(Path.Combine(soundPath, Sound));
                        waveOut.Init(oggreader);
                        waveOut.Play();
                        waveOut.PlaybackStopped += (s, e) =>
                        {
                            waveOut.Dispose();
                            oggreader.Dispose();
                        };
                        break;
                    default:
                        Warn("Tried to play unsupported sound type: " + extensionType);
                        break;
                }
            }
            else
            {
                DebugLog("Failed to play sound! Sound doesn't exist!");
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

        public string Decrypt(string text)
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

        public async Task HandleExit()
        {
            exiting = true;
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
                File.OpenWrite(Path);
            }
        }

        public async Task AppLoad()
        {
            await CreatePath(dataPath);
            await CreatePath(Path.Combine(dataPath, "Logs"));
            await CreatePath(twitchPath);
            await CreatePath(Path.Combine(twitchPath, "Twitch Logs"));
            await CreatePath(soundsPath);
            await CreatePath(Path.Combine(soundsPath, "Twitch"));

            ResetLog();

            DebugLog("Spark Loaded!");
            BuildLibrary();
            Command.AppLoad();
            await Twitch.AppLoad();
            PlaySound("Startup.mp3");
        }

        private void BuildLibrary()
        {
            #region VoiceRecognition

            #region Choices

            AddChoice(osName + "ban that guy", "twitch_ban_recent");
            AddChoice(osName + "clip that", "clip_that");
            AddChoice(osName + "release the monkeys", "release_monkeys");

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
            #endregion

            #region AntiMixup
            AddMixup("Clip That");
            AddMixup("Ban That Guy");
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

        private void AddChoice(string Phrase, string ActionID = "%null%")
        {
            Phrase = Phrase.ToLower();
            Classes.Recognition.choices.Add(Phrase);
            Classes.Recognition.phrases.TryAdd(Phrase, ActionID);
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
                Shutdown();
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
        public static Spark Spark = new Spark();
        public static Recognition Recognition = new Recognition();
        public static Emotion Emotion = new Emotion();
        public static Twitch Twitch = new Twitch();
        public static Command Command = new Command();
        public static OBS OBS = new OBS();
    }
}
