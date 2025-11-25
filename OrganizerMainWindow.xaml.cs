using System.Windows;
using System.Windows.Controls;

namespace EAccess.Client
{
    public partial class MainOrganizerWindow : Window
    {
        // Можно передавать сюда ФИО при создании окна
        public string UserFullName { get; set; }

        public MainOrganizerWindow() : this("Фамилия Имя Отчество")
        {
        }

        // Конструктор с передачей имени пользователя (вызови из AuthWindow после логина)
        public MainOrganizerWindow(string userFullName)
        {
            InitializeComponent();
            UserFullName = userFullName ?? "Фамилия Имя Отчество";
            UserFullNameTextBlock.Text = UserFullName;
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Главная", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBudget_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Смета", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Отчёты", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAudit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы нажали: Аудит", "Действие", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnStartEvent_Click(object sender, RoutedEventArgs e)
        {
            var startWindow = new StartEventWindow
            {
                Owner = this
            };

            if (startWindow.ShowDialog() == true)
            {
                // Заглушка: дальнейшая логика запуска мероприятия будет добавлена позже
                MessageBox.Show(
                    $"Мероприятие \"{startWindow.EventName}\" сохранено",
                    "Начать мероприятие",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
