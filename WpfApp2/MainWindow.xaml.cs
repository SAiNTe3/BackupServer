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
            this.Loaded += MainWindow_Loaded;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            ServerViewInitialize();
        }
        private void ServerViewInitialize() {
            serverView=new ServerView();
            var sideBarMenuControl = FindVisualChild<SideBarMenu>(this);
            if (sideBarMenuControl != null) {
                sideBarMenuControl.NavigationRequest += OnNavigationRequested;
            }
        }
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T) {
                    return (T)child;
                }
                else {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
        private void OnNavigationRequested(object sender, string viewName) {
            // 根据viewName切换不同的视图
            switch (viewName) {
                case "ServerPanel":
                    ShowServerView();
                    break;
                case "Settings":
                    ShowSettingsView();
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
        private void ShowSettingsView() {
            var settingsView = new SettingsView();
            MainContentControl.Content = settingsView;
            StatusTextBlock.Text = "设置";
        }
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // 停止所有服务器
            var serverView = FindName("ServerView") as BackupServer.Components.ServerView;
            serverView?.StopServer();
        }
    }
}
