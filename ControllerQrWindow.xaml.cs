using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EAccess.Client
{
    public partial class ControllerQrWindow : Window
    {
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;
        private readonly string? _connectionString;

        private readonly StringBuilder _scanBuffer = new();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private readonly TimeSpan _scanTimeout = TimeSpan.FromMilliseconds(400);

        public ControllerQrWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
        {
            InitializeComponent();

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;
            _userId = userId;
            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;

            UserFullNameTextBlock.Text = userFullName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            HiddenInput.Focus();
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            ResetBufferIfTimeout();
            _scanBuffer.Append(e.Text);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var payload = _scanBuffer.ToString().Trim();
                _scanBuffer.Clear();
                e.Handled = true;

                ProcessScan(payload);
            }
            else
            {
                ResetBufferIfTimeout();
            }
        }

        private void ResetBufferIfTimeout()
        {
            var now = DateTime.Now;
            if ((now - _lastKeyTime) > _scanTimeout)
            {
                _scanBuffer.Clear();
            }

            _lastKeyTime = now;
        }

        private void ProcessScan(string payload)
        {
            try
            {
                var cleanedPayload = NormalizePayload(payload);

                if (string.IsNullOrWhiteSpace(cleanedPayload))
                {
                    ShowNotFound("Отсканированный код пустой.");
                    return;
                }

                var incomingHash = ComputeHash(cleanedPayload);
                var accessId = ParseAccessId(cleanedPayload) ?? ParseAccessId(payload);
                var lookup = accessId.HasValue ? TryLoadAccessEntry(accessId.Value) : null;

                if (lookup == null)
                {
                    lookup = TryLoadAccessEntryByHash(incomingHash);
                }

                if (lookup == null)
                {
                    ShowNotFound("Пользователь не найден в системе.");
                    return;
                }

                var today = DateTime.Today;
                var isWithinAccreditation = today >= lookup.Entry.AccredStart.Date && today <= lookup.Entry.AccredEnd.Date;

                var noteBuilder = new StringBuilder();
                if (!string.Equals(payload, cleanedPayload, StringComparison.Ordinal))
                {
                    noteBuilder.Append("Считанный код очищен от лишних символов. ");
                }

                if (lookup.StoredHash is { Length: > 0 })
                {
                    var expectedPayload = BuildPayload(lookup.Entry);
                    var expectedHash = ComputeHash(expectedPayload);

                    if (!expectedHash.SequenceEqual(lookup.StoredHash))
                    {
                        noteBuilder.Append("QR в базе не совпадает с текущими данными записи. ");
                    }
                    else if (!incomingHash.SequenceEqual(lookup.StoredHash))
                    {
                        noteBuilder.Append("Содержимое QR-кода отличается от сохранённого, использованы данные из базы. ");
                    }
                }

                var note = noteBuilder.ToString().Trim();

                if (isWithinAccreditation)
                {
                    ShowApproved(lookup.Entry, note);
                }
                else
                {
                    ShowDenied(lookup.Entry, string.IsNullOrWhiteSpace(note)
                        ? "Срок аккредитации не действует на текущую дату."
                        : $"{note}Срок аккредитации не действует на текущую дату.");
                }
            }
            finally
            {
                HiddenInput.Focus();
            }
        }

        private static string NormalizePayload(string payload)
        {
            var builder = new StringBuilder(payload.Length);

            foreach (var ch in payload)
            {
                if (char.IsControl(ch) && ch != '\r' && ch != '\n')
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString().Trim();
        }

        private static int? ParseAccessId(string payload)
        {
            var directMatch = Regex.Match(payload, @"AccessID\D*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (directMatch.Success && int.TryParse(directMatch.Groups[1].Value, out var directId))
            {
                return directId;
            }

            var lines = payload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("AccessID", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(line, @"AccessID\s*[:=]?\s*(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var idFromMatch))
                    {
                        return idFromMatch;
                    }

                    var digits = new string(line.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var digitsOnlyId))
                    {
                        return digitsOnlyId;
                    }
                }
            }

            if (int.TryParse(payload, out var fallbackId))
            {
                return fallbackId;
            }

            return null;
        }

        private AccessLookupResult? TryLoadAccessEntry(int accessId)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string query = @"SELECT a.AccessID, a.LastName, a.FirstName, a.MiddleName, a.Phone, a.Passport, p.PositionName, a.AccredStart, a.AccredEnd, a.QRHash
                                       FROM AccessList a
                                       INNER JOIN Positions p ON a.PositionID = p.PositionID
                                       WHERE a.AccessID = @AccessId";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@AccessId", accessId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new AccessLookupResult
                    {
                        Entry = new AccessEntry
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
                        },
                        StoredHash = reader["QRHash"] as byte[]
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private AccessLookupResult? TryLoadAccessEntryByHash(byte[] incomingHash)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string query = @"SELECT TOP 1 a.AccessID, a.LastName, a.FirstName, a.MiddleName, a.Phone, a.Passport, p.PositionName, a.AccredStart, a.AccredEnd, a.QRHash
                                       FROM AccessList a
                                       INNER JOIN Positions p ON a.PositionID = p.PositionID
                                       WHERE a.QRHash = @QrHash";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@QrHash", incomingHash);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new AccessLookupResult
                    {
                        Entry = new AccessEntry
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
                        },
                        StoredHash = reader["QRHash"] as byte[]
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private static byte[] ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        private void ShowApproved(AccessEntry entry, string? note = null)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ScanPlaceholderBorder.Visibility = Visibility.Collapsed;
            ResultBorder.Background = Brushes.Transparent;
            StatusText.Foreground = (Brush)FindResource("SuccessBrush");
            StatusText.Text = "ДОСТУП: РАЗРЕШЕН";
            UpdatePersonData(entry);
            NoteText.Text = note ?? "";
        }

        private void ShowDenied(AccessEntry entry, string reason)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ScanPlaceholderBorder.Visibility = Visibility.Collapsed;
            ResultBorder.Background = Brushes.Transparent;
            StatusText.Foreground = (Brush)FindResource("ErrorBrush");
            StatusText.Text = "ДОСТУП: ЗАПРЕЩЕН";
            UpdatePersonData(entry);
            NoteText.Text = reason;
        }

        private void ShowNotFound(string reason)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ScanPlaceholderBorder.Visibility = Visibility.Collapsed;
            ResultBorder.Background = Brushes.Transparent;
            StatusText.Foreground = (Brush)FindResource("NeutralBrush");
            StatusText.Text = "ДОСТУП: НЕ НАЙДЕНО В СИСТЕМЕ";
            FullNameText.Text = "Фамилия Имя Отчество: -";
            PositionText.Text = "Должность: -";
            PassportText.Text = "Паспорт: -";
            AccreditationText.Text = "Аккредитация: -";
            NoteText.Text = reason;
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

        private void UpdatePersonData(AccessEntry entry)
        {
            var fullName = AccessListFormatting.FormatFullName(entry.LastName, entry.FirstName, entry.MiddleName);
            FullNameText.Text = $"Фамилия Имя Отчество: {fullName}";
            PositionText.Text = $"Должность: {entry.Position}";
            PassportText.Text = $"Паспорт: {entry.Passport}";
            AccreditationText.Text = $"Аккредитация: с {entry.AccredStart:dd.MM.yyyy} по {entry.AccredEnd:dd.MM.yyyy}";
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
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var auditWindow = new ControllerAuditWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            auditWindow.Show();
            Close();
        }
    }

    internal sealed class AccessLookupResult
    {
        public required AccessEntry Entry { get; init; }
        public byte[]? StoredHash { get; init; }
    }
}
