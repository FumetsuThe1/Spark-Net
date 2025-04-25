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


 // Add Global Keybind Support

namespace WinFormsApp1.Classes
{
    public class Spark
    {
        public bool TwitchConnection = true;
        public bool EnableRecognition = true;

        readonly MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        Twitch Twitch = Classes.Twitch;
        Recognition Recognition = Classes.Recognition;
        Command Command = Classes.Command;

        public bool Powered = false;
        bool NoActions = true;
        public bool SparkPowered = false;

        const bool EnableLogging = true;
        const bool ResetLogs = false;

        public const string osName = "Spark ";
        public const string osOpen = osName + "open ";
        public const string osStart = osName + "start ";

        public readonly Color DefaultColor = Color.White;
        public readonly Color ResponseColor = Color.Aqua;

        public readonly Color SpeechColor = Color.FromArgb(103, 36, 237);
        readonly Color WarningColor = Color.Red;
        readonly Color UtilColor = Color.FromArgb(237, 183, 36);

        public readonly Color ParamColor = Color.Pink;
        readonly Color ProcessColor = Color.Green;

        const string NoActionString = " No actions have been logged..";

        const string SteamPath = @"E:\Tools\Windows\Steam\steam.exe";
        const string EpicGames = @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe";

        public string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET");
        public string binData = Path.Combine(Environment.CurrentDirectory, "Data");
        public string TwitchPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spark NET", "Twitch");

        public Dictionary<string, string> Responses = new Dictionary<string, string>();

        public async Task HandleExit()
        {
            Shutdown();
            MainForm.ConsoleBox.Enabled = false;
            MainForm.PowerButton.Enabled = false;
            MainForm.ClearButton.Enabled = false;
            await Twitch.AppClosing();

            MainForm.ExitComplete = true;
            System.Windows.Forms.Application.Exit();
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
            await CreatePath(DataPath);
            await CreatePath(Path.Combine(DataPath, "Logs"));
            await CreatePath(TwitchPath);
            await CreatePath(Path.Combine(TwitchPath, "MessageLogs"));
            await CreatePath(binData);
            await CreateFile(Path.Combine(binData, "Client.json"));

            ResetLog();

            DebugLog("Spark Loaded!");
            BuildLibrary();
            CodeLibrary();
            await Twitch.AppLoad();
        }

        private void BuildLibrary()
        {
            AddChoice(osName + "ban that guy", "twitch_ban_recent");

            AddResponse("burger", "[E:\\Testo.txt]");
            AddResponse("fence", "This isn't a fence..");
            AddResponse("bank", "Timble Bonk");

            AddProcess("Test Process", "Big Path?");
        }

        private void CodeLibrary()
        {
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
        }

        public void Shutdown()
        {
            MainForm.PowerButton.Text = "Stopping";
            MainForm.PowerButton.Enabled = false;

            string api = Recognition.RecognitionModel.ToLower();
            if (Powered)
            {
                Classes.Recognition.waveIn.StopRecording();
                Classes.Recognition.waveIn.Dispose();
                if (api == "windows" || api == "vosk backing")
                {
                    Classes.Recognition.winRec.RecognizeAsyncStop();
                    Classes.Recognition.winRec.Dispose();
                }
                Powered = false;
                MainForm.PowerButton.Text = "Start";
                MainForm.PowerButton.Enabled = true;
                if (SparkPowered)
                {
                    SparkPowered = false;
                    Log("Spark has been shutdown.", UtilColor);
                }
            }
            Command.RunCommand("say big testo biggo!");
        }

        private void AddCommand(string String, bool VA = false)
        {
            String = String.ToLower();
            if (!Classes.Command.CommandList.ContainsKey(String))
            {
                Classes.Command.CommandList.Add(String, String);
            }
            if (VA)
            {
                Classes.Recognition.Choices.Add(osName + String);
            }
        }

        public void Respond(string Phrase)
        {
            string Response;
            if (Responses.TryGetValue(Phrase, out var Line))
            {
                Response = Responses[Phrase].ToString();
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
            Responses.TryAdd(Phrase, Response);
            AddChoice(Phrase, "respond");
        }

        private void AddChoice(string Phrase, string ActionID = "%null%")
        {
            Phrase = Phrase.ToLower();
            Classes.Recognition.Choices.Add(Phrase);
            Classes.Recognition.Phrases.TryAdd(Phrase, ActionID);
        }

        private void AddProcess(string Phrase, string FilePath, string Params = "")
        {
            Phrase = Phrase.ToLower();
            Classes.Recognition.Processes.TryAdd(Phrase, new Tuple<string, string>(FilePath, Params));
            AddChoice(Phrase, "run");
        }

        public void DebugLog(string Text)
        {
            Debug.WriteLine(Text);
        }

        public void Log(string Text, Color color, bool Force = false)
        {
            if (EnableLogging || Force)
            {
                if (NoActions)
                {
                    ClearLog();
                }
                DebugLog(Text);
                MainForm.ConsoleBox.AppendText(" - " + Text, color);
                MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
                NoActions = false;
            }
        }

        public void ResetLog()
        {
            ClearLog();
            MainForm.ConsoleBox.AppendText(NoActionString, DefaultColor);
            MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
            NoActions = true;
        }

        public void ClearLog()
        {
            if (MainForm.InvokeRequired) {MainForm.ConsoleBox.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => MainForm.ConsoleBox.Clear())); }
            else { MainForm.ConsoleBox.Clear(); }
        }

        public void Say(string Response)
        {
            if (NoActions)
            {
                ClearLog();
            }
            NoActions = false;
            DebugLog(Response);
            MainForm.ConsoleBox.AppendText(" - SPARK: " + Response, ResponseColor);
            MainForm.ConsoleBox.AppendText(Environment.NewLine, Color.White);
        }
        public void Warn(string Text)
        {
            if (NoActions)
            {
                ClearLog();
            }
            Log("Warning! " + Text, WarningColor);
            NoActions = false;
        }

        public void SparkStarted()
        {
            Log("Spark has been started!", UtilColor);

            SparkPowered = true;
            MainForm.PowerButton.Text = "Stop";
            MainForm.PowerButton.Enabled = true;
        }

        public void Startup()
        {
            if (!Powered)
            {
                MainForm.PowerButton.Text = "Starting";
                MainForm.PowerButton.Enabled = false;
                Powered = true;
                if (ResetLogs || NoActions)
                {
                    ClearLog();
                }
                Classes.Recognition.Load();
            }
        }

        public void TogglePower()
        {
            if (Powered)
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
            if (Powered)
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
}
