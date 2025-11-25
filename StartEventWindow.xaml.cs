using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;

namespace EAccess.Client
{
    public partial class StartEventWindow : Window
    {
        public string EventName { get; private set; } = string.Empty;
        public int EventId { get; private set; }

        public StartEventWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var name = EventNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название мероприятия", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                EventNameTextBox.Focus();
                return;
            }

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

                using (var deactivate = new SqlCommand("UPDATE Events SET IsActive = 0 WHERE IsActive = 1", connection))
                {
                    deactivate.ExecuteNonQuery();
                }

                using (var insertCommand = new SqlCommand(
                           "INSERT INTO Events (EventName, StartDate, EndDate, Location, IsActive) OUTPUT INSERTED.EventID " +
                           "VALUES (@name, NULL, NULL, NULL, 1)", connection))
                {
                    insertCommand.Parameters.AddWithValue("@name", name);
                    EventId = Convert.ToInt32(insertCommand.ExecuteScalar());
                }

                EventName = name;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении мероприятия: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}