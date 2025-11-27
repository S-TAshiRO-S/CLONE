using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfContainer = QuestPDF.Infrastructure.IContainer;

namespace EAccess.Client
{
    public partial class SecurityReportsWindow : Window, INotifyPropertyChanged
    {
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly string? _connectionString;

        private readonly List<AccessEntry> _allEntries = new();

        public ObservableCollection<AccessEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> ReportModes { get; } = new(new[]
        {
            "Общий список",
            "По периоду аккредитации",
            "По должностям"
        });

        public ObservableCollection<PositionFilterItem> PositionFilters { get; } = new();

        private string _selectedReportMode = "Общий список";
        public string SelectedReportMode
        {
            get => _selectedReportMode;
            set
            {
                if (_selectedReportMode == value)
                {
                    return;
                }

                _selectedReportMode = value;
                OnPropertyChanged(nameof(SelectedReportMode));
                UpdateControlAvailability();
                ApplyFilters();
            }
        }

        private DateTime? _startDateFilter;
        public DateTime? StartDateFilter
        {
            get => _startDateFilter;
            set
            {
                if (_startDateFilter == value)
                {
                    return;
                }

                _startDateFilter = value;
                OnPropertyChanged(nameof(StartDateFilter));
                ApplyFilters();
            }
        }

        private DateTime? _endDateFilter;
        public DateTime? EndDateFilter
        {
            get => _endDateFilter;
            set
            {
                if (_endDateFilter == value)
                {
                    return;
                }

                _endDateFilter = value;
                OnPropertyChanged(nameof(EndDateFilter));
                ApplyFilters();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public SecurityReportsWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();
            DataContext = this;

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;
            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;

            UserFullNameTextBlock.Text = userFullName;

            LoadAccessEntries();
            PopulatePositionFilters();
            UpdatePositionsSummary();
            UpdateControlAvailability();
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new SecurityMainWindow(_eventName, _startDate, _endDate, _location, _userFullName);
            mainWindow.Show();
            Close();
        }

        private void BtnAccessList_Click(object sender, RoutedEventArgs e)
        {
            var accessListWindow = new SecurityAccessListWindow(_eventName, _startDate, _endDate, _location, _userFullName);
            accessListWindow.Show();
            Close();
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnControlAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Контроль / Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var entries = FilteredEntries.ToList();

            if (!entries.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EAccessExports");
                Directory.CreateDirectory(exportFolder);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var baseFileName = $"Report_{timestamp}";

                var csvPath = Path.Combine(exportFolder, baseFileName + ".csv");
                var pdfPath = Path.Combine(exportFolder, baseFileName + ".pdf");

                ExportToCsv(csvPath, entries);
                ExportToPdf(pdfPath, entries);

                MessageBox.Show($"Экспорт завершён.\nPDF: {pdfPath}\nExcel (CSV): {csvPath}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReportModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportModeComboBox.SelectedItem is string selectedMode)
            {
                SelectedReportMode = selectedMode;
            }
        }

        private void TodayOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (SelectedReportMode != "По периоду аккредитации")
            {
                return;
            }

            if (TodayOnlyCheckBox.IsChecked == true)
            {
                SetDateTextBoxes(DateTime.Today, DateTime.Today);
            }
            else
            {
                ClearDateFields();
            }
        }

        private void DateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private void StartDateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleDateTextChange(StartDateTextBox, isStart: true);
        }

        private void EndDateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleDateTextChange(EndDateTextBox, isStart: false);
        }

        private void PositionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedReportMode != "По должностям")
            {
                return;
            }

