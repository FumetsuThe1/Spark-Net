using WinFormsApp1.Classes;

namespace WinFormsApp1.Designs
{
    public partial class MainForm : Form
    {
        private bool Closing = false;
        public bool ExitComplete = false;

        public MainForm()
        {
            InitializeComponent();
            Application.ApplicationExit += new EventHandler(this.Application_ApplicationExit);
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

        private void MainForm_Closing(object sender, FormClosingEventArgs e)
        {
            if (!ExitComplete)
            {
                e.Cancel = true;
            }
            if (!Closing)
            {
                Closing = true;
                Classes.Spark.HandleExit();
            }
        }

        private void CommandBar_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                string input = CommandBar.Text;
                CommandBar.Clear();
                Classes.Spark.SendCommand(input);
            }
        }

        private void CommandBar_Enter(object sender, EventArgs e)
        {
            if (CommandBar.Text == "Enter Command" || CommandBar.Text == "" || CommandBar.Text == " ")
            {
                CommandBar.Clear();
            }
        }

        private void CommandBar_Leave(object sender, EventArgs e)
        {
            if (CommandBar.Text == "" || CommandBar.Text == " ")
            {
                CommandBar.Text = "Enter Command";
            }
        }
    }

    public class Classes
    {
        static public Spark Spark = new Spark();
    }
}
