using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EAccess.Client
{
    public partial class AddEditPurchaseItemWindow : Window
    {
        private readonly string _connectionString;
        private readonly int _eventId;
        private readonly int _userId;
        private readonly PurchaseEntry? _edit;
        private double _oldOwnerOpacity = 1.0;

        public ObservableCollection<StatusOption> Statuses { get; } = new();

        public AddEditPurchaseItemWindow(string connectionString, int eventId, int userId, PurchaseEntry? edit = null)
        {
            InitializeComponent();

            Loaded += AddEditPurchaseItemWindow_Loaded;
            Closed += AddEditPurchaseItemWindow_Closed;

            _connectionString = connectionString;
            _eventId = eventId;
            _userId = userId;
            _edit = edit;

            LoadStatuses();

            StatusComboBox.ItemsSource = Statuses;

            if (_edit != null)
            {
                NameTextBox.Text = _edit.Name;
                SupplierTextBox.Text = _edit.Supplier;
                QuantityTextBox.Text = _edit.Quantity.ToString();
                PriceTextBox.Text = _edit.Price.ToString("0", CultureInfo.CurrentCulture);

                var s = Statuses.FirstOrDefault(x => x.Name == _edit.StatusName);
                if (s != null)
                    StatusComboBox.SelectedValue = s.Id;
            }
            else
            {
                StatusComboBox.SelectedIndex = 0;
            }
        }

        private void AddEditPurchaseItemWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null) return;

            _oldOwnerOpacity = Owner.Opacity;
            Owner.Opacity = 0.55;
        }

        private void AddEditPurchaseItemWindow_Closed(object? sender, EventArgs e)
        {
            if (Owner == null) return;

            Owner.Opacity = _oldOwnerOpacity;
        }

        private void LoadStatuses()
        {
            Statuses.Clear();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var cmd = new SqlCommand("SELECT StatusID, StatusName FROM PurchaseStatuses ORDER BY StatusID", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Statuses.Add(new StatusOption
                {
                    Id = reader.GetInt32(reader.GetOrdinal("StatusID")),
                    Name = reader["StatusName"] as string ?? string.Empty
                });
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = (NameTextBox.Text ?? "").Trim();
            var supplier = (SupplierTextBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(supplier))
            {
                MessageBox.Show("Заполните наименование и поставщика.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(QuantityTextBox.Text, out var qty) || qty <= 0)
            {
                MessageBox.Show("Количество должно быть больше 0.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var priceText = new string((PriceTextBox.Text ?? "").Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var price) || price < 0)
            {
                MessageBox.Show("Цена должна быть числом и не меньше 0.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StatusComboBox.SelectedValue is not int statusId)
            {
                MessageBox.Show("Выберите статус.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var tr = connection.BeginTransaction();

                int purchaseItemId;

                if (_edit == null)
                {
                    const string ins = @"
INSERT INTO PurchaseItems (EventID, Name, Supplier, Quantity, Price, StatusID)
OUTPUT INSERTED.PurchaseItemID
VALUES (@EventID, @Name, @Supplier, @Quantity, @Price, @StatusID);";

                    using var cmd = new SqlCommand(ins, connection, tr);
                    cmd.Parameters.AddWithValue("@EventID", _eventId);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Supplier", supplier);
                    cmd.Parameters.AddWithValue("@Quantity", qty);
                    cmd.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
                    cmd.Parameters.AddWithValue("@StatusID", statusId);

                    purchaseItemId = Convert.ToInt32(cmd.ExecuteScalar());

                    InsertAudit(connection, tr, purchaseItemId, $"[ДОБАВЛЕНО] {name}");
                }
                else
                {
                    const string upd = @"
UPDATE PurchaseItems
SET Name=@Name, Supplier=@Supplier, Quantity=@Quantity, Price=@Price, StatusID=@StatusID
WHERE PurchaseItemID=@Id;";

                    using var cmd = new SqlCommand(upd, connection, tr);
                    cmd.Parameters.AddWithValue("@Id", _edit.PurchaseItemId);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Supplier", supplier);
                    cmd.Parameters.AddWithValue("@Quantity", qty);
                    cmd.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
                    cmd.Parameters.AddWithValue("@StatusID", statusId);
                    cmd.ExecuteNonQuery();

                    purchaseItemId = _edit.PurchaseItemId;

                    InsertAudit(connection, tr, purchaseItemId, $"[ИЗМЕНЕНО] {name}");
                }

                tr.Commit();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InsertAudit(SqlConnection connection, SqlTransaction tr, int purchaseItemId, string note)
        {
            const string sql = "INSERT INTO PurchaseAudit (EventID, UserID, PurchaseItemID, Note) VALUES (@EventID, @UserID, @PurchaseItemID, @Note)";
            using var cmd = new SqlCommand(sql, connection, tr);
            cmd.Parameters.AddWithValue("@EventID", _eventId);
            cmd.Parameters.AddWithValue("@UserID", _userId);
            cmd.Parameters.AddWithValue("@PurchaseItemID", purchaseItemId);
            cmd.Parameters.AddWithValue("@Note", note);
            cmd.ExecuteNonQuery();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void Money_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !(char.IsDigit(e.Text, 0) || e.Text == " " || e.Text == "," || e.Text == ".");
        }

        public class StatusOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}