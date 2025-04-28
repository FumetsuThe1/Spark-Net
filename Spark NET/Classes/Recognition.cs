using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using WinFormsApp1.Designs;
using Vosk;
using WinFormsApp1.Classes;

 // Fix Responses Not Working

namespace WinFormsApp1.Classes
{
    public class Recognition
    {
        public const string recognitionModel = "Windows"; // Vosk, Windows, Vosk Backing
        public const double sensitivity = 60;

        const string smallModel = @"E:\Files\AI Models\vosk-model-small-en-us-0.15\vosk-model-small-en-us-0.15";
        const string largeModel = @"E:\Files\AI Models\vosk-model-en-us-0.22\vosk-model-en-us-0.22";

        public const string AIModel = smallModel;
        public bool recognitionLoaded = false;


        public Choices choices = new Choices();

        public readonly static Model model = new Model(AIModel);
        public readonly static VoskRecognizer voskRec = new VoskRecognizer(model, 44100);
        public SpeechRecognitionEngine winRec;

        public readonly WaveInEvent waveIn = new WaveInEvent();

        MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        Spark Spark = Classes.Spark;


        public Dictionary<string, Tuple<string, string>> processes = new Dictionary<string, Tuple<string, string>>();

        public Dictionary<string, string> phrases = new Dictionary<string, string>();
        public Dictionary<string, string> synonymPhrases = new Dictionary<string, string>();
        public Dictionary<string, Tuple<string, List<string>>> synonymWords = new Dictionary<string, Tuple<string, List<string>>>();



        private Choices GetChoiceLibrary()
        {
            return choices;
        }

        private void CommandLibrary(string Command, string Parameter, string CasedParameter)
        {
            if (Spark.enableRecognition)
            {
                switch (Command)
                {
                    case "exit":
                        Application.Exit(); break;
                    case "crash":
                        throw new Exception();

                    case "start":
                        Classes.Spark.Startup(); break;
                    case "stop":
                        Classes.Spark.Shutdown(); break;
                    case "restart":
                        Classes.Spark.Restart(); break;

                    case "say":
                        Classes.Spark.Say(CasedParameter); break;
                    case "debug":
                        Classes.Spark.DebugLog(CasedParameter); break;
                    case "warn":
                        Classes.Spark.Warn(CasedParameter); break;
                    case "parameter":
                        Classes.Spark.Log(CasedParameter, Classes.Spark.paramColor); break;
                }
            }
            else
            {
                Spark.DebugLog("Failed to run recognition command! Recognition is not enabled!");
            }
        }



        private void RunAction(string ID, string WinResult = "%null%", string VoskResult = "%null%")
        {
            if (Spark.enableRecognition)
            {
                switch (ID)
                {
                    default:
                        return;
                    case "debuglog":
                        Classes.Spark.DebugLog("test");
                        return;
                    case "twitch_ban_recent":
                        Classes.Twitch.BanRecentUser();
                        return;
                    case "clip_that":
                        Classes.Twitch.CreateClip();
                        return;
                }
            }
            else
            {
                Spark.DebugLog("Failed to run recognition command! Recognition is not enabled!");
            }
        }

        private void SpeechRecognised(string WinResult = "%null%", string VoskResult = "%null%")
        {
            string Phrase = "%null%";
            string ActionID = "%null%";

            if (phrases.TryGetValue(VoskResult, out ActionID))
            {
                Phrase = VoskResult;
            }
            else
            {
                if (phrases.TryGetValue(WinResult, out ActionID))
                {
                    Phrase = WinResult;
                }
            }

            Classes.Spark.DebugLog(Phrase);
            Classes.Spark.Respond(Phrase);

            if (Classes.Spark.responses.ContainsKey(Phrase))
            {
                Classes.Spark.responses.TryGetValue(Phrase, out string? value);
                Classes.Spark.Respond("CONTAINS KEYYY");
            }

            RunAction(ActionID, WinResult, VoskResult);
        }

        public void Load(bool forceLoad = false)
        {
            if (Spark.enableRecognition || forceLoad)
            {
                Classes.Command.RunCommand("say Big Sparked Testo!");
                string api = recognitionModel.ToLower();
                switch (api)
                {
                    case "vosk":
                        if (Directory.Exists(AIModel))
                        {
                            string fileName = "TestRecording_Vosk.wav";
                            Classes.Spark.DebugLog("Vosk Speech Recognition Activated!");
                            WaveFormat waveFormat = new WaveFormat(44100, 1);
                            WaveFileWriter waveFileWriter = new WaveFileWriter(fileName, waveFormat);

                            void AudioDetected(object? sender, WaveInEventArgs e)
                            {
                                waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
                                if (voskRec.AcceptWaveform(e.Buffer, e.BytesRecorded))
                                {
                                    Classes.Spark.DebugLog(voskRec.Result());
                                }
                            }

                            waveIn.WaveFormat = waveFormat;
                            waveIn.DataAvailable += AudioDetected;
                            waveIn.StartRecording();

                            Classes.Spark.SparkStarted();
                        }
                        else
                        {
                            Classes.Spark.Warn("Recognition Model doesn't exist!");
                            Classes.Spark.Shutdown();
                        }
                        break;
                    case "windows":
                        winRec = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));

                        var GrammarBuilder = new Grammar(GetChoiceLibrary());
                        winRec.LoadGrammar(GrammarBuilder);

                        winRec.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(WinSpeechRecognised);
                        winRec.SetInputToDefaultAudioDevice();

                        void WinSpeechRecognised(object? sender, SpeechRecognizedEventArgs e)
                        {
                            double Confidence = e.Result.Confidence * 100;
                            Classes.Spark.DebugLog("Confidence Check?");
                            if (Confidence >= sensitivity)
                            {
                                SpeechRecognised(e.Result.Text);
                                Classes.Spark.DebugLog("Confidence: " + Confidence);
                            }
                        }

                        winRec.RecognizeAsync(RecognizeMode.Multiple);
                        Classes.Spark.SparkStarted();
                        break;
                    case "vosk backing":

                        break;
                }
            }
        }
    }
}
