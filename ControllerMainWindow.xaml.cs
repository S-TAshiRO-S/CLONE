using System;
using System.Windows;

namespace EAccess.Client
{
    public partial class ControllerMainWindow : Window
    {
        private readonly string _eventName;
        private readonly DateTime? _startDate;
        private readonly DateTime? _endDate;
        private readonly string? _location;
        private readonly string _userFullName;
        private readonly int _userId;

        public ControllerMainWindow(string eventName, DateTime? startDate, DateTime? endDate, string? location, string userFullName, int userId)
        {
            InitializeComponent();

            _eventName = eventName;
            _startDate = startDate;
            _endDate = endDate;
            _location = location;
            _userFullName = userFullName;
            _userId = userId;

            EventTitleText.Text = eventName;
            UserFullNameTextBlock.Text = userFullName;
            StartDateText.Text = FormatDate(startDate);
            EndDateText.Text = FormatDate(endDate);
            LocationText.Text = string.IsNullOrWhiteSpace(location) ? string.Empty : location;
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
            var accessListWindow = new ControllerAccessListWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            accessListWindow.Show();
            Close();
        }

        private void BtnQrBarcode_Click(object sender, RoutedEventArgs e)
        {
            var qrWindow = new ControllerQrWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            qrWindow.Show();
            Close();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var auditWindow = new ControllerAuditWindow(_eventName, _startDate, _endDate, _location, _userFullName, _userId);
            auditWindow.Show();
            Close();
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("dd.MM.yyyy") : string.Empty;
        }
    }
}