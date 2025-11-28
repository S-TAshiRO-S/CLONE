using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace EAccess.Client
{
    public partial class ControllerAccessListWindow : Window
    {
        public ObservableCollection<AccessEntry> AccessEntries { get; } = new();

        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;
        private readonly string? _connectionString;

        private const float BadgeWidth = 283.5f;  // 10 см
        private const float BadgeHeight = 425.2f; // 15 см
        private const float QrSize = 113.4f;      // 4 см

        public ControllerAccessListWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
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

            LoadAccessEntries();
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new ControllerMainWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            mainWindow.Show();
            Close();
        }

        private void BtnAccessList_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnQrBarcode_Click(object sender, RoutedEventArgs e)
        {
            var qrWindow = new ControllerQrWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            qrWindow.Show();
            Close();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var auditWindow = new ControllerAuditWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            auditWindow.Show();
            Close();
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (AccessDataGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одну запись для печати бейджей.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedEntries = AccessDataGrid.SelectedItems.Cast<AccessEntry>().ToList();

            try
            {
                var badgePayloads = selectedEntries
                    .Select(entry =>
                    {
                        var rawPayload = BuildPayload(entry);
                        var encodedPayload = EncodePayloadBase64(rawPayload);
                        return new BadgePayload(entry, rawPayload, encodedPayload);
                    })
                    .ToList();

                var qrImages = badgePayloads.Select(payload => GenerateQrCode(payload.EncodedPayload)).ToList();
                var qrHashes = badgePayloads.Select(payload => ComputeHash(payload.RawPayload)).ToList();

                SaveQrHashes(selectedEntries, qrHashes);

                var badgeItems = selectedEntries
                    .Select((entry, index) => new BadgeContent(entry, qrImages[index]))
                    .ToList();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    FileName = "Badges.pdf",
                    Title = "Сохранить бейджи"
                };

                if (saveDialog.ShowDialog(this) == true)
                {
                    GenerateBadgeDocument(badgeItems).GeneratePdf(saveDialog.FileName);
                    MessageBox.Show("PDF с бейджами успешно создан.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании бейджей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveQrHashes(IReadOnlyList<AccessEntry> entries, IReadOnlyList<byte[]> qrHashes)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            const string updateQuery = "UPDATE AccessList SET QRHash = @QrHash WHERE AccessID = @AccessId";

            for (var i = 0; i < entries.Count; i++)
            {
                using var command = new SqlCommand(updateQuery, connection, transaction);
                command.Parameters.AddWithValue("@QrHash", qrHashes[i]);
                command.Parameters.AddWithValue("@AccessId", entries[i].AccessId);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private string BuildPayload(AccessEntry entry)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"AccessID:{entry.AccessId}");
            builder.AppendLine($"LastName:{entry.LastName}");
            builder.AppendLine($"FirstName:{entry.FirstName}");
            builder.AppendLine($"MiddleName:{entry.MiddleName}");
            builder.AppendLine($"Phone:{entry.Phone}");
            builder.AppendLine($"AccredStart:{entry.AccredStart:yyyy-MM-dd}");
            builder.AppendLine($"AccredEnd:{entry.AccredEnd:yyyy-MM-dd}");
            builder.AppendLine($"Event:{_eventName}");
            return builder.ToString();
        }

        private static byte[] GenerateQrCode(string payload)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrData);
            return pngQrCode.GetGraphic(20);
        }

        private static string EncodePayloadBase64(string rawPayload)
        {
            var bytes = Encoding.UTF8.GetBytes(rawPayload);
            return Convert.ToBase64String(bytes);
        }

        private static byte[] ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        private Document GenerateBadgeDocument(IReadOnlyList<BadgeContent> badges)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(10);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(14).FontColor(Colors.Black));

                    page.Content().Column(column =>
                    {
                        column.Spacing(15);

                        for (var i = 0; i < badges.Count; i += 2)
                        {
                            var first = badges[i];
                            var secondExists = i + 1 < badges.Count;
                            var second = secondExists ? badges[i + 1] : null;

                            column.Item().Row(row =>
                            {
                                row.Spacing(15);
                                row.RelativeItem().Width(BadgeWidth).Height(BadgeHeight).Element(container => ComposeBadge(container, first));
                                if (secondExists && second != null)
                                {
                                    row.RelativeItem().Width(BadgeWidth).Height(BadgeHeight).Element(container => ComposeBadge(container, second));
                                }
                            });
                        }
                    });
                });
            });
        }

        private void ComposeBadge(IContainer container, BadgeContent badge)
        {
            var fullName = AccessListFormatting.FormatFullName(badge.Entry.LastName, badge.Entry.FirstName, badge.Entry.MiddleName);
            var accreditation = $"Аккредитация: с {badge.Entry.AccredStart:dd.MM.yyyy} по {badge.Entry.AccredEnd:dd.MM.yyyy}";
            var locationText = string.IsNullOrWhiteSpace(_location) ? string.Empty : $"Место проведения: {_location}";
            var disclaimer = "Аккредитационный бейдж следует носить на видном месте в течение всего времени пребывания на территории мероприятия. Аккредитованное лицо должно иметь при себе удостоверение личности, указанное при подаче заявления на аккредитацию и предъявлять его по требованию уполномоченных лиц. Передача бейджа третьим лицам запрещена. За нарушение правил нахождения на объектах мероприятия бейдж может быть изъят. Бейдж содержит цифровой носитель информации.";

            container
                .Border(2)
                .BorderColor(Colors.Grey.Darken2)
                .Background(Colors.Grey.Lighten5)
                .Padding(16)
                .Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Background(Colors.Grey.Lighten3).Padding(6).Text(_eventName).FontSize(22).SemiBold().AlignCenter();
                    column.Item().Text(fullName).FontSize(24).Bold().FontColor(Colors.Black).AlignCenter();
                    column.Item().Text(badge.Entry.Position).FontSize(18).FontColor(Colors.Black).AlignCenter();
                    column.Item().Text(accreditation).FontSize(16).FontColor(Colors.Black).AlignCenter();
                    if (!string.IsNullOrWhiteSpace(locationText))
                        column.Item().Text(locationText).FontSize(14).FontColor(Colors.Black).AlignCenter();

                    column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().AlignBottom().PaddingRight(10).Text(disclaimer).FontSize(8).FontColor(Colors.Grey.Darken2).AlignLeft();
                        row.ConstantItem(QrSize).Height(QrSize).AlignBottom().AlignRight().PaddingLeft(6).Image(badge.QrCodeBytes);
                    });
                });
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

                AccessEntries.Clear();
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

                    AccessEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка допусков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private record BadgePayload(AccessEntry Entry, string RawPayload, string EncodedPayload);

        private record BadgeContent(AccessEntry Entry, byte[] QrCodeBytes);
    }
}