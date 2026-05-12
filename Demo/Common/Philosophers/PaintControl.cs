namespace Demo.Common.Philosophers;

public partial class PaintControl : UserControl
{
    private PhilosophersSnapshot? _snapshot;

    public PhilosophersSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            Invalidate();
        }
    }

    public PaintControl()
    {
        InitializeComponent();
        SetStyle(ControlStyles.ResizeRedraw, true); // перерисовка при изменении размера
    }

    private void PhilosophersPaintControl_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.WhiteSmoke);

        if (Snapshot == null)
            return;

        var cx = Width / 2f;
        var cy = Height / 2f;
        var tableR = Math.Min(Width, Height) * 0.2f;
        var philosopherR = Math.Min(Width, Height) * 0.35f;
        var headR = 20f;

        // Стол
        g.FillEllipse(Brushes.Moccasin, cx - tableR, cy - tableR, tableR * 2, tableR * 2);

        for (int i = 0; i < 5; i++)
        {
            var angle = i * 72.0 - 90;
            var rad = angle * Math.PI / 180.0;
            var px = cx + philosopherR * (float)Math.Cos(rad) - headR;
            var py = cy + philosopherR * (float)Math.Sin(rad) - headR;

            var color = Snapshot.Philosophers[i] switch
            {
                State.Thinking => Color.LightBlue,
                State.WaitingForLeftFork => Color.Yellow,
                State.WaitingForRightFork => Color.Orange,
                State.Eating => Color.LightGreen,
                State.Starving => Color.Red,
                _ => Color.Gray
            };

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, px, py, headR * 2, headR * 2);
            g.DrawEllipse(Pens.Black, px, py, headR * 2, headR * 2);
            g.DrawString($"Ф{i + 1}", Font, Brushes.Black, px + 5, py + 5);

            // Вилка между философом i и i+1
            DrawFork(g, cx, cy, philosopherR, i);
        }

        // Легенда
        DrawLegend(g);
    }

    private void DrawFork(Graphics g, float cx, float cy, float philosopherR, int i)
    {
        // Вилка находится между философом i и философом (i+1)%5
        var angle = i * 72.0 + 36.0 - 90; // середина между двумя философами
        var rad = angle * Math.PI / 180.0;
        var forkR = philosopherR * 0.75f;
        var x = cx + forkR * (float)Math.Cos(rad);
        var y = cy + forkR * (float)Math.Sin(rad);

        var forkLength = 24f;  // было 16
        var forkWidth = 6f;    // было 4

        var forkAngle = angle + 90;
        var forkRad = forkAngle * Math.PI / 180.0;
        var dx = forkLength * (float)Math.Cos(forkRad);
        var dy = forkLength * (float)Math.Sin(forkRad);

        var color = Snapshot!.Forks[i] == ForkState.Free
            ? Color.LightGray
            : Color.SteelBlue;

        using var pen = new Pen(color, forkWidth);
        g.DrawLine(pen, x - dx / 2, y - dy / 2, x + dx / 2, y + dy / 2);
    }

    private void DrawLegend(Graphics g)
    {
        var items = new (string Text, Color Color)[]
        {
        ("Думает", Color.LightBlue),
        ("Ждёт левую", Color.Yellow),
        ("Ждёт правую", Color.Orange),
        ("Ест", Color.LightGreen),
        ("Голодает", Color.Red),
        };

        var x = 10f;
        var y = Height - 25f;
        var boxSize = 12f;
        var spacing = 8f;
        var fontSize = 8f;

        using var font = new Font(Font.FontFamily, fontSize);

        for (int i = 0; i < items.Length; i++)
        {
            using var brush = new SolidBrush(items[i].Color);
            g.FillRectangle(brush, x, y, boxSize, boxSize);
            g.DrawRectangle(Pens.Black, x, y, boxSize, boxSize);

            g.DrawString(items[i].Text, font, Brushes.Black, x + boxSize + 2, y - 1);

            x += boxSize + spacing + g.MeasureString(items[i].Text, font).Width + 4;
        }

        // Вилка
        x += 10;
        using var forkPen = new Pen(Color.LightGray, 3f);
        g.DrawLine(forkPen, x, y + boxSize / 2, x + 14, y + boxSize / 2);
        g.DrawString("Свободна", font, Brushes.Black, x + 16, y - 1);

        x += 70;
        using var busyPen = new Pen(Color.SteelBlue, 3f);
        g.DrawLine(busyPen, x, y + boxSize / 2, x + 14, y + boxSize / 2);
        g.DrawString("Занята", font, Brushes.Black, x + 16, y - 1);
    }
}
