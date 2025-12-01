using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BackupServer.Components;
using BackupServer.Services;

namespace BackupServer {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private ServerView serverView;
        private SideBarMenu sideBarMenu;
        public MainWindow() {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        }
        private void ServerViewInitialize() {
            serverView=new ServerView();
            sideBarMenu = new SideBarMenu();
            sideBarMenu.NavigationRequest += OnNavigationRequested;
        }
        private void OnNavigationRequested(object sender, string viewName) {
            // 根据viewName切换不同的视图
            switch (viewName) {
                case "ServerPanel":
                    ShowServerView();
                    break;
                case "Settings":
                    //ShowSettingsView();
                    break;
                case "LocalBrowse":
                    //ShowLocalBrowseView();
                    break;
                case "Roaming":
                    //ShowRoamingView();
                    break;
                case "Follow":
                    //ShowFollowView();
                    break;
                default:
                    ShowServerView();
                    break;
            }
        }
        private void ShowServerView() {
            if (serverView == null) {
                serverView = new ServerView();
            }
            MainContentControl.Content = serverView;
            StatusTextBlock.Text = "服务器面板";
        }
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // 停止所有服务器
            var serverView = FindName("ServerView") as BackupServer.Components.ServerView;
            serverView?.StopServer();
        }
    }
}
