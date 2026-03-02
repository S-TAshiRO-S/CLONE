using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace EAccess.Client
{
    public partial class AuthWindow : Window
    {
        // цвета для ошибки
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x6B)); 
        private static readonly Brush NormalLoginBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4E586E"));
        private static readonly Brush NormalPasswordBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4E586E"));

        public AuthWindow()
        {
            InitializeComponent();
            UpdatePasswordPlaceholderVisibility();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            bool loginEmpty = string.IsNullOrWhiteSpace(LoginTextBox.Text);
            bool pwdEmpty = string.IsNullOrEmpty(PasswordBox.Password);

            // Сбрасываем рамки
            LoginBorder.BorderThickness = new Thickness(0);
            PasswordBorder.BorderThickness = new Thickness(0);

            if (loginEmpty || pwdEmpty)
            {
                // Пометить ошибочные поля красной рамкой
                if (loginEmpty)
                {
                    LoginBorder.BorderBrush = ErrorBrush;
                    LoginBorder.BorderThickness = new Thickness(3);
                }
                if (pwdEmpty)
                {
                    PasswordBorder.BorderBrush = ErrorBrush;
                    PasswordBorder.BorderThickness = new Thickness(3);
                }

                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? connectionString = ConfigurationManager.ConnectionStrings["EAccessDb"]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не настроена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var connection = new SqlConnection(connectionString!);
                connection.Open();

                using var command = new SqlCommand(
                    "SELECT UserID, PasswordHash, Role, LastName, FirstName, MiddleName FROM Users WHERE Login = @login",
                    connection);
                command.Parameters.AddWithValue("@login", LoginTextBox.Text);

                using var reader = command.ExecuteReader();

                if (!reader.Read())
                {
                    ShowUserNotFoundMessage();
                    return;
                }

                var userId = reader.GetInt32(reader.GetOrdinal("UserID"));
                var storedHash = reader["PasswordHash"] as byte[] ?? Array.Empty<byte>();
                string role = reader["Role"] as string ?? string.Empty;
                string lastName = reader["LastName"] as string ?? string.Empty;
                string firstName = reader["FirstName"] as string ?? string.Empty;
                string middleName = reader["MiddleName"] as string ?? string.Empty;

                var passwordHash = ComputeSha256(PasswordBox.Password);

                bool passwordMatches = storedHash != null && storedHash.SequenceEqual(passwordHash);

                if (!passwordMatches)
                {
                    ShowUserNotFoundMessage();
                    return;
                }

                var fullName = string.Join(" ", new[] { lastName, firstName, middleName }.Where(x => !string.IsNullOrWhiteSpace(x)));

                if (string.Equals(role, "Организатор", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Close();

                    var activeEvent = GetActiveEvent(connection);
                    if (activeEvent.HasValue)
                    {
                        var evt = activeEvent.Value;
                        var activeWindow = new OrganizerEventWindow(evt.EventId, evt.EventName, evt.StartDate, evt.EndDate, evt.Location, fullName, userId);
                        activeWindow.Show();
                    }
                    else
                    {
                        var organizerWindow = new MainOrganizerWindow(fullName, userId);
                        organizerWindow.Show();
                    }

                    Close();
                }
                else if (string.Equals(role, "Служба безопасности", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Close();
                    var activeEvent = GetActiveEvent(connection);

                    if (activeEvent.HasValue)
                    {
                        var evt = activeEvent.Value;
                        var securityWindow = new SecurityMainWindow(evt.EventName, evt.StartDate, evt.EndDate, evt.Location, fullName, userId);
                        securityWindow.Show();
                        Close();
                    }
                    else
                    {
                        var popup = new NoEventPopup
                        {
                            Owner = this
                        };

                        popup.ShowDialog();
                    }
                }
                else if (string.Equals(role, "Контроллер доступа", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Close();
                    var activeEvent = GetActiveEvent(connection);

                    if (activeEvent.HasValue)
                    {
                        var evt = activeEvent.Value;
                        var controllerWindow = new ControllerMainWindow(evt.EventName, evt.StartDate, evt.EndDate, evt.Location, fullName, userId);
                        controllerWindow.Show();
                        Close();
                    }
                    else
                    {
                        var popup = new NoEventPopup
                        {
                            Owner = this
                        };

                        popup.ShowDialog();
                    }
                }
                else
                {
                    ShowUserNotFoundMessage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при подключении к базе данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static byte[] ComputeSha256(string text)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        }

        private static void ShowUserNotFoundMessage()
        {
            MessageBox.Show("Такого пользователя не существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholderVisibility();
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholderVisibility();
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholderVisibility();
        }

        private void PasswordPlaceholder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PasswordBox.Focus();
        }

        private void UpdatePasswordPlaceholderVisibility()
        {
            if (PasswordBox == null || PasswordPlaceholder == null) return;
            bool has = !string.IsNullOrEmpty(PasswordBox.Password);
            bool focused = PasswordBox.IsFocused;
            PasswordPlaceholder.Visibility = (!has && !focused) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static ActiveEventInfo? GetActiveEvent(SqlConnection connection)
        {
            using var cmd = new SqlCommand("SELECT TOP 1 EventID, EventName, StartDate, EndDate, Location FROM Events WHERE IsActive = 1 ORDER BY EventID DESC", connection);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return new ActiveEventInfo
            {
                EventId = (reader["EventID"] as int?) ?? Convert.ToInt32(reader["EventID"]),
                EventName = reader["EventName"] as string ?? string.Empty,
                StartDate = reader["StartDate"] == DBNull.Value ? (DateTime?)null : (DateTime)reader["StartDate"],
                EndDate = reader["EndDate"] == DBNull.Value ? (DateTime?)null : (DateTime)reader["EndDate"],
                Location = reader["Location"] as string
            };
        }

        private struct ActiveEventInfo
        {
            public int EventId { get; init; }
            public string EventName { get; init; }
            public DateTime? StartDate { get; init; }
            public DateTime? EndDate { get; init; }
            public string? Location { get; init; }
        }
    }

    public class TextEmptyAndNotFocusedToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? text = values.Length > 0 ? values[0] as string : null;
            bool isFocused = values.Length > 1 && values[1] is bool b && b;

            bool show = string.IsNullOrEmpty(text) && !isFocused;
            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
