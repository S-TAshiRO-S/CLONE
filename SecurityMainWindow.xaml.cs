using System;
using System.Windows;

namespace EAccess.Client
{
    public partial class SecurityMainWindow : Window
    {
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;

        public SecurityMainWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;

            EventTitleText.Text = eventName;
            UserFullNameTextBlock.Text = userFullName;
            StartDateText.Text = FormatDate(startDate);
            EndDateText.Text = FormatDate(endDate);
            LocationText.Text = string.IsNullOrWhiteSpace(location) ? "" : location;
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnAccessList_Click(object sender, RoutedEventArgs e)
        {
            var accessListWindow = new SecurityAccessListWindow(_eventName, _startDate, _endDate, _location, _userFullName);
            accessListWindow.Show();
            Close();
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Отчёты", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnControlAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Контроль / Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("dd.MM.yyyy") : string.Empty;
        }
    }
}