using SparkNet.Classes;

namespace SparkNet.Designs
{
    public partial class MainForm : Form
    {
        private bool Closing = false;
        public bool ExitComplete = false;

        string enterCommandText = "> Enter Command";

        public MainForm()
        {
            InitializeComponent();
            Application.ApplicationExit += new EventHandler(this.Application_ApplicationExit);
            CommandBar.Text = enterCommandText;
        }

        private void Application_ApplicationExit(object? sender, EventArgs e)
        {
            Classes.Spark.DebugLog("The app is currently being closed!");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Classes.Spark.AppLoad();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Classes.Spark.ResetLog();
        }

        private void PowerButton_Click(object sender, EventArgs e)
        {
            Classes.Spark.TogglePower();
        }

        private async void MainForm_Closing(object sender, FormClosingEventArgs e)
        {
            if (!ExitComplete)
            {
                e.Cancel = true;
            }
            if (!Closing)
            {
                Closing = true;
                await Classes.Spark.HandleExit();
            }
        }

        private void CommandBar_KeyPress(object sender, KeyPressEventArgs e)
        {
            string input = CommandBar.Text;
            if (e.KeyChar == (char)Keys.Enter)
            {
                CommandBar.Clear();
                Classes.Spark.SendCommand(input);
            }
        }

        private void CommandBar_Enter(object sender, EventArgs e)
        {
            string text = CommandBar.Text;
            if (text == enterCommandText || string.IsNullOrWhiteSpace(text))
            {
                CommandBar.Clear();
            }
        }

        private void CommandBar_Leave(object sender, EventArgs e)
        {
            string text = CommandBar.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                CommandBar.Text = enterCommandText;
            }
        }
    }

    public class Classes
    {
        static public Spark Spark = new();
    }
}
