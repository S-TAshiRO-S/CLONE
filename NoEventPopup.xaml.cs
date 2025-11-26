using System.ComponentModel;
using System.Windows;

namespace EAccess.Client
{
    public partial class NoEventPopup : Window
    {
        private bool _canClose;

        public NoEventPopup()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _canClose = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_canClose)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }
    }
}