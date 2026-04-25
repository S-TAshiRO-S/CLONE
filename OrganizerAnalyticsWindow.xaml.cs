using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EAccess.Client
{
    public partial class OrganizerAnalyticsWindow : Window
    {
        private readonly decimal _allocated;
        private readonly decimal _reserved;
        private readonly List<PurchaseEntry> _entries;

        private string _chartType = "pie";
        private List<(string Label, double Value)>? _budgetData;
        private List<(string Label, double Value)>? _statusData;

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

            _budgetData = new List<(string Label, double Value)>
            {
                ($"Потрачено: {_reserved.ToString("N0", Ru)} ₽", (double)_reserved),
                ($"Осталось: {remaining.ToString("N0", Ru)} ₽",  (double)remaining)
            };

            _statusData = _entries
                .GroupBy(x => x.StatusName)
                .OrderByDescending(g => g.Count())
                .Select(g => ($"{g.Key}: {g.Count()} поз.", (double)g.Count()))
                .ToList();

            RedrawAll();
        }

        private void RedrawAll()
        {
            if (_budgetData == null || _statusData == null) return;

            switch (_chartType)
            {
                case "bar":
                    DrawBarChart(BudgetChartCanvas, BudgetLegend, _budgetData, ChartColors);
                    DrawBarChart(StatusChartCanvas, StatusLegend, _statusData, ChartColors);
                    break;
                case "strip":
                    DrawStripChart(BudgetChartCanvas, BudgetLegend, _budgetData, ChartColors);
                    DrawStripChart(StatusChartCanvas, StatusLegend, _statusData, ChartColors);
                    break;
                default:
                    DrawPieChart(BudgetChartCanvas, BudgetLegend, _budgetData, ChartColors);
                    DrawPieChart(StatusChartCanvas, StatusLegend, _statusData, ChartColors);
                    break;
            }
        }

        private void ChartType_Changed(object sender, RoutedEventArgs e)
        {
            if (RbPie.IsChecked == true)        _chartType = "pie";
            else if (RbBar.IsChecked == true)   _chartType = "bar";
            else if (RbStrip.IsChecked == true) _chartType = "strip";
            RedrawAll();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Жду пока всё отрисуется
                AnalyticsCard.UpdateLayout();

                var rtb = new RenderTargetBitmap(
                    (int)AnalyticsCard.ActualWidth,
                    (int)AnalyticsCard.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);

                // Рендерю только карточку без фона окна
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var vb = new VisualBrush(AnalyticsCard);
                    dc.DrawRectangle(vb, null, new Rect(0, 0, AnalyticsCard.ActualWidth, AnalyticsCard.ActualHeight));
                }
                rtb.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EAccessExports");
                Directory.CreateDirectory(dir);

                var ts = DateTime.Now.ToString("ddMMyyyy_HHmmss");
                var filePath = System.IO.Path.Combine(dir, $"Аналитика_Смета_{ts}.png");

                using var fs = File.Create(filePath);
                encoder.Save(fs);

                MessageBox.Show($"Сохранено:\n{filePath}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void DrawPieChart(Canvas canvas, StackPanel legend,
            IReadOnlyList<(string Label, double Value)> data, string[] colors)
        {
            canvas.Children.Clear();
            legend.Children.Clear();

            double total = data.Sum(d => d.Value);

            if (total <= 0)
            {
                var tb = new TextBlock { Text = "Нет данных", Foreground = Brushes.White, FontSize = 16 };
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
                AddLegendRow(legend, brush, $"{data[i].Label} ({pct:N2}%)");
            }
        }

        private static void DrawBarChart(Canvas canvas, StackPanel legend,
            IReadOnlyList<(string Label, double Value)> data, string[] colors)
        {
            canvas.Children.Clear();
            legend.Children.Clear();

            double total = data.Sum(d => d.Value);
            if (total <= 0)
            {
                AddNoData(canvas, 40, 90);
                return;
            }

            double cw = double.IsNaN(canvas.Width)  ? 200 : canvas.Width;
            double ch = double.IsNaN(canvas.Height) ? 200 : canvas.Height;
            double maxVal = data.Where(d => d.Value > 0).Max(d => d.Value);

            int count = data.Count(d => d.Value > 0);
            double topPad = 22, botPad = 28;
            double usableH = ch - topPad - botPad;
            double barW = Math.Max(10, (cw - 16) / count - 6);
            double totalW = count * barW + (count - 1) * 6;
            double startX = (cw - totalW) / 2;

            int idx = 0;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Value <= 0) continue;

                var color = (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]);
                var brush = new SolidColorBrush(color);

                double barH = data[i].Value / maxVal * usableH;
                double x = startX + idx * (barW + 6);
                double y = topPad + (usableH - barH);

                var rect = new Rectangle { Width = barW, Height = Math.Max(1, barH), Fill = brush };
                canvas.Children.Add(rect);
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);

                double pct = data[i].Value / total * 100.0;
                var pctLabel = new TextBlock
                {
                    Text = $"{pct:N2}%",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    Width = barW
                };
                canvas.Children.Add(pctLabel);
                Canvas.SetLeft(pctLabel, x);
                Canvas.SetTop(pctLabel, Math.Max(0, y - 18));

                var raw = data[i].Label;
                var shortName = raw.Length > 7 ? raw.Substring(0, 7) : raw;
                var nameLabel = new TextBlock
                {
                    Text = shortName,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    Width = barW + 10,
                    TextWrapping = TextWrapping.NoWrap
                };
                canvas.Children.Add(nameLabel);
                Canvas.SetLeft(nameLabel, x - 5);
                Canvas.SetTop(nameLabel, ch - botPad + 4);

                AddLegendRow(legend, brush, $"{data[i].Label} ({pct:N2}%)");
                idx++;
            }
        }

        private static void DrawStripChart(Canvas canvas, StackPanel legend,
            IReadOnlyList<(string Label, double Value)> data, string[] colors)
        {
            canvas.Children.Clear();
            legend.Children.Clear();

            double total = data.Sum(d => d.Value);
            if (total <= 0)
            {
                AddNoData(canvas, 40, 90);
                return;
            }

            double cw = double.IsNaN(canvas.Width)  ? 200 : canvas.Width;
            double ch = double.IsNaN(canvas.Height) ? 200 : canvas.Height;

            int count = data.Count(d => d.Value > 0);
            double gap = 4;
            double stripH = Math.Max(8, (ch - (count - 1) * gap - 10) / count);
            double maxBarW = cw - 44;

            double y = 5;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Value <= 0) continue;

                var color = (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]);
                var brush = new SolidColorBrush(color);

                double pct = data[i].Value / total * 100.0;
                double barW = Math.Max(2, data[i].Value / total * maxBarW);

                var rect = new Rectangle { Width = barW, Height = stripH, Fill = brush };
                canvas.Children.Add(rect);
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, y);

                var pctLabel = new TextBlock
                {
                    Text = $"{pct:N2}%",
                    Foreground = Brushes.White,
                    FontSize = 11
                };
                canvas.Children.Add(pctLabel);
                Canvas.SetLeft(pctLabel, barW + 4);
                Canvas.SetTop(pctLabel, y + (stripH - 14) / 2);

                AddLegendRow(legend, brush, $"{data[i].Label} ({pct:N2}%)");
                y += stripH + gap;
            }
        }

        private static void AddLegendRow(StackPanel legend, Brush brush, string text)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new Rectangle
            {
                Width = 14, Height = 14,
                Fill = brush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            legend.Children.Add(row);
        }

        private static void AddNoData(Canvas canvas, double left, double top)
        {
            var tb = new TextBlock { Text = "Нет данных", Foreground = Brushes.White, FontSize = 16 };
            canvas.Children.Add(tb);
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
        }

        private static System.Windows.Shapes.Path CreateSlice(double cx, double cy, double r,
            double startDeg, double sweepDeg, Brush fill)
        {
            if (sweepDeg >= 360) sweepDeg = 359.99;

            double s = startDeg * Math.PI / 180.0;
            double e = (startDeg + sweepDeg) * Math.PI / 180.0;

            var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
            fig.Segments.Add(new LineSegment(
                new Point(cx + r * Math.Cos(s), cy + r * Math.Sin(s)), false));
            fig.Segments.Add(new ArcSegment(
                new Point(cx + r * Math.Cos(e), cy + r * Math.Sin(e)),
                new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, false));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            return new System.Windows.Shapes.Path { Data = geo, Fill = fill };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
