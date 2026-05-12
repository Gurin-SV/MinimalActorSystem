namespace Demo
{
    partial class FormMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            _tabControl = new TabControl();
            _timer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // _tabControl
            // 
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Location = new Point(0, 0);
            _tabControl.Multiline = true;
            _tabControl.Name = "_tabControl";
            _tabControl.SelectedIndex = 0;
            _tabControl.ShowToolTips = true;
            _tabControl.Size = new Size(800, 450);
            _tabControl.TabIndex = 0;
            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            // 
            // _timer
            // 
            _timer.Enabled = true;
            _timer.Interval = 25;
            _timer.Tick += Timer_Tick;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(_tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "FormMain";
            Text = "Demo";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private TabControl _tabControl;
        private System.Windows.Forms.Timer _timer;
    }
}
