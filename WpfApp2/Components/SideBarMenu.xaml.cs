using System;
using System.Windows.Controls;

namespace BackupServer.Components
{
    public partial class SideBarMenu : UserControl
    {
        public SideBarMenu()
        {
            InitializeComponent();
        }

        public Action<object, string> NavigationRequest { get; internal set; }

        public void ServerPanelButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            NavigationRequest?.Invoke(this, "ServerPanel");
        }
        public void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            NavigationRequest?.Invoke(this, "Settings");
        }
    }
}