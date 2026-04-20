using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EAccess.Client
{
    public partial class ControllerAuditWindow : Window
    {
        public ObservableCollection<AccessAuditEntry> FilteredAuditEntries { get; } = new();

        private readonly List<AccessAuditEntry> _allAuditEntries = new();

        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;
        private readonly string? _connectionString;
        private string _searchQuery = string.Empty;

        public ControllerAuditWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
        {
            InitializeComponent();
            DataContext = this;

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
            var mainWindow = new ControllerMainWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            mainWindow.Show();
            Close();
        }

        private void BtnAccessList_Click(object sender, RoutedEventArgs e)
        {
            var accessListWindow = new ControllerAccessListWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            accessListWindow.Show();
            Close();
        }

        private void BtnQrBarcode_Click(object sender, RoutedEventArgs e)
        {
            var qrWindow = new ControllerQrWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            qrWindow.Show();
            Close();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _searchQuery = SearchTextBox.Text ?? string.Empty;
            ApplyFilter();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!FilteredAuditEntries.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EAccessExports");
                Directory.CreateDirectory(exportFolder);

                var timestamp = DateTime.Now.ToString("ddMMyyyy_HHmmss", CultureInfo.InvariantCulture);
                var pdfPath = Path.Combine(exportFolder, $"Аудит_Контролёр_{timestamp}.pdf");

                ExportToPdf(pdfPath, FilteredAuditEntries.ToList());

                MessageBox.Show($"Экспорт завершён.\nPDF: {pdfPath}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToPdf(string pdfPath, List<AccessAuditEntry> entries)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var title = string.IsNullOrWhiteSpace(_eventName)
                ? "Аудит контролёров доступа"
                : $"Аудит контролёров доступа — {_eventName}";

            var subtitle = string.IsNullOrWhiteSpace(_searchQuery)
                ? "Аудит по фильтрам: Без фильтров"
                : $"Аудит по фильтрам: {_searchQuery}";

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
                        col.Item().Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(11).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.4f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text(text => text.Span("№").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Фамилия Имя Отчество").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Дата / время").SemiBold().FontSize(11));
                            header.Cell().Element(HeaderCell).Text(text => text.Span("Примечание").SemiBold().FontSize(11));
                        });

                        for (var i = 0; i < entries.Count; i++)
                        {
                            var entry = entries[i];

                            table.Cell().Element(DataCell).Text(text => text.Span($"{i + 1}.").FontSize(10));
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

                const string query = @"SELECT aa.AccessAuditID, aa.AuditDate, aa.Note,
                                               u.LastName, u.FirstName, u.MiddleName
                                        FROM AccessAudit aa
                                        INNER JOIN Users u ON aa.UserID = u.UserID
                                        ORDER BY aa.AuditDate DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                _allAuditEntries.Clear();
                while (reader.Read())
                {
                    var lastName = reader["LastName"] as string ?? string.Empty;
                    var firstName = reader["FirstName"] as string ?? string.Empty;
                    var middleName = reader["MiddleName"] as string;
                    var note = reader["Note"] as string ?? string.Empty;
                    var auditDate = reader.GetDateTime(reader.GetOrdinal("AuditDate"));

                    _allAuditEntries.Add(new AccessAuditEntry
                    {
                        AuditId = reader.GetInt32(reader.GetOrdinal("AccessAuditID")),
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
            IEnumerable<AccessAuditEntry> entries = _allAuditEntries;

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
            {
                FilteredAuditEntries.Add(entry);
            }
        }
    }

    public class AccessAuditEntry
    {
        public int AuditId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public DateTime AuditDate { get; set; }
        public string Note { get; set; } = string.Empty;

        public string AuditDateTime => AuditDate.ToString("dd.MM.yyyy HH:mm");
    }
}