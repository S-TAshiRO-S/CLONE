using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EAccess.Client
{
    public partial class OrganizerBudgetWindow : Window
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

        public OrganizerBudgetWindow(int eventId, string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
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
            AllocatedTextBox.Text = FormatMoney(_allocated);
            ReservedTextBox.Text = FormatMoney(_reserved);
            RemainingTextBox.Text = FormatMoney(_allocated - _reserved);
        }

        private void AllocatedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            if (!TryParseMoney(AllocatedTextBox.Text, out var newBudget))
            {
                MessageBox.Show("Введите число (бюджет).", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                AllocatedTextBox.Text = FormatMoney(_allocated);
                return;
            }

            if (newBudget < _reserved)
            {
                MessageBox.Show("Нельзя сделать бюджет меньше, чем уже зарезервировано (потрачено).", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                AllocatedTextBox.Text = FormatMoney(_allocated);
                return;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var cmd = new SqlCommand("UPDATE Events SET Budget = @b WHERE EventID = @id", connection);
                cmd.Parameters.Add("@b", SqlDbType.Decimal).Value = newBudget;
                cmd.Parameters.AddWithValue("@id", _eventId);
                cmd.ExecuteNonQuery();

                _allocated = newBudget;
                UpdateBudgetFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении бюджета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                AllocatedTextBox.Text = FormatMoney(_allocated);
            }
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerEventWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnBudget_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage { Owner = this };
            popup.ShowDialog();
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerReportsWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new OrganizerAuditWindow(_eventId, _eventName, _startDate, _endDate, _location, _userFullName, _userId);
            wnd.Show();
            Close();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            var popup = new AddEditPurchaseItemWindow(_connectionString, _eventId, _userId)
            {
                Owner = this
            };

            if (popup.ShowDialog() == true)
                RefreshAll();
        }

        private void PurchaseDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PurchaseDataGrid.SelectedItem is not PurchaseEntry entry)
                return;

            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            var popup = new AddEditPurchaseItemWindow(_connectionString, _eventId, _userId, entry)
            {
                Owner = this
            };

            if (popup.ShowDialog() == true)
                RefreshAll();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (PurchaseDataGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одну запись для удаления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            var selected = PurchaseDataGrid.SelectedItems.Cast<PurchaseEntry>().ToList();

            var msg = selected.Count == 1
                ? $"Удалить позицию \"{selected[0].Name}\"?"
                : $"Удалить выбранные {selected.Count} позиций?";

            if (MessageBox.Show(msg, "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var tr = connection.BeginTransaction();

                foreach (var item in selected)
                {
                    using (var check = new SqlCommand("SELECT COUNT(1) FROM PurchaseAudit WHERE PurchaseItemID = @id", connection, tr))
                    {
                        check.Parameters.AddWithValue("@id", item.PurchaseItemId);
                        var cnt = Convert.ToInt32(check.ExecuteScalar());
                        if (cnt > 0)
                            throw new InvalidOperationException("Нельзя удалить позицию, т.к. по ней уже есть записи аудита.");
                    }

                    using (var del = new SqlCommand("DELETE FROM PurchaseItems WHERE PurchaseItemID = @id", connection, tr))
                    {
                        del.Parameters.AddWithValue("@id", item.PurchaseItemId);
                        del.ExecuteNonQuery();
                    }
                }

                tr.Commit();
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatMoney(decimal v) =>
            v.ToString("N0", Ru);

        private static bool TryParseMoney(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var cleaned = new string(text.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            cleaned = cleaned.Replace('.', ',');

            return decimal.TryParse(cleaned, NumberStyles.Number, Ru, out value);
        }

        private void MoneyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !(char.IsDigit(e.Text, 0) || e.Text == " " || e.Text == "," || e.Text == ".");
        }

        private void MoneyTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = (string)e.DataObject.GetData(DataFormats.Text);
            if (string.IsNullOrEmpty(text))
                return;

            if (text.Any(ch => !(char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch == ',' || ch == '.')))
                e.CancelCommand();
        }
    }

    public class PurchaseEntry
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public int PurchaseItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;

        public decimal Total => Quantity * Price;

        public string PriceFormatted => Price.ToString("N0", Ru);
        public string TotalFormatted => Total.ToString("N0", Ru);
    }
}