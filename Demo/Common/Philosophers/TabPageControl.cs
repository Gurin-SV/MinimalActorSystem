namespace Demo.Common.Philosophers
{
    public partial class TabPageControl : UserControl
    {
        public TabPageControl()
        {
            InitializeComponent();
        }

        public void SetSettingsControl(UserControl control)
        {
            SplitContainer.Panel2.Controls.Add(control);
            control.Dock = DockStyle.Fill;
        }
    }
}
