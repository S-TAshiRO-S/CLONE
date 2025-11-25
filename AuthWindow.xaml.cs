using System;
using System.Diagnostics;
using System.Globalization;
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

            MessageBox.Show($"Логин: {LoginTextBox.Text}\nПароль: {(PasswordBox.Password.Length > 0 ? "●●●●" : "(пусто)")}");

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
    }

    public class TextEmptyAndNotFocusedToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string text = values.Length > 0 ? values[0] as string : null;
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
