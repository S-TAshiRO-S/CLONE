using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

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
                if (string.IsNullOrWhiteSpace(payload))
                {
                    ShowNotFound("Отсканированный код пустой.");
                    return;
                }

                var accessId = ParseAccessId(payload);
                if (!accessId.HasValue)
                {
                    ShowNotFound("В коде не найден идентификатор пользователя.");
                    return;
                }

                var entry = TryLoadAccessEntry(accessId.Value, out var qrHash);
                if (entry == null)
                {
                    ShowNotFound("Пользователь не найден в системе.");
                    return;
                }

                if (qrHash is { Length: > 0 })
                {
                    var incomingHash = ComputeHash(payload);
                    if (!incomingHash.SequenceEqual(qrHash))
                    {
                        ShowDenied(entry, "QR-код не совпадает с данными в системе.");
                        return;
                    }
                }

                var today = DateTime.Today;
                var isWithinAccreditation = today >= entry.AccredStart.Date && today <= entry.AccredEnd.Date;

                if (isWithinAccreditation)
                {
                    ShowApproved(entry);
                }
                else
                {
                    ShowDenied(entry, "Срок аккредитации не действует на текущую дату.");
                }
            }
            finally
            {
                HiddenInput.Focus();
            }
        }

        private static int? ParseAccessId(string payload)
        {
            var lines = payload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("AccessID:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Split(':', 2).Skip(1).FirstOrDefault();
                    if (int.TryParse(value, out var id))
                    {
                        return id;
                    }
                }
            }

            if (int.TryParse(payload, out var fallbackId))
            {
                return fallbackId;
            }

            return null;
        }

        private AccessEntry? TryLoadAccessEntry(int accessId, out byte[]? qrHash)
        {
            qrHash = null;

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
                    qrHash = reader["QRHash"] as byte[];

                    return new AccessEntry
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

        private void ShowApproved(AccessEntry entry)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ResultBorder.Background = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            StatusText.Text = "ДОСТУП: РАЗРЕШЕН";
            UpdatePersonData(entry);
            NoteText.Text = "";
        }

        private void ShowDenied(AccessEntry entry, string reason)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ResultBorder.Background = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            StatusText.Text = "ДОСТУП: ЗАПРЕЩЕН";
            UpdatePersonData(entry);
            NoteText.Text = reason;
        }

        private void ShowNotFound(string reason)
        {
            ResultBorder.Visibility = Visibility.Visible;
            ResultBorder.Background = (System.Windows.Media.Brush)FindResource("NeutralBrush");
            StatusText.Text = "ДОСТУП: НЕ НАЙДЕНО В СИСТЕМЕ";
            FullNameText.Text = "Фамилия Имя Отчество: -";
            PositionText.Text = "Должность: -";
            PassportText.Text = "Паспорт: -";
            AccreditationText.Text = "Аккредитация: -";
            NoteText.Text = reason;
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
}