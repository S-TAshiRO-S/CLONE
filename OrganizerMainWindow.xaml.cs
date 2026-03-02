using System.Windows;
using System.Windows.Controls;

namespace EAccess.Client
{
    public partial class MainOrganizerWindow : Window
    {
        public string UserFullName { get; set; }

        private readonly int _userId;

        public MainOrganizerWindow() : this("Фамилия Имя Отчество", 0)
        {
        }

        public MainOrganizerWindow(string userFullName) : this(userFullName, 0)
        {
        }

        public MainOrganizerWindow(string userFullName, int userId)
        {
            InitializeComponent();

            UserFullName = userFullName ?? "Фамилия Имя Отчество";
            UserFullNameTextBlock.Text = UserFullName;

            _userId = userId;
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AlreadyOnPage
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnBudget_Click(object sender, RoutedEventArgs e)
        {
            var popup = new NoEventPopup
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var popup = new NoEventPopup
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            var popup = new NoEventPopup
            {
                Owner = this
            };

            popup.ShowDialog();
        }

        private void BtnStartEvent_Click(object sender, RoutedEventArgs e)
        {
            var startWindow = new StartEventWindow
            {
                Owner = this
            };

            if (startWindow.ShowDialog() == true)
            {
                var eventWindow = new OrganizerEventWindow(
                    startWindow.EventId,
                    startWindow.EventName,
                    null,
                    null,
                    null,
                    UserFullName,
                    _userId);

                eventWindow.Show();
                Close();
            }
        }
    }
}