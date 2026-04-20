using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EAccess.Client
{
    public partial class SecurityAnalyticsWindow : Window
    {
        private readonly List<AccessEntry> _entries;

        private static readonly string[] ChartColors = { "#F54B64", "#F78361", "#2ECA8E", "#5B8CFF", "#FFD166" };

        public SecurityAnalyticsWindow(List<AccessEntry> entries)
        {
            InitializeComponent();
            _entries = entries;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var positionData = _entries
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Position) ? "Не указана" : x.Position)
                .OrderByDescending(g => g.Count())
                .Select(g => ($"{g.Key}: {g.Count()} чел.", (double)g.Count()))
                .ToList();
            DrawPieChart(PositionChartCanvas, PositionLegend, positionData, ChartColors);
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
                Canvas.SetLeft(tb, 50);
                Canvas.SetTop(tb, 100);
                return;
            }

            double cx = 110, cy = 110, r = 100;
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
