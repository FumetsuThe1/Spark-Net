namespace WinFormsApp1.Designs
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            ConsoleBox = new RichTextBox();
            PowerButton = new Button();
            ClearButton = new Button();
            SuspendLayout();
            // 
            // ConsoleBox
            // 
            ConsoleBox.BackColor = Color.FromArgb(40, 40, 40);
            ConsoleBox.BorderStyle = BorderStyle.None;
            ConsoleBox.Font = new Font("Segoe UI", 11.5F);
            ConsoleBox.ForeColor = SystemColors.Menu;
            ConsoleBox.Location = new Point(-1, -1);
            ConsoleBox.Name = "ConsoleBox";
            ConsoleBox.ReadOnly = true;
            ConsoleBox.Size = new Size(1005, 475);
            ConsoleBox.TabIndex = 0;
            ConsoleBox.Text = " No actions have been logged..";
            // 
            // PowerButton
            // 
            PowerButton.Font = new Font("Segoe UI", 14F);
            PowerButton.Location = new Point(910, 477);
            PowerButton.Name = "PowerButton";
            PowerButton.Size = new Size(86, 44);
            PowerButton.TabIndex = 1;
            PowerButton.Text = "Start";
            PowerButton.UseVisualStyleBackColor = true;
            PowerButton.Click += PowerButton_Click;
            // 
            // ClearButton
            // 
            ClearButton.Font = new Font("Segoe UI", 14F);
            ClearButton.Location = new Point(2, 477);
            ClearButton.Name = "ClearButton";
            ClearButton.Size = new Size(86, 44);
            ClearButton.TabIndex = 2;
            ClearButton.Text = "Clear";
            ClearButton.UseVisualStyleBackColor = true;
            ClearButton.Click += ClearButton_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(24, 24, 24);
            ClientSize = new Size(998, 524);
            Controls.Add(ClearButton);
            Controls.Add(PowerButton);
            Controls.Add(ConsoleBox);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Name = "MainForm";
            Text = "Spark NET";
            FormClosing += MainForm_Closing;
            Load += MainForm_Load;
            ResumeLayout(false);
        }

        #endregion

        public RichTextBox ConsoleBox;
        public Button PowerButton;
        public Button ClearButton;
    }
}