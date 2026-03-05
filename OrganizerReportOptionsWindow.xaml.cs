using System;
using System.Collections.Generic;
using System.Windows;

namespace EAccess.Client
{
    public enum OrganizerReportMode
    {
        All,
        Selected,
        ByStatus
    }

    public partial class OrganizerReportOptionsWindow : Window
    {
        public OrganizerReportMode Mode { get; private set; } = OrganizerReportMode.All;
        public string? SelectedStatus { get; private set; }

        private double _oldOwnerOpacity = 1.0;

        public OrganizerReportOptionsWindow(List<string> statuses, bool hasSelection)
        {
            InitializeComponent();

            StatusComboBox.ItemsSource = statuses;
            StatusComboBox.SelectedIndex = statuses.Count > 0 ? 0 : -1;

            if (!hasSelection)
                RbSelected.IsEnabled = false;

            Loaded += OrganizerReportOptionsWindow_Loaded;
            Closed += OrganizerReportOptionsWindow_Closed;
        }

        private void OrganizerReportOptionsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null) return;
            _oldOwnerOpacity = Owner.Opacity;
            Owner.Opacity = 0.55;
        }

        private void OrganizerReportOptionsWindow_Closed(object? sender, EventArgs e)
        {
            if (Owner == null) return;
            Owner.Opacity = _oldOwnerOpacity;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (RbAll.IsChecked == true)
            {
                Mode = OrganizerReportMode.All;
                SelectedStatus = null;
            }
            else if (RbSelected.IsChecked == true)
            {
                Mode = OrganizerReportMode.Selected;
                SelectedStatus = null;
            }
            else
            {
                Mode = OrganizerReportMode.ByStatus;
                SelectedStatus = StatusComboBox.SelectedItem as string;

                if (string.IsNullOrWhiteSpace(SelectedStatus))
                {
                    MessageBox.Show("Выберите статус.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}