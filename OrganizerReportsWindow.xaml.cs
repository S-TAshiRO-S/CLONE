using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace EAccess.Client
{
    public partial class OrganizerReportsWindow : Window
    {
        public ObservableCollection<PurchaseEntry> PurchaseEntries { get; } = new();

        private readonly int _eventId;
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;

        private readonly string? _connectionString;

        private decimal _allocated;
        private decimal _reserved;

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public OrganizerReportsWindow(int eventId, string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
        {
            InitializeComponent();
            DataContext = this;

            _eventId = eventId;
            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;
            _userId = userId;

            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;
            UserFullNameTextBlock.Text = userFullName;

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadBudget();
            LoadPurchaseEntries();
            UpdateBudgetFields();
        }

        private void LoadBudget()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using (var cmd = new SqlCommand("SELECT Budget FROM Events WHERE EventID = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", _eventId);
                var val = cmd.ExecuteScalar();
                _allocated = val == null || val == DBNull.Value ? 0m : Convert.ToDecimal(val);
            }

            const string reservedSql = @"
SELECT ISNULL(SUM(CAST(pi.Quantity AS decimal(18,2)) * pi.Price), 0)
FROM PurchaseItems pi
JOIN PurchaseStatuses ps ON ps.StatusID = pi.StatusID
WHERE pi.EventID = @id AND ps.StatusName = N'Куплено';";

            using (var cmd = new SqlCommand(reservedSql, connection))
            {
                cmd.Parameters.AddWithValue("@id", _eventId);
                var val = cmd.ExecuteScalar();
                _reserved = val == null || val == DBNull.Value ? 0m : Convert.ToDecimal(val);
            }
        }

        private void LoadPurchaseEntries()
        {
            PurchaseEntries.Clear();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            const string sql = @"
SELECT pi.PurchaseItemID, pi.Name, pi.Supplier, pi.Quantity, pi.Price, ps.StatusName
FROM PurchaseItems pi
JOIN PurchaseStatuses ps ON ps.StatusID = pi.StatusID
WHERE pi.EventID = @id
ORDER BY pi.PurchaseItemID DESC;";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", _eventId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                PurchaseEntries.Add(new PurchaseEntry
                {
                    PurchaseItemId = reader.GetInt32(reader.GetOrdinal("PurchaseItemID")),
                    Name = reader["Name"] as string ?? string.Empty,
                    Supplier = reader["Supplier"] as string ?? string.Empty,
                    Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                    Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                    StatusName = reader["StatusName"] as string ?? string.Empty
                });
            }
        }

        private void UpdateBudgetFields()
        {
            AllocatedTextBox.Text = _allocated.ToString("N0", Ru);
            ReservedTextBox.Text = _reserved.ToString("N0", Ru);
            RemainingTextBox.Text = (_allocated - _reserved).ToString("N0", Ru);
        }

        private List<string> LoadStatuses()
        {
            var list = new List<string>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var cmd = new SqlCommand("SELECT StatusName FROM PurchaseStatuses ORDER BY StatusID", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(reader["StatusName"] as string ?? "");

            return list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private void BtnCreateReport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            var statuses = LoadStatuses();

            var dlg = new OrganizerReportOptionsWindow(statuses, PurchaseDataGrid.SelectedItems.Count > 0)
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true)
                return;

            List<PurchaseEntry> items;

            if (dlg.Mode == OrganizerReportMode.All)
            {
                items = PurchaseEntries.ToList();
            }
            else if (dlg.Mode == OrganizerReportMode.Selected)
            {
                items = PurchaseDataGrid.SelectedItems.Cast<PurchaseEntry>().ToList();
                if (items.Count == 0)
                {
                    MessageBox.Show("Выберите строки в таблице.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                var status = dlg.SelectedStatus ?? "";
                items = PurchaseEntries.Where(x => x.StatusName == status).ToList();
            }

            if (items.Count == 0)
            {
                MessageBox.Show("Нет данных для отчёта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EAccessExports");
                Directory.CreateDirectory(dir);

                var generatedAt = DateTime.Now;
                var ts = generatedAt.ToString("ddMMyyyy_HHmmss");

                var kind =
                    dlg.Mode == OrganizerReportMode.All ? "Все" :
                    dlg.Mode == OrganizerReportMode.Selected ? "Выбранные" :
                    $"Статус_{SanitizeFilePart(dlg.SelectedStatus ?? "Неизвестно")}";

                var baseName = $"Отчёт_Смета_{kind}_{ts}";

                var pdfPath = Path.Combine(dir, baseName + ".pdf");
                var csvPath = Path.Combine(dir, baseName + ".csv");

                var includeBudgetLines = dlg.Mode == OrganizerReportMode.All;

                ExportCsv(csvPath, items, generatedAt, includeBudgetLines);
                ExportPdf(pdfPath, items, generatedAt);

                MessageBox.Show($"Отчёты сохранены:\n{pdfPath}\n{csvPath}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания отчёта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string SanitizeFilePart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Пусто";

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(text.Where(ch => !invalid.Contains(ch)).ToArray());
            cleaned = cleaned.Trim().Replace(' ', '_');

            return string.IsNullOrWhiteSpace(cleaned) ? "Пусто" : cleaned;
        }

        private void ExportCsv(string path, List<PurchaseEntry> items, DateTime generatedAt, bool includeBudgetLines)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Отчёт по смете: {_eventName}");
            sb.AppendLine($"Дата формирования: {generatedAt:dd.MM.yyyy HH:mm}");

            if (includeBudgetLines)
            {
                sb.AppendLine($"Выделено: {_allocated.ToString("N0", Ru)} ₽");
                sb.AppendLine($"Зарезервировано: {_reserved.ToString("N0", Ru)} ₽");
                sb.AppendLine($"Осталось: {(_allocated - _reserved).ToString("N0", Ru)} ₽");
            }

            sb.AppendLine();

            sb.AppendLine("Сводка по статусам:");
            sb.AppendLine("Статус;Кол-во (ед.);Сумма");

            var summary = items
                .GroupBy(x => x.StatusName)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Sum = g.Sum(x => x.Total)
                })
                .OrderBy(x => x.Status)
                .ToList();

            foreach (var s in summary)
                sb.AppendLine($"{Escape(s.Status)};{s.Count};{s.Sum.ToString("N0", Ru)} ₽");

            sb.AppendLine();

            sb.AppendLine("Позиции:");
            sb.AppendLine("Наименование;Кол-во (ед.);Цена;Поставщик;Сумма;Статус");

            foreach (var it in items)
            {
                sb.AppendLine($"{Escape(it.Name)};{it.Quantity};{it.Price.ToString("N0", Ru)} ₽;{Escape(it.Supplier)};{it.Total.ToString("N0", Ru)} ₽;{Escape(it.StatusName)}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string s)
        {
            s ??= "";
            if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private void ExportPdf(string path, List<PurchaseEntry> items, DateTime generatedAt)
        {
            var summary = items
                .GroupBy(x => x.StatusName)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Sum = g.Sum(x => x.Total)
                })
                .OrderBy(x => x.Status)
                .ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Отчёт по смете: {_eventName}").FontSize(18).Bold();
                        col.Item().Text($"Дата формирования: {generatedAt:dd.MM.yyyy HH:mm}").FontSize(12);

                        col.Item().PaddingVertical(8).Row(r =>
                        {
                            r.RelativeItem().Text($"Выделено: {_allocated.ToString("N0", Ru)} ₽");
                            r.RelativeItem().Text($"Зарезервировано: {_reserved.ToString("N0", Ru)} ₽");
                            r.RelativeItem().Text($"Осталось: {(_allocated - _reserved).ToString("N0", Ru)} ₽");
                        });

                        col.Item().PaddingTop(6).Text("Сводка по статусам:").Bold();

                        col.Item().Border(1).BorderColor(Colors.Grey.Darken2).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.ConstantColumn(140);
                                c.ConstantColumn(180);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Статус").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Кол-во (ед.)").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Сумма").Bold().AlignCenter();
                            });

                            foreach (var s in summary)
                            {
                                t.Cell().Element(BodyCell).Text(s.Status);
                                t.Cell().Element(BodyCell).AlignCenter().Text(s.Count.ToString());
                                t.Cell().Element(BodyCell).AlignRight().Text($"{s.Sum.ToString("N0", Ru)} ₽");
                            }
                        });

                        col.Item().PaddingTop(10).Text("Позиции:").Bold();

                        col.Item().Border(1).BorderColor(Colors.Grey.Darken2).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.ConstantColumn(110);
                                c.ConstantColumn(130);
                                c.RelativeColumn(2);
                                c.ConstantColumn(150);
                                c.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Наименование").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Кол-во (ед.)").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Цена").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Поставщик").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Сумма").Bold().AlignCenter();
                                h.Cell().Element(HeaderCell).Text("Статус").Bold().AlignCenter();
                            });

                            foreach (var it in items)
                            {
                                t.Cell().Element(BodyCell).Text(it.Name);
                                t.Cell().Element(BodyCell).AlignCenter().Text(it.Quantity.ToString());
                                t.Cell().Element(BodyCell).AlignRight().Text($"{it.Price.ToString("N0", Ru)} ₽");
                                t.Cell().Element(BodyCell).Text(it.Supplier);
                                t.Cell().Element(BodyCell).AlignRight().Text($"{it.Total.ToString("N0", Ru)} ₽");
                                t.Cell().Element(BodyCell).Text(it.StatusName);
                            }
                        });
                    });
                });
            }).GeneratePdf(path);
        }

        private static IContainer HeaderCell(IContainer c) =>
            c.Background(Colors.White)
             .Border(1)
             .BorderColor(Colors.Grey.Darken2)
             .Padding(4)
             .AlignMiddle();

        private static IContainer BodyCell(IContainer c) =>
            c.Background(Colors.White)
             .Border(1)
             .BorderColor(Colors.Grey.Darken2)
             .Padding(4)
             .AlignMiddle();

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerEventWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnBudget_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerBudgetWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage { Owner = this };
            popup.ShowDialog();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerAuditWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }
    }
}