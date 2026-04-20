using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EAccess.Client
{
    public partial class OrganizerAnalyticsWindow : Window
    {
        private readonly decimal _allocated;
        private readonly decimal _reserved;
        private readonly List<PurchaseEntry> _entries;

        private static readonly string[] ChartColors = { "#F54B64", "#F78361", "#2ECA8E", "#5B8CFF", "#FFD166" };
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public OrganizerAnalyticsWindow(decimal allocated, decimal reserved, List<PurchaseEntry> entries)
        {
            InitializeComponent();
            _allocated = allocated;
            _reserved = reserved;
            _entries = entries;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var remaining = Math.Max(0m, _allocated - _reserved);

            var budgetData = new List<(string Label, double Value)>
            {
                ($"Потрачено: {_reserved.ToString("N0", Ru)} ₽", (double)_reserved),
                ($"Осталось: {remaining.ToString("N0", Ru)} ₽",  (double)remaining)
            };
            DrawPieChart(BudgetChartCanvas, BudgetLegend, budgetData, ChartColors);

            var statusData = _entries
                .GroupBy(x => x.StatusName)
                .OrderByDescending(g => g.Count())
                .Select(g => ($"{g.Key}: {g.Count()} поз.", (double)g.Count()))
                .ToList();
            DrawPieChart(StatusChartCanvas, StatusLegend, statusData, ChartColors);
        }

        private static void DrawPieChart(Canvas canvas, StackPanel legend,
            IReadOnlyList<(string Label, double Value)> data, string[] colors)
        {
            canvas.Children.Clear();
            legend.Children.Clear();

            double total = data.Sum(d => d.Value);

            if (total <= 0)
            {
                var tb = new TextBlock
                {
                    Text = "Нет данных",
                    Foreground = Brushes.White,
                    FontSize = 16
                };
                canvas.Children.Add(tb);
                Canvas.SetLeft(tb, 40);
                Canvas.SetTop(tb, 90);
                return;
            }

            double cx = 100, cy = 100, r = 90;
            double startAngle = -90.0;

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Value <= 0) continue;

                double sweep = data[i].Value / total * 360.0;
                var color = (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]);
                var brush = new SolidColorBrush(color);

                canvas.Children.Add(CreateSlice(cx, cy, r, startAngle, sweep, brush));
                startAngle += sweep;

                double pct = data[i].Value / total * 100.0;
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                row.Children.Add(new Rectangle
                {
                    Width = 14, Height = 14,
                    Fill = brush,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = $"{data[i].Label} ({pct:N0}%)",
                    Foreground = Brushes.White,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                });
                legend.Children.Add(row);
            }
        }

        private static Path CreateSlice(double cx, double cy, double r,
            double startDeg, double sweepDeg, Brush fill)
        {
            if (sweepDeg >= 360) sweepDeg = 359.99;

            double s = startDeg * Math.PI / 180.0;
            double e = (startDeg + sweepDeg) * Math.PI / 180.0;

            var fig = new PathFigure
            {
                StartPoint = new Point(cx, cy),
                IsClosed = true
            };
            fig.Segments.Add(new LineSegment(
                new Point(cx + r * Math.Cos(s), cy + r * Math.Sin(s)), false));
            fig.Segments.Add(new ArcSegment(
                new Point(cx + r * Math.Cos(e), cy + r * Math.Sin(e)),
                new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, false));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            return new Path { Data = geo, Fill = fill };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