            UpdatePositionsSummary();
            ApplyFilters();
            PositionsComboBox.IsDropDownOpen = true;
        }

        private void ClearPositionsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var position in PositionFilters)
            {
                position.IsSelected = false;
            }

            UpdatePositionsSummary();
            ApplyFilters();
        }

        private void PositionsComboBox_DropDownClosed(object sender, EventArgs e)
        {
            UpdatePositionsSummary();
        }

        private static void ExportToCsv(string csvPath, List<AccessEntry> entries)
        {
            var lines = new List<string>
            {
                "Фамилия;Имя;Отчество;Телефон;Паспорт;Должность;Аккредитация"
            };

            lines.AddRange(entries.Select(e =>
                string.Join(';',
                    EscapeCsv(e.LastName),
                    EscapeCsv(e.FirstName),
                    EscapeCsv(e.MiddleName ?? string.Empty),
                    EscapeCsvAsText(e.Phone),
                    EscapeCsvAsText(e.Passport),
                    EscapeCsv(e.Position),
                    EscapeCsv($"с {e.AccredStart:dd.MM.yyyy} по {e.AccredEnd:dd.MM.yyyy}"))));

            File.WriteAllLines(csvPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(';'))
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }

            return value;
        }

        private static string EscapeCsvAsText(string value)
        {
            // Форматируем как строку Excel, чтобы не терялись ведущие нули и не включалась экспонента
            var escaped = EscapeCsv(value);
            if (!escaped.StartsWith('"'))
            {
                escaped = '"' + escaped + '"';
            }

            return "=" + escaped;
        }

        private void ExportToPdf(string pdfPath, List<AccessEntry> entries)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var title = string.IsNullOrWhiteSpace(_eventName)
                ? "Отчёт по допускам"
                : $"Отчёт по допускам — {_eventName}";

            var subtitle = SelectedReportMode switch
            {
                "По периоду аккредитации" when StartDateFilter.HasValue && EndDateFilter.HasValue
                    => $"Период: {StartDateFilter:dd.MM.yyyy} — {EndDateFilter:dd.MM.yyyy}",
                "По должностям"
                    => BuildPositionsSubtitle(),
                _ => "Общий список"
            };

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);

                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).SemiBold().FontSize(18);
                        col.Item().Text(subtitle).FontSize(12).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrWhiteSpace(_location))
                        {
                            col.Item().Text($"Локация: {_location}").FontSize(11).FontColor(Colors.Grey.Darken2);
                        }
                        col.Item().Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(11).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.6f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(1.15f);
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(1.15f);
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(1.45f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text(text => text.Span("№").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Фамилия").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Имя").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Отчество").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Телефон").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Паспорт").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Должность").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Аккредитация").SemiBold().FontSize(11));
                        });

                        for (var i = 0; i < entries.Count; i++)
                        {
                            var entry = entries[i];

                            table.Cell().Element(DataCell).Text(text => text.Span($"{i + 1}.").FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.LastName).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.FirstName).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.MiddleName ?? string.Empty).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.Phone).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.Passport).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.Position).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span($"с {entry.AccredStart:dd.MM.yyyy} по {entry.AccredEnd:dd.MM.yyyy}").FontSize(10));
                        }
                    });
                });
            })
            .GeneratePdf(pdfPath);
        }

        private string BuildPositionsSubtitle()
        {
            var selected = PositionFilters.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            return selected.Count == 0 ? "Все должности" : "Должности: " + string.Join(", ", selected);
        }

        private PdfContainer HeaderCell(PdfContainer container)
        {
            return container
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(6)
                .PaddingHorizontal(4)
                .AlignCenter();
        }

        private PdfContainer DataCell(PdfContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(4)
                .PaddingHorizontal(4);
        }

        private void LoadAccessEntries()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string query = @"SELECT a.AccessID, a.LastName, a.FirstName, a.MiddleName, a.Phone, a.Passport, p.PositionName, a.AccredStart, a.AccredEnd
                                        FROM AccessList a
                                        INNER JOIN Positions p ON a.PositionID = p.PositionID";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                _allEntries.Clear();
                FilteredEntries.Clear();
                while (reader.Read())
                {
                    var entry = new AccessEntry
                    {
                        AccessId = reader.GetInt32(reader.GetOrdinal("AccessID")),
                        LastName = reader["LastName"] as string ?? string.Empty,
                        FirstName = reader["FirstName"] as string ?? string.Empty,
                        MiddleName = reader["MiddleName"] as string,
                        Phone = reader["Phone"] as string ?? string.Empty,
                        Passport = reader["Passport"] as string ?? string.Empty,
                        Position = reader["PositionName"] as string ?? string.Empty,
                        AccredStart = reader.GetDateTime(reader.GetOrdinal("AccredStart")),
                        AccredEnd = reader.GetDateTime(reader.GetOrdinal("AccredEnd"))
                    };

                    _allEntries.Add(entry);
                    FilteredEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка допусков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulatePositionFilters()
        {
            PositionFilters.Clear();

            var distinctPositions = _allEntries
                .Select(entry => entry.Position)
                .Where(position => !string.IsNullOrWhiteSpace(position))
                .Distinct()
                .OrderBy(position => position);

            foreach (var position in distinctPositions)
            {
                PositionFilters.Add(new PositionFilterItem
                {
                    Name = position,
                    IsSelected = false
                });
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<AccessEntry> filtered = _allEntries;

            switch (SelectedReportMode)
            {
                case "По периоду аккредитации":
                    var start = StartDateFilter;
                    var end = EndDateFilter;

                    if (start.HasValue || end.HasValue)
                    {
                        var effectiveStart = start ?? end;
                        var effectiveEnd = end ?? start;

                        if (effectiveStart.HasValue && effectiveEnd.HasValue)
                        {
                            filtered = filtered.Where(entry => entry.AccredStart <= effectiveEnd.Value && entry.AccredEnd >= effectiveStart.Value);
                        }
                    }
                    break;
                case "По должностям":
                    var selectedPositions = PositionFilters.Where(p => p.IsSelected).Select(p => p.Name).ToList();
                    if (selectedPositions.Any())
                    {
                        filtered = filtered.Where(entry => selectedPositions.Contains(entry.Position));
                    }
                    break;
            }

            FilteredEntries.Clear();
            foreach (var entry in filtered)
            {
                FilteredEntries.Add(entry);
            }
        }

        private void UpdateControlAvailability()
        {
            var isPeriodMode = SelectedReportMode == "По периоду аккредитации";
            var isPositionMode = SelectedReportMode == "По должностям";

            ReportModeComboBox.IsEnabled = true;
            DateFilterPanel.Visibility = isPeriodMode ? Visibility.Visible : Visibility.Collapsed;
            PositionFilterPanel.Visibility = isPositionMode ? Visibility.Visible : Visibility.Collapsed;

            StartDateTextBox.IsEnabled = isPeriodMode;
            EndDateTextBox.IsEnabled = isPeriodMode;
            TodayOnlyCheckBox.IsEnabled = isPeriodMode;

            PositionsComboBox.IsEnabled = isPositionMode;
            ClearPositionsButton.IsEnabled = isPositionMode;

            if (!isPeriodMode)
            {
                TodayOnlyCheckBox.IsChecked = false;
                ClearDateFields();
            }

            if (!isPositionMode)
            {
                foreach (var position in PositionFilters)
                {
                    position.IsSelected = false;
                }

                UpdatePositionsSummary();
            }
        }

        private void UpdatePositionsSummary()
        {
            var selectedNames = PositionFilters.Where(p => p.IsSelected).Select(p => p.Name).ToList();

            if (selectedNames.Count == 0)
            {
                PositionsComboBox.Text = "Все должности";
                PositionsComboBox.ToolTip = "Выбраны все должности";
            }
            else
            {
                var preview = string.Join(", ", selectedNames.Take(2));
                var suffix = selectedNames.Count > 2 ? ", …" : string.Empty;
                PositionsComboBox.Text = preview + suffix;
                PositionsComboBox.ToolTip = string.Join(", ", selectedNames);
            }
        }

        private void HandleDateTextChange(TextBox textBox, bool isStart)
        {
            var caret = textBox.SelectionStart;
            var originalLength = textBox.Text.Length;
            var formatted = FormatDateInput(textBox.Text);

            if (!string.Equals(textBox.Text, formatted, StringComparison.Ordinal))
            {
                textBox.Text = formatted;
                var diff = formatted.Length - originalLength;
                textBox.SelectionStart = Clamp(caret + diff, 0, formatted.Length);
            }

            if (formatted.Length == 10 && DateTime.TryParseExact(formatted, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                if (isStart)
                {
                    StartDateFilter = parsedDate;
                }
                else
                {
                    EndDateFilter = parsedDate;
                }
            }
            else
            {
                if (isStart)
                {
                    StartDateFilter = null;
                }
                else
                {
                    EndDateFilter = null;
                }
            }
        }

        private static string FormatDateInput(string rawText)
        {
            var digits = new string(rawText.Where(char.IsDigit).ToArray());
            if (digits.Length > 8)
            {
                digits = digits.Substring(0, 8);
            }

            if (digits.Length <= 2)
            {
                return digits;
            }

            if (digits.Length <= 4)
            {
                return $"{digits.Substring(0, 2)}.{digits.Substring(2)}";
            }

            var day = digits.Substring(0, 2);
            var month = digits.Substring(2, 2);
            var year = digits.Substring(4);
            return $"{day}.{month}.{year}";
        }

        private void SetDateTextBoxes(DateTime? start, DateTime? end)
        {
            StartDateTextBox.Text = start.HasValue ? start.Value.ToString("dd.MM.yyyy") : string.Empty;
            EndDateTextBox.Text = end.HasValue ? end.Value.ToString("dd.MM.yyyy") : string.Empty;
        }

        private void ClearDateFields()
        {
            StartDateTextBox.Text = string.Empty;
            EndDateTextBox.Text = string.Empty;
            StartDateFilter = null;
            EndDateFilter = null;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PositionFilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}