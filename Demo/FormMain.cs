using System.Reflection;

namespace Demo
{
    public partial class FormMain : Form
    {
        private readonly List<IDemoAlgorithm> _algorithms = [];
        private IDemoAlgorithm? _activeAlgorithm;

        public FormMain()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var algorithmTypes = Assembly.GetExecutingAssembly()
                 .GetTypes()
                 .Where(t => !t.IsAbstract && typeof(IDemoAlgorithm).IsAssignableFrom(t));

            List<IDemoAlgorithm> list = [];
            foreach (var type in algorithmTypes)
            {
                list.Add((IDemoAlgorithm)Activator.CreateInstance(type)!);
            }
            _algorithms.AddRange(list.OrderBy(t => t.Index));

            foreach (var algorithm in _algorithms)
            {
                var tabPage = new TabPage(algorithm.Name)
                {
                    ToolTipText = algorithm.Description
                };
                _tabControl.TabPages.Add(tabPage);
            }

            // Активируем первую вкладку, если есть
            if (_tabControl.TabPages.Count > 0)
            {
                _tabControl.SelectedIndex = 0;
                ShowActiveAlgorithm();
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowActiveAlgorithm();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _activeAlgorithm?.Update();
        }

        private void ShowActiveAlgorithm()
        {
            if (_tabControl.SelectedIndex < 0 || _tabControl.SelectedIndex >= _algorithms.Count)
            {
                _activeAlgorithm = null;
                return;
            }

            var algorithm = _algorithms[_tabControl.SelectedIndex];
            var page = _tabControl.SelectedTab!;

            algorithm.Show(page);
            _activeAlgorithm = algorithm;
        }
    }
}
