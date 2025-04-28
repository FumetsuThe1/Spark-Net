using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinFormsApp1.Classes;
using WinFormsApp1.Designs;

namespace WinFormsApp1.Designs
{
    public partial class MainForm : Form
    {
        Classes Classes = new Classes();

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
    }


    public class Classes
    {
        static public Spark Spark = new Spark();
        static public Recognition Recognition = new Recognition();
        static public Command Command = new Command();
    }
}
