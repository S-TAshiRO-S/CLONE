using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;

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

        public SecurityAccessListWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();
            DataContext = this;

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;

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
            MessageBox.Show("Вы нажали: Отчёты", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnControlAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Контроль / Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Заглушка: добавить запись", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Заглушка: удалить запись", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadAccessEntries()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                const string query = @"SELECT a.LastName, a.FirstName, a.MiddleName, a.Phone, a.Passport, p.PositionName, a.AccredStart, a.AccredEnd
                                        FROM AccessList a
                                        INNER JOIN Positions p ON a.PositionID = p.PositionID";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                AccessEntries.Clear();
                while (reader.Read())
                {
                    var entry = new AccessEntry
                    {
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