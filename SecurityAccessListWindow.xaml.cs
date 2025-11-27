using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EAccess.Client
{
    public partial class SecurityAccessListWindow : Window
    {
        public ObservableCollection<AccessEntry> AccessEntries { get; } = new();

        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly string? _connectionString;

        public SecurityAccessListWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();
            DataContext = this;

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;
            _connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;

            UserFullNameTextBlock.Text = userFullName;

            LoadAccessEntries();
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new SecurityMainWindow(_eventName, _startDate, _endDate, _location, _userFullName);
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

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var reportsWindow = new SecurityReportsWindow(_eventName, _startDate, _endDate, _location, _userFullName);
            reportsWindow.Show();
            Close();
        }

        private void BtnControlAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Контроль / Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var popup = new AddEditAccessWindow(_connectionString)
            {
                Owner = this
            };

            if (popup.ShowDialog() == true)
            {
                LoadAccessEntries();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (AccessDataGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одну запись для удаления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedEntries = AccessDataGrid.SelectedItems.Cast<AccessEntry>().ToList();
            var confirmationMessage = selectedEntries.Count == 1
                ? $"Удалить запись для {selectedEntries[0].LastName} {selectedEntries[0].FirstName}?"
                : $"Удалить выбранные {selectedEntries.Count} записей?";

            var result = MessageBox.Show(confirmationMessage, "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                const string deleteQuery = "DELETE FROM AccessList WHERE AccessID = @AccessId";

                foreach (var entry in selectedEntries)
                {
                    using var command = new SqlCommand(deleteQuery, connection, transaction);
                    command.Parameters.AddWithValue("@AccessId", entry.AccessId);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
                LoadAccessEntries();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении записей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AccessDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AccessDataGrid.SelectedItem is not AccessEntry entry)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var popup = new AddEditAccessWindow(_connectionString, entry)
            {
                Owner = this
            };

            if (popup.ShowDialog() == true)
            {
                LoadAccessEntries();
            }
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
    }

    public class AccessEntry
    {
        public int AccessId { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Passport { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime AccredStart { get; set; }
        public DateTime AccredEnd { get; set; }

        public string AccreditationRange => $"с {AccredStart:dd.MM.yyyy} по {AccredEnd:dd.MM.yyyy}";
    }
}