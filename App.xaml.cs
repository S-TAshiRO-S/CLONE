using QuestPDF;
using QuestPDF.Infrastructure;
using System.Configuration;
using System.Data;
using System.Windows;

namespace EAccess.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Settings.License = LicenseType.Community;
            base.OnStartup(e);
        }
    }

}