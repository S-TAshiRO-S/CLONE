using System;
using System.Collections.ObjectModel;
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

            // Заглушечные данные для визуализации таблицы; будут заменены загрузкой из БД позже
            AccessEntries.Add(new AccessEntry
            {
                LastName = "Карпов",
                FirstName = "Глеб",
                MiddleName = "Андреевич",
                Phone = "89990155772",
                Passport = "4090123456",
                Position = "Безопасность",
                AccredStart = new DateTime(2025, 10, 10),
                AccredEnd = new DateTime(2025, 10, 20)
            });
            AccessEntries.Add(new AccessEntry
            {
                LastName = "Иванова",
                FirstName = "Мария",
                MiddleName = "Петровна",
                Phone = "87774441100",
                Passport = "5011223344",
                Position = "Волонтер",
                AccredStart = new DateTime(2025, 10, 12),
                AccredEnd = new DateTime(2025, 10, 21)
            });
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
    }
}