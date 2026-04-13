using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EAccess.Client
{
    public partial class AddEditAccessWindow : Window
    {
        private readonly string _connectionString;
        private readonly AccessEntry? _existingEntry;
        private readonly int _actorUserId;
        private bool _isDateFormatting;

        public AddEditAccessWindow(string connectionString, int actorUserId, string actorFullName, AccessEntry? entry = null)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _existingEntry = entry;
            _actorUserId = actorUserId;

            LoadPositions();
            PopulateFieldsIfEdit();
        }

        private void LoadPositions()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string query = "SELECT PositionID, PositionName FROM Positions ORDER BY PositionName";
                using var adapter = new SqlDataAdapter(query, connection);

                var table = new DataTable();
                adapter.Fill(table);

                var positions = table.Rows
                    .Cast<DataRow>()
                    .Select(r => new PositionOption((int)r["PositionID"], r["PositionName"] as string ?? string.Empty))
                    .ToList();

                PositionComboBox.ItemsSource = positions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить список должностей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateFieldsIfEdit()
        {
            if (_existingEntry is null)
            {
                return;
            }

            PassportTextBox.Text = _existingEntry.Passport;
            AccredStartTextBox.Text = _existingEntry.AccredStart.ToString("dd.MM.yyyy");
            AccredEndTextBox.Text = _existingEntry.AccredEnd.ToString("dd.MM.yyyy");
            LastNameTextBox.Text = _existingEntry.LastName;
            FirstNameTextBox.Text = _existingEntry.FirstName;
            MiddleNameTextBox.Text = _existingEntry.MiddleName;

            var trimmedPhone = _existingEntry.Phone.StartsWith("8") && _existingEntry.Phone.Length == 11
                ? _existingEntry.Phone.Substring(1)
                : _existingEntry.Phone;

            PhoneTextBox.Text = trimmedPhone;

            if (PositionComboBox.ItemsSource is IEnumerable<PositionOption> options)
            {
                var selected = options.FirstOrDefault(o => o.Name.Equals(_existingEntry.Position, StringComparison.OrdinalIgnoreCase));
                if (selected is not null)
                {
                    PositionComboBox.SelectedValue = selected.Id;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryValidate(out var startDate, out var endDate, out var positionId))
            {
                return;
            }

            var phoneWithoutCode = PhoneTextBox.Text.Trim();
            var phoneWithCountryCode = $"8{phoneWithoutCode}";
            var lastName = LastNameTextBox.Text.Trim();
            var firstName = FirstNameTextBox.Text.Trim();
            var middleName = string.IsNullOrWhiteSpace(MiddleNameTextBox.Text) ? null : MiddleNameTextBox.Text.Trim();
            var passport = PassportTextBox.Text.Trim();
            var positionName = (PositionComboBox.SelectedItem as PositionOption)?.Name ?? string.Empty;
            var fullName = AccessListFormatting.FormatFullName(lastName, firstName, middleName);

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                if (_existingEntry is null)
                {
                    const string insertQuery = @"INSERT INTO AccessList (EventID, LastName, FirstName, MiddleName, Phone, Passport, PositionID, AccredStart, AccredEnd)
SELECT TOP 1 e.EventID, @LastName, @FirstName, @MiddleName, @Phone, @Passport, @PositionId, @AccredStart, @AccredEnd
FROM Events e WHERE e.IsActive = 1";

                    using var command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@LastName", lastName);
                    command.Parameters.AddWithValue("@FirstName", firstName);
                    command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(middleName) ? DBNull.Value : middleName);
                    command.Parameters.AddWithValue("@Phone", phoneWithCountryCode);
                    command.Parameters.AddWithValue("@Passport", passport);
                    command.Parameters.AddWithValue("@PositionId", positionId);
                    command.Parameters.AddWithValue("@AccredStart", startDate);
                    command.Parameters.AddWithValue("@AccredEnd", endDate);

                    command.ExecuteNonQuery();

                    InsertAuditRecord(connection, $"[ДОБАВЛЕНО] Добавил пользователя: {fullName}");
                }
                else
                {
                    const string updateQuery = @"UPDATE AccessList
                                                 SET LastName = @LastName, FirstName = @FirstName, MiddleName = @MiddleName, Phone = @Phone, Passport = @Passport, PositionID = @PositionId, AccredStart = @AccredStart, AccredEnd = @AccredEnd
                                                 WHERE AccessID = @AccessId";

                    using var command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@LastName", lastName);
                    command.Parameters.AddWithValue("@FirstName", firstName);
                    command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(middleName) ? DBNull.Value : middleName); ;
                    command.Parameters.AddWithValue("@Phone", phoneWithCountryCode);
                    command.Parameters.AddWithValue("@Passport", passport);
                    command.Parameters.AddWithValue("@PositionId", positionId);
                    command.Parameters.AddWithValue("@AccredStart", startDate);
                    command.Parameters.AddWithValue("@AccredEnd", endDate);
                    command.Parameters.AddWithValue("@AccessId", _existingEntry.AccessId);

                    command.ExecuteNonQuery();

                    var changes = BuildChangeList(_existingEntry, lastName, firstName, middleName, phoneWithCountryCode, passport, positionName, startDate, endDate);
                    if (changes.Count > 0)
                    {
                        var originalFullName = AccessListFormatting.FormatFullName(_existingEntry.LastName, _existingEntry.FirstName, _existingEntry.MiddleName);
                        var changeSummary = string.Join("; ", changes);
                        var note = $"Изменил данные у: {originalFullName} (ИЗМЕНЕНО: {changeSummary})";
                        InsertAuditRecord(connection, note);
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (message.Contains("Passport") || message.Contains("UQ_AccessList_Passport"))
                    MessageBox.Show("Человек с таким номером паспорта уже есть в списке допусков.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                else if (message.Contains("Phone") || message.Contains("UQ_AccessList_Phone"))
                    MessageBox.Show("Человек с таким номером телефона уже есть в списке допусков.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show($"Не удалось сохранить запись: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<string> BuildChangeList(AccessEntry original, string newLastName, string newFirstName, string? newMiddleName, string newPhone, string newPassport, string newPositionName, DateTime newStart, DateTime newEnd)
        {
            var changes = new List<string>();

            if (!string.Equals(original.LastName, newLastName, StringComparison.Ordinal))
            {
                changes.Add($"Фамилия {original.LastName} на {newLastName}");
            }

            if (!string.Equals(original.FirstName, newFirstName, StringComparison.Ordinal))
            {
                changes.Add($"Имя {original.FirstName} на {newFirstName}");
            }

            if (!string.Equals(original.MiddleName ?? string.Empty, newMiddleName ?? string.Empty, StringComparison.Ordinal))
            {
                var fromValue = string.IsNullOrWhiteSpace(original.MiddleName) ? "(пусто)" : original.MiddleName;
                var toValue = string.IsNullOrWhiteSpace(newMiddleName) ? "(пусто)" : newMiddleName;
                changes.Add($"Отчество {fromValue} на {toValue}");
            }

            if (!string.Equals(original.Phone, newPhone, StringComparison.Ordinal))
            {
                changes.Add($"Телефон {original.Phone} на {newPhone}");
            }

            if (!string.Equals(original.Passport, newPassport, StringComparison.Ordinal))
            {
                changes.Add($"Паспорт {original.Passport} на {newPassport}");
            }

            if (!string.Equals(original.Position, newPositionName, StringComparison.Ordinal))
            {
                changes.Add($"Должность {original.Position} на {newPositionName}");
            }

            if (original.AccredStart.Date != newStart.Date)
            {
                changes.Add($"Аккредитация с {original.AccredStart:dd.MM.yyyy} на {newStart:dd.MM.yyyy}");
            }

            if (original.AccredEnd.Date != newEnd.Date)
            {
                changes.Add($"Аккредитация по {original.AccredEnd:dd.MM.yyyy} на {newEnd:dd.MM.yyyy}");
            }

            return changes;
        }

        private void InsertAuditRecord(SqlConnection connection, string note)
        {
            const string auditQuery = @"INSERT INTO SecurityAudit (EventID, UserID, Note)
SELECT TOP 1 EventID, @UserId, @Note
FROM Events WHERE IsActive = 1";

            using var command = new SqlCommand(auditQuery, connection);
            command.Parameters.AddWithValue("@UserId", _actorUserId);
            command.Parameters.AddWithValue("@Note", note);
            command.ExecuteNonQuery();
        }

        private bool TryValidate(out DateTime accredStart, out DateTime accredEnd, out int positionId)
        {
            accredStart = default;
            accredEnd = default;
            positionId = 0;

            if (string.IsNullOrWhiteSpace(LastNameTextBox.Text))
            {
                MessageBox.Show("Поле 'Фамилия' обязательно для заполнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text))
            {
                MessageBox.Show("Поле 'Имя' обязательно для заполнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsDigitsExactLength(PassportTextBox.Text, 10))
            {
                MessageBox.Show("Паспорт должен содержать ровно 10 цифр без пробелов и символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsDigitsExactLength(PhoneTextBox.Text, 10))
            {
                MessageBox.Show("Телефон должен содержать ровно 10 цифр после кода страны 8.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!DateTime.TryParseExact(AccredStartTextBox.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out accredStart))
            {
                MessageBox.Show("Дата 'Аккредитация с' указана неверно. Используйте формат дд.мм.гггг.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!DateTime.TryParseExact(AccredEndTextBox.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out accredEnd))
            {
                MessageBox.Show("Дата 'Аккредитация по' указана неверно. Используйте формат дд.мм.гггг.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (accredEnd < accredStart)
            {
                MessageBox.Show("Дата окончания аккредитации не может быть раньше даты начала.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (PositionComboBox.SelectedValue is not int selectedPosition)
            {
                MessageBox.Show("Выберите должность из списка.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            positionId = selectedPosition;
            return true;
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isDateFormatting)
            {
                return;
            }

            if (sender is not TextBox textBox)
            {
                return;
            }

            var digits = new string(textBox.Text.Where(char.IsDigit).ToArray());
            if (digits.Length > 8)
            {
                digits = digits[..8];
            }

            var formatted = digits;
            if (digits.Length > 4)
            {
                formatted = $"{digits[..2]}.{digits.Substring(2, 2)}.{digits[4..]}";
            }
            else if (digits.Length > 2)
            {
                formatted = $"{digits[..2]}.{digits[2..]}";
            }

            _isDateFormatting = true;
            textBox.Text = formatted;
            textBox.CaretIndex = textBox.Text.Length;
            _isDateFormatting = false;
        }

        private void PassportTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            textBox.Text = new string(textBox.Text.Where(char.IsDigit).Take(10).ToArray());
            textBox.CaretIndex = textBox.Text.Length;
        }

        private static bool IsDigitsExactLength(string? value, int length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var digitsOnly = value.Trim();
            return digitsOnly.Length == length && digitsOnly.All(char.IsDigit);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public record PositionOption(int Id, string Name)
    {
        public override string ToString() => Name;
    }
}