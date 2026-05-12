using System.ComponentModel;

namespace Demo.Philosophers_Deadlock
{
    public partial class SettingsControl : UserControl
    {
        private readonly BindingList<Common.LogEntry> _logEntries = [];

        public event EventHandler? StartClicked;
        public event EventHandler? StopClicked;

        public SettingsControl()
        {
            InitializeComponent();
            lbLog.DataSource = _logEntries;
            cmbSpeedFactor.SelectedIndex = 0;
            cbStartMode.SelectedIndex = 0;
        }

        public void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => Log(message));
                return;
            }

            _logEntries.Insert(0, new Common.LogEntry(message));
            if (_logEntries.Count > 100)
                _logEntries.RemoveAt(_logEntries.Count - 1);

            lbLog.TopIndex = 0;
        }

        public void ClearLog()
        {
            if (InvokeRequired)
            {
                Invoke(ClearLog);
                return;
            }

            _logEntries.Clear();
        }
        
        public void UpdatePhilosopherState(int index, string state)
        {
            var tb = index switch
            {
                0 => tbPhilosopher1State,
                1 => tbPhilosopher2State,
                2 => tbPhilosopher3State,
                3 => tbPhilosopher4State,
                4 => tbPhilosopher5State,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };

            if (tb.InvokeRequired)
                tb.Invoke(() => tb.Text = state);
            else
                tb.Text = state;
        }

        public int SpeedMultiplier => cmbSpeedFactor.SelectedIndex + 1;

        public bool IsRandomStart => cbStartMode.SelectedIndex == 1;

        private void BtnStart_Click(object sender, EventArgs e)
        {
            StartClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
