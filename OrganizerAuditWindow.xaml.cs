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
using System.Windows;

namespace EAccess.Client
{
    public partial class OrganizerAuditWindow : Window
    {
        public ObservableCollection<PurchaseAuditEntry> FilteredAuditEntries { get; } = new();

        private readonly List<PurchaseAuditEntry> _allAuditEntries = new();

        private readonly int _eventId;
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;

        private readonly string? _connectionString;
        private string _searchQuery = string.Empty;

        public OrganizerAuditWindow(int eventId, string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
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

            LoadAuditEntries();
            ApplyFilter();
        }

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
            var wnd = new OrganizerReportsWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage { Owner = this };
            popup.ShowDialog();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _searchQuery = SearchTextBox.Text ?? string.Empty;
            ApplyFilter();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!_allAuditEntries.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selected = AuditDataGrid.SelectedItems.Cast<PurchaseAuditEntry>().ToList();
            var toExport = selected.Count > 0 ? selected : _allAuditEntries.ToList();

            var kind = selected.Count > 0 ? "Выбранные" : "Все";

            try
            {
                var exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EAccessExports");
                Directory.CreateDirectory(exportFolder);

                var generatedAt = DateTime.Now;
                var ts = DateTime.Now.ToString("ddMMyyyy_HHmmss");
                var pdfPath = Path.Combine(exportFolder, $"Аудит_Организатор_{ts}.pdf");

                ExportToPdf(pdfPath, toExport, generatedAt, kind);

                MessageBox.Show($"Экспорт завершён.\nPDF: {pdfPath}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAuditEntries()
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

                const string query = @"
SELECT pa.PurchaseAuditID, pa.AuditDateTime, pa.Note,
       u.LastName, u.FirstName, u.MiddleName
FROM PurchaseAudit pa
INNER JOIN Users u ON pa.UserID = u.UserID
WHERE pa.EventID = @eventId
ORDER BY pa.AuditDateTime DESC;";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@eventId", _eventId);

                using var reader = command.ExecuteReader();

                _allAuditEntries.Clear();
                while (reader.Read())
                {
                    var lastName = reader["LastName"] as string ?? string.Empty;
                    var firstName = reader["FirstName"] as string ?? string.Empty;
                    var middleName = reader["MiddleName"] as string;
                    var note = reader["Note"] as string ?? string.Empty;
                    var auditDate = reader.GetDateTime(reader.GetOrdinal("AuditDateTime"));

                    _allAuditEntries.Add(new PurchaseAuditEntry
                    {
                        AuditId = reader.GetInt32(reader.GetOrdinal("PurchaseAuditID")),
                        UserFullName = string.IsNullOrWhiteSpace(middleName)
                            ? $"{lastName} {firstName}"
                            : $"{lastName} {firstName} {middleName}",
                        AuditDate = auditDate,
                        Note = note
                    });
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке аудита: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<PurchaseAuditEntry> entries = _allAuditEntries;

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var lowered = _searchQuery.ToLower(CultureInfo.CurrentCulture);
                entries = entries.Where(entry =>
                    entry.UserFullName.ToLower(CultureInfo.CurrentCulture).Contains(lowered) ||
                    entry.AuditDateTime.ToLower(CultureInfo.CurrentCulture).Contains(lowered) ||
                    entry.Note.ToLower(CultureInfo.CurrentCulture).Contains(lowered));
            }

            FilteredAuditEntries.Clear();
            foreach (var entry in entries)
                FilteredAuditEntries.Add(entry);
        }

        private void ExportToPdf(string pdfPath, List<PurchaseAuditEntry> entries, DateTime generatedAt, string kind)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var title = string.IsNullOrWhiteSpace(_eventName)
                ? "Аудит сметы"
                : $"Аудит сметы — {_eventName}";

            var subtitle = $"Строки: {kind}";

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
                        col.Item().Text($"Дата формирования: {generatedAt:dd.MM.yyyy HH:mm}")
                            .FontSize(11).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Фамилия Имя Отчество").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Дата / время").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Примечание").SemiBold().FontSize(11));
                        });

                        for (var i = 0; i < entries.Count; i++)
                        {
                            var entry = entries[i];

                            table.Cell().Element(DataCell).Text(text => text.Span(entry.UserFullName).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.AuditDateTime).FontSize(10));
                            table.Cell().Element(DataCell).Text(text => text.Span(entry.Note).FontSize(10));
                        }
                    });
                });
            })
            .GeneratePdf(pdfPath);
        }

        private IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(6)
                .PaddingHorizontal(4)
                .AlignCenter();
        }

        private IContainer DataCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(4)
                .PaddingHorizontal(4);
        }
    }

    public class PurchaseAuditEntry
    {
        public int AuditId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public DateTime AuditDate { get; set; }
        public string Note { get; set; } = string.Empty;

        public string AuditDateTime => AuditDate.ToString("dd.MM.yyyy HH:mm");
    }
}