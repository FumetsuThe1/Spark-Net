using WinFormsApp1.Designs;

namespace WinFormsApp1.Classes
{
    public class Command
    {
        readonly public string moduleName = "Command";


        MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];
        Spark Spark = Classes.Spark;
        Twitch Twitch = Classes.Twitch;
        Recognition Recognition = Classes.Recognition;


        public Dictionary<string, string> commandList = new();


        private async void CommandLibrary(string Command, string Parameter, string CasedParameter)
        {
            string[] parameters = Parameter.Split(' ');
            switch (Command)
            {
                case "exit":
                    Application.Exit(); break;
                case "crash":
                    throw new Exception("A crash was forced from console!");

                case "start":
                    Classes.Spark.Startup(); break;
                case "stop":
                    Classes.Spark.Shutdown(); break;
                case "restart":
                    Classes.Spark.Restart(); break;

                case "command":
                    RunCommand(CasedParameter); break;
                case "say":
                    Classes.Spark.Say(CasedParameter); break;
                case "debug":
                    Classes.Spark.DebugLog(CasedParameter); break;
                case "warn":
                    Classes.Spark.Warn(CasedParameter); break;
                case "parameter":
                    Classes.Spark.Log(CasedParameter, Classes.Spark.paramColor); break;

                case "twitch":
                    if (parameters.Length < 1)
                    {
                        Spark.Warn("Twitch command requires a parameter!");
                        return;
                    }
                    else
                    {
                        switch (parameters[0])
                        {
                            case "banrecent":
                                Classes.Twitch.BanRecentUser(); break;
                            case "viewers":
                                Classes.Twitch.Log($"Current Viewers: {Classes.Twitch.CurrentViewers()}"); break;
                            case "start":
                                await Classes.Twitch.LoadConnection(); break;
                            case "stop":
                                Classes.Twitch.Disconnect(); break;
                            case "eventsub":
                                switch (parameters[1])
                                {
                                    case "start":
                                        Classes.Twitch.eventSub.StartAsync(); break;
                                    case "stop":
                                        Classes.Twitch.DisconnectEventsub(); break;
                                    default:
                                        Spark.Warn("Invalid EventSub command!"); break;
                                }
                                break;
                        }
                        break;
                    }
            }
        }

        public void Add(string String, bool VA = false)
        {
            String = String.ToLower();
            commandList.Add(String, String);
            if (!commandList.ContainsKey(String))
            {
                
            }
            if (VA)
            {
                Recognition.choices.Add(Spark.osName + String);
            }
        }

        private void ConsoleBar_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter && !Spark.exiting)
            {
                string command = MainForm.CommandBar.Text;
                MainForm.CommandBar.Clear();
                RunCommand("say ah booga");
            }
        }

        public void RunCommand(string Command, string Parameter = "%null%")
        {
            Spark.DebugLog("Running Command: " + Command);
            if (Spark.commandConnection)
            {
                string CasedParameter = Parameter;
                string CasedCommand = Command;

                Command = Command.ToLower();

                string[] Words = Command.Split(' ');
                string Result = string.Join(" ", Command.Split().Take(Words.Length));

                Spark.DebugLog("Command: " + Words[0]);

                if (commandList.TryGetValue(Words[0], out string? String))
                {
                    Command = Words[0];

                    if (Words.Length > 1)
                    {
                        CasedParameter = CasedCommand.Substring(Command.Length + 1);
                        Parameter = CasedParameter.ToLower();
                    }

                    Spark.DebugLog("Command: " + Command);
                    Spark.DebugLog("Parameter: " + Parameter);

                    CommandLibrary(Command, Parameter, CasedParameter);
                }
                else
                {
                    Spark.Warn("Command Not Found!");
                }
            }
            else
            {
                Spark.Log("Failed to run command! Command Connection is disabled!", Spark.utilColor);
            }
        }

        public void AppLoad()
        {
            MainForm.ConsoleBox.KeyPress += ConsoleBar_KeyPress;
        }
    }
}
