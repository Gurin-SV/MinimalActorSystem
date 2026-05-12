namespace Demo.Philosophers_Deadlock
{
    partial class SettingsControl
    {
        /// <summary> 
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором компонентов

        /// <summary> 
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel = new TableLayoutPanel();
            lblStartMode = new Label();
            cbStartMode = new ComboBox();
            flowLayoutPanelButtons = new FlowLayoutPanel();
            BtnStart = new Button();
            BtnStop = new Button();
            lblSpeedFactor = new Label();
            cmbSpeedFactor = new ComboBox();
            lblStates = new Label();
            lblPhilosopher1State = new Label();
            tbPhilosopher1State = new TextBox();
            lblPhilosopher2State = new Label();
            lblPhilosopher3State = new Label();
            lblPhilosopher4State = new Label();
            lblPhilosopher5State = new Label();
            tbPhilosopher2State = new TextBox();
            tbPhilosopher3State = new TextBox();
            tbPhilosopher4State = new TextBox();
            tbPhilosopher5State = new TextBox();
            lblLog = new Label();
            lbLog = new ListBox();
            tableLayoutPanel.SuspendLayout();
            flowLayoutPanelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.ColumnCount = 2;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel.Controls.Add(lblStartMode, 0, 0);
            tableLayoutPanel.Controls.Add(cbStartMode, 1, 0);
            tableLayoutPanel.Controls.Add(flowLayoutPanelButtons, 1, 1);
            tableLayoutPanel.Controls.Add(lblSpeedFactor, 0, 2);
            tableLayoutPanel.Controls.Add(cmbSpeedFactor, 1, 2);
            tableLayoutPanel.Controls.Add(lblStates, 0, 4);
            tableLayoutPanel.Controls.Add(lblPhilosopher1State, 0, 5);
            tableLayoutPanel.Controls.Add(tbPhilosopher1State, 1, 5);
            tableLayoutPanel.Controls.Add(lblPhilosopher2State, 0, 6);
            tableLayoutPanel.Controls.Add(lblPhilosopher3State, 0, 7);
            tableLayoutPanel.Controls.Add(lblPhilosopher4State, 0, 8);
            tableLayoutPanel.Controls.Add(lblPhilosopher5State, 0, 9);
            tableLayoutPanel.Controls.Add(tbPhilosopher2State, 1, 6);
            tableLayoutPanel.Controls.Add(tbPhilosopher3State, 1, 7);
            tableLayoutPanel.Controls.Add(tbPhilosopher4State, 1, 8);
            tableLayoutPanel.Controls.Add(tbPhilosopher5State, 1, 9);
            tableLayoutPanel.Controls.Add(lblLog, 0, 10);
            tableLayoutPanel.Controls.Add(lbLog, 0, 11);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 12;
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel.Size = new Size(476, 475);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblStartMode
            // 
            lblStartMode.Anchor = AnchorStyles.Left;
            lblStartMode.AutoSize = true;
            lblStartMode.Location = new Point(3, 0);
            lblStartMode.Name = "lblStartMode";
            lblStartMode.Size = new Size(231, 30);
            lblStartMode.TabIndex = 0;
            lblStartMode.Text = "Режим старта деятельности философов. Изменяется только после старта";
            // 
            // cbStartMode
            // 
            cbStartMode.Dock = DockStyle.Fill;
            cbStartMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cbStartMode.FormattingEnabled = true;
            cbStartMode.Items.AddRange(new object[] { "Одновременно", "Случайно" });
            cbStartMode.Location = new Point(241, 3);
            cbStartMode.MaxDropDownItems = 2;
            cbStartMode.Name = "cbStartMode";
            cbStartMode.Size = new Size(232, 23);
            cbStartMode.TabIndex = 1;
            // 
            // flowLayoutPanelButtons
            // 
            flowLayoutPanelButtons.AutoSize = true;
            flowLayoutPanelButtons.Controls.Add(BtnStart);
            flowLayoutPanelButtons.Controls.Add(BtnStop);
            flowLayoutPanelButtons.Dock = DockStyle.Fill;
            flowLayoutPanelButtons.Location = new Point(241, 33);
            flowLayoutPanelButtons.Margin = new Padding(3, 3, 3, 8);
            flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            flowLayoutPanelButtons.Size = new Size(232, 29);
            flowLayoutPanelButtons.TabIndex = 2;
            // 
            // BtnStart
            // 
            BtnStart.Location = new Point(0, 3);
            BtnStart.Margin = new Padding(0, 3, 3, 3);
            BtnStart.Name = "BtnStart";
            BtnStart.Size = new Size(75, 23);
            BtnStart.TabIndex = 0;
            BtnStart.Text = "Старт";
            BtnStart.UseVisualStyleBackColor = true;
            BtnStart.Click += BtnStart_Click;
            // 
            // BtnStop
            // 
            BtnStop.Location = new Point(84, 3);
            BtnStop.Margin = new Padding(6, 3, 3, 3);
            BtnStop.Name = "BtnStop";
            BtnStop.Size = new Size(75, 23);
            BtnStop.TabIndex = 1;
            BtnStop.Text = "Стоп";
            BtnStop.UseVisualStyleBackColor = true;
            BtnStop.Click += BtnStop_Click;
            // 
            // lblSpeedFactor
            // 
            lblSpeedFactor.Anchor = AnchorStyles.Left;
            lblSpeedFactor.AutoSize = true;
            lblSpeedFactor.Location = new Point(3, 70);
            lblSpeedFactor.Name = "lblSpeedFactor";
            lblSpeedFactor.Size = new Size(213, 30);
            lblSpeedFactor.TabIndex = 3;
            lblSpeedFactor.Text = "Фактор скорости. Определяет время задержки между действиями акторов";
            // 
            // cmbSpeedFactor
            // 
            cmbSpeedFactor.Dock = DockStyle.Fill;
            cmbSpeedFactor.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSpeedFactor.FormattingEnabled = true;
            cmbSpeedFactor.Items.AddRange(new object[] { "1 - наиболее медленно", "2", "3", "4", "5", "6", "7", "8", "9 - наиболее быстро" });
            cmbSpeedFactor.Location = new Point(238, 73);
            cmbSpeedFactor.Margin = new Padding(0, 3, 3, 3);
            cmbSpeedFactor.Name = "cmbSpeedFactor";
            cmbSpeedFactor.Size = new Size(235, 23);
            cmbSpeedFactor.TabIndex = 4;
            // 
            // lblStates
            // 
            lblStates.Anchor = AnchorStyles.Left;
            lblStates.AutoSize = true;
            tableLayoutPanel.SetColumnSpan(lblStates, 2);
            lblStates.Location = new Point(3, 110);
            lblStates.Name = "lblStates";
            lblStates.Size = new Size(113, 15);
            lblStates.TabIndex = 5;
            lblStates.Text = "Состояние акторов";
            // 
            // lblPhilosopher1State
            // 
            lblPhilosopher1State.Anchor = AnchorStyles.Right;
            lblPhilosopher1State.AutoSize = true;
            lblPhilosopher1State.Location = new Point(150, 128);
            lblPhilosopher1State.Margin = new Padding(0, 0, 20, 0);
            lblPhilosopher1State.Name = "lblPhilosopher1State";
            lblPhilosopher1State.Size = new Size(68, 15);
            lblPhilosopher1State.TabIndex = 6;
            lblPhilosopher1State.Text = "Философ 1";
            // 
            // tbPhilosopher1State
            // 
            tbPhilosopher1State.BorderStyle = BorderStyle.None;
            tbPhilosopher1State.Dock = DockStyle.Fill;
            tbPhilosopher1State.Location = new Point(238, 128);
            tbPhilosopher1State.Margin = new Padding(0, 3, 3, 3);
            tbPhilosopher1State.Name = "tbPhilosopher1State";
            tbPhilosopher1State.ReadOnly = true;
            tbPhilosopher1State.Size = new Size(235, 16);
            tbPhilosopher1State.TabIndex = 7;
            // 
            // lblPhilosopher2State
            // 
            lblPhilosopher2State.Anchor = AnchorStyles.Right;
            lblPhilosopher2State.AutoSize = true;
            lblPhilosopher2State.Location = new Point(150, 150);
            lblPhilosopher2State.Margin = new Padding(0, 0, 20, 0);
            lblPhilosopher2State.Name = "lblPhilosopher2State";
            lblPhilosopher2State.Size = new Size(68, 15);
            lblPhilosopher2State.TabIndex = 8;
            lblPhilosopher2State.Text = "Философ 2";
            // 
            // lblPhilosopher3State
            // 
            lblPhilosopher3State.Anchor = AnchorStyles.Right;
            lblPhilosopher3State.AutoSize = true;
            lblPhilosopher3State.Location = new Point(150, 172);
            lblPhilosopher3State.Margin = new Padding(0, 0, 20, 0);
            lblPhilosopher3State.Name = "lblPhilosopher3State";
            lblPhilosopher3State.Size = new Size(68, 15);
            lblPhilosopher3State.TabIndex = 9;
            lblPhilosopher3State.Text = "Философ 3";
            // 
            // lblPhilosopher4State
            // 
            lblPhilosopher4State.Anchor = AnchorStyles.Right;
            lblPhilosopher4State.AutoSize = true;
            lblPhilosopher4State.Location = new Point(150, 194);
            lblPhilosopher4State.Margin = new Padding(0, 0, 20, 0);
            lblPhilosopher4State.Name = "lblPhilosopher4State";
            lblPhilosopher4State.Size = new Size(68, 15);
            lblPhilosopher4State.TabIndex = 10;
            lblPhilosopher4State.Text = "Философ 4";
            // 
            // lblPhilosopher5State
            // 
            lblPhilosopher5State.Anchor = AnchorStyles.Right;
            lblPhilosopher5State.AutoSize = true;
            lblPhilosopher5State.Location = new Point(150, 216);
            lblPhilosopher5State.Margin = new Padding(0, 0, 20, 0);
            lblPhilosopher5State.Name = "lblPhilosopher5State";
            lblPhilosopher5State.Size = new Size(68, 15);
            lblPhilosopher5State.TabIndex = 11;
            lblPhilosopher5State.Text = "Философ 5";
            // 
            // tbPhilosopher2State
            // 
            tbPhilosopher2State.BorderStyle = BorderStyle.None;
            tbPhilosopher2State.Dock = DockStyle.Fill;
            tbPhilosopher2State.Location = new Point(238, 150);
            tbPhilosopher2State.Margin = new Padding(0, 3, 3, 3);
            tbPhilosopher2State.Name = "tbPhilosopher2State";
            tbPhilosopher2State.ReadOnly = true;
            tbPhilosopher2State.Size = new Size(235, 16);
            tbPhilosopher2State.TabIndex = 12;
            // 
            // tbPhilosopher3State
            // 
            tbPhilosopher3State.BorderStyle = BorderStyle.None;
            tbPhilosopher3State.Dock = DockStyle.Fill;
            tbPhilosopher3State.Location = new Point(238, 172);
            tbPhilosopher3State.Margin = new Padding(0, 3, 3, 3);
            tbPhilosopher3State.Name = "tbPhilosopher3State";
            tbPhilosopher3State.ReadOnly = true;
            tbPhilosopher3State.Size = new Size(235, 16);
            tbPhilosopher3State.TabIndex = 13;
            // 
            // tbPhilosopher4State
            // 
            tbPhilosopher4State.BorderStyle = BorderStyle.None;
            tbPhilosopher4State.Dock = DockStyle.Fill;
            tbPhilosopher4State.Location = new Point(238, 194);
            tbPhilosopher4State.Margin = new Padding(0, 3, 3, 3);
            tbPhilosopher4State.Name = "tbPhilosopher4State";
            tbPhilosopher4State.ReadOnly = true;
            tbPhilosopher4State.Size = new Size(235, 16);
            tbPhilosopher4State.TabIndex = 14;
            // 
            // tbPhilosopher5State
            // 
            tbPhilosopher5State.BorderStyle = BorderStyle.None;
            tbPhilosopher5State.Dock = DockStyle.Fill;
            tbPhilosopher5State.Location = new Point(238, 216);
            tbPhilosopher5State.Margin = new Padding(0, 3, 3, 3);
            tbPhilosopher5State.Name = "tbPhilosopher5State";
            tbPhilosopher5State.ReadOnly = true;
            tbPhilosopher5State.Size = new Size(235, 16);
            tbPhilosopher5State.TabIndex = 15;
            // 
            // lblLog
            // 
            lblLog.AutoSize = true;
            tableLayoutPanel.SetColumnSpan(lblLog, 2);
            lblLog.Dock = DockStyle.Fill;
            lblLog.Location = new Point(3, 235);
            lblLog.Name = "lblLog";
            lblLog.Size = new Size(470, 15);
            lblLog.TabIndex = 16;
            lblLog.Text = "Лог";
            // 
            // lbLog
            // 
            tableLayoutPanel.SetColumnSpan(lbLog, 2);
            lbLog.Dock = DockStyle.Fill;
            lbLog.FormattingEnabled = true;
            lbLog.IntegralHeight = false;
            lbLog.ItemHeight = 15;
            lbLog.Location = new Point(3, 253);
            lbLog.Name = "lbLog";
            lbLog.ScrollAlwaysVisible = true;
            lbLog.SelectionMode = SelectionMode.None;
            lbLog.Size = new Size(470, 219);
            lbLog.TabIndex = 17;
            // 
            // SettingsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(tableLayoutPanel);
            Name = "SettingsControl";
            Size = new Size(476, 475);
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            flowLayoutPanelButtons.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private Label lblStartMode;
        private ComboBox cbStartMode;
        private FlowLayoutPanel flowLayoutPanelButtons;
        private Button BtnStart;
        private Button BtnStop;
        private Label lblSpeedFactor;
        private ComboBox cmbSpeedFactor;
        private Label lblStates;
        private Label lblPhilosopher1State;
        private TextBox tbPhilosopher1State;
        private Label lblPhilosopher2State;
        private Label lblPhilosopher3State;
        private Label lblPhilosopher4State;
        private Label lblPhilosopher5State;
        private TextBox tbPhilosopher2State;
        private TextBox tbPhilosopher3State;
        private TextBox tbPhilosopher4State;
        private TextBox tbPhilosopher5State;
        private Label lblLog;
        private ListBox lbLog;
    }
}
