using System;
using System.Windows;

namespace EAccess.Client
{
    public partial class SecurityMainWindow : Window
    {
        public SecurityMainWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName)
        {
            InitializeComponent();

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
            MessageBox.Show("Вы нажали: Список допусков", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
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