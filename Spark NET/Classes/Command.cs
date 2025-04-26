using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinFormsApp1.Designs;

namespace WinFormsApp1.Classes
{
    public class Classes
    {
        public static Spark Spark = new Spark();
        public static Recognition Recognition = new Recognition();
        public static Twitch Twitch = new Twitch();
        public static Command Command = new Command();
    }

    public class Command
    {
        readonly MainForm MainForm = (MainForm)System.Windows.Forms.Application.OpenForms["MainForm"];


        public Dictionary<string, string> commandList = new Dictionary<string, string>();


        private void CommandLibrary(string Command, string Parameter, string CasedParameter)
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
            }
        }

        public void RunCommand(string Command, string Parameter = "%null%")
        {
            string CasedParameter = Parameter;
            string CasedCommand = Command;

            Command = Command.ToLower();

            string[] Words = Command.Split(' ');
            string Result = string.Join(" ", Command.Split().Take(Words.Length));

            if (commandList.TryGetValue(Words.GetValue(0).ToString(), out string? String))
            {
                Command = Words.GetValue(0).ToString();

                if (Words.Length > 1)
                {
                    CasedParameter = CasedCommand.Substring(Command.Length + 1);
                    Parameter = CasedParameter.ToLower();
                }

                Classes.Spark.DebugLog("Command: " + Command);
                Classes.Spark.DebugLog("Parameter: " + Parameter);

                CommandLibrary(Command, Parameter, CasedParameter);
            }
            else
            {
                Classes.Spark.Warn("Command Not Found!");
            }
        }
    }
}
