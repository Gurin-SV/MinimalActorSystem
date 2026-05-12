namespace Demo.Common.Philosophers
{
    partial class TabPageControl
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
            SplitContainer = new SplitContainer();
            PhilosophersPaintControl = new PaintControl();
            ((System.ComponentModel.ISupportInitialize)SplitContainer).BeginInit();
            SplitContainer.Panel1.SuspendLayout();
            SplitContainer.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            SplitContainer.Dock = DockStyle.Fill;
            SplitContainer.Location = new Point(0, 0);
            SplitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            SplitContainer.Panel1.Controls.Add(PhilosophersPaintControl);
            SplitContainer.Size = new Size(600, 351);
            SplitContainer.SplitterDistance = 300;
            SplitContainer.TabIndex = 0;
            // 
            // philosophersPaintControl
            // 
            PhilosophersPaintControl.BackColor = Color.White;
            PhilosophersPaintControl.BorderStyle = BorderStyle.FixedSingle;
            PhilosophersPaintControl.CausesValidation = false;
            PhilosophersPaintControl.Dock = DockStyle.Fill;
            PhilosophersPaintControl.Location = new Point(0, 0);
            PhilosophersPaintControl.Name = "philosophersPaintControl";
            PhilosophersPaintControl.Size = new Size(300, 351);
            PhilosophersPaintControl.Snapshot = null;
            PhilosophersPaintControl.TabIndex = 0;
            // 
            // PhilosophersTabPage
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(SplitContainer);
            Name = "PhilosophersTabPage";
            Size = new Size(600, 351);
            SplitContainer.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)SplitContainer).EndInit();
            SplitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        public SplitContainer SplitContainer;
        public PaintControl PhilosophersPaintControl;
    }
}
