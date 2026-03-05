using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EAccess.Client
{
    public partial class OrganizerEventWindow : Window
    {
        private readonly int _eventId;
        private readonly string _connectionString;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string? _location;
        private readonly string _eventName;
        private readonly string _userFullName;
        private readonly int _userId;

        public OrganizerEventWindow(int eventId, string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
        {
            InitializeComponent();

            _eventId = eventId;
            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString ?? string.Empty;

            _startDate = startDate;
            _endDate = endDate;
            _location = location;

            _eventName = eventName;
            _userFullName = userFullName;
            _userId = userId;

            EventTitleText.Text = eventName;
            UserFullNameTextBlock.Text = userFullName;
            StartDateTextBox.Text = FormatDate(_startDate);
            EndDateTextBox.Text = FormatDate(_endDate);
            LocationTextBox.Text = location ?? string.Empty;
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
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

        private void DateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DateTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (string)e.DataObject.GetData(DataFormats.Text);
                var digits = Regex.Replace(text ?? string.Empty, "[^0-9]", string.Empty);
                e.DataObject = new DataObject(DataFormats.Text, digits);
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void DateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            var caretDigits = CountDigitsBeforeCaret(textBox.Text, textBox.CaretIndex);

            var digitsOnly = Regex.Replace(textBox.Text, "[^0-9]", string.Empty);
            if (digitsOnly.Length > 8)
            {
                digitsOnly = digitsOnly[..8];
            }

            var formatted = InsertDateSeparators(digitsOnly);

            if (textBox.Text == formatted)
            {
                return;
            }

            var newCaretIndex = CalculateCaretIndex(formatted, caretDigits);

            textBox.TextChanged -= DateTextBox_TextChanged;
            textBox.Text = formatted;
            textBox.CaretIndex = newCaretIndex;
            textBox.TextChanged += DateTextBox_TextChanged;
        }

        private void LocationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveLocation(LocationTextBox.Text);
        }

        private void EndEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnection())
                return;

            var confirm = MessageBox.Show(
                "Завершить мероприятие?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var cmd = new SqlCommand("UPDATE Events SET IsActive = 0 WHERE EventID = @id", connection);
                cmd.Parameters.AddWithValue("@id", _eventId);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Мероприятие завершено.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);

                var main = new MainOrganizerWindow(_userFullName, _userId);
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

        private static string InsertDateSeparators(string digits)
        {
            return digits.Length switch
            {
                <= 2 => digits,
                <= 4 => $"{digits[..2]}.{digits[2..]}",
                _ => $"{digits[..2]}.{digits[2..4]}.{digits[4..]}"
            };
        }

        private static int CountDigitsBeforeCaret(string text, int caretIndex)
        {
            caretIndex = Math.Min(caretIndex, text.Length);
            var leftPart = caretIndex > 0 ? text[..caretIndex] : string.Empty;
            return leftPart.Count(char.IsDigit);
        }

        private static int CalculateCaretIndex(string formatted, int digitsBeforeCaret)
        {
            if (digitsBeforeCaret <= 0)
            {
                return 0;
            }

            var digitsSeen = 0;
            for (var i = 0; i < formatted.Length; i++)
            {
                if (char.IsDigit(formatted[i]))
                {
                    digitsSeen++;
                }

                if (digitsSeen >= digitsBeforeCaret)
                {
                    return i + 1;
                }
            }

            return formatted.Length;
        }
    }
}