using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Windows;

namespace EAccess.Client
{
    public partial class OrganizerEventWindow : Window
    {
        private readonly int _eventId;
        private readonly string _connectionString;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string? _location;

        public OrganizerEventWindow(int eventId, string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();

            _eventId = eventId;
            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString ?? string.Empty;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;

            EventTitleText.Text = eventName;
            UserFullNameTextBlock.Text = userFullName;
            StartDateTextBox.Text = FormatDate(_startDate);
            EndDateTextBox.Text = FormatDate(_endDate);
            LocationTextBox.Text = location ?? string.Empty;
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Главная", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBudget_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Смета", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Отчёты", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StartDateTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveDateValue(StartDateTextBox.Text, "StartDate", ref _startDate, StartDateTextBox);
        }

        private void EndDateTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveDateValue(EndDateTextBox.Text, "EndDate", ref _endDate, EndDateTextBox);
        }

        private void LocationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveLocation(LocationTextBox.Text);
        }

        private void EndEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnection())
            {
                return;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var cmd = new SqlCommand("UPDATE Events SET IsActive = 0 WHERE EventID = @id", connection);
                cmd.Parameters.AddWithValue("@id", _eventId);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Мероприятие завершено", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);

                var main = new MainOrganizerWindow(UserFullNameTextBlock.Text);
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при завершении мероприятия: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDateValue(string? value, string columnName, ref DateTime? cachedValue, System.Windows.Controls.TextBox source)
        {
            if (!EnsureConnection())
            {
                source.Text = FormatDate(cachedValue);
                return;
            }

            value = value?.Trim();
            DateTime? parsedValue = null;

            if (!string.IsNullOrEmpty(value))
            {
                if (!DateTime.TryParseExact(value, new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    MessageBox.Show("Введите дату в формате дд.мм.гггг", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    source.Text = FormatDate(cachedValue);
                    return;
                }

                parsedValue = parsed.Date;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var cmd = new SqlCommand($"UPDATE Events SET {columnName} = @value WHERE EventID = @id", connection);
                cmd.Parameters.AddWithValue("@id", _eventId);
                cmd.Parameters.AddWithValue("@value", parsedValue.HasValue ? parsedValue.Value : DBNull.Value);
                cmd.ExecuteNonQuery();

                cachedValue = parsedValue;
                source.Text = FormatDate(cachedValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении даты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                source.Text = FormatDate(cachedValue);
            }
        }

        private void SaveLocation(string? text)
        {
            if (!EnsureConnection())
            {
                LocationTextBox.Text = _location ?? string.Empty;
                return;
            }

            text = text?.Trim();
            object dbValue = string.IsNullOrEmpty(text) ? DBNull.Value : text;

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var cmd = new SqlCommand("UPDATE Events SET Location = @location WHERE EventID = @id", connection);
                cmd.Parameters.AddWithValue("@id", _eventId);
                cmd.Parameters.AddWithValue("@location", dbValue);
                cmd.ExecuteNonQuery();

                _location = text;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении места проведения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                LocationTextBox.Text = _location ?? string.Empty;
            }
        }

        private bool EnsureConnection()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("dd.MM.yyyy") : string.Empty;
        }
    }
}