using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Http;
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
using BackupServer.Services;

namespace BackupServer.Components
{
    /// <summary>
    /// ServerView.xaml 的交互逻辑
    /// </summary>
    public partial class ServerView : UserControl
    {
        public event Action<string> MessageReceived;
        public event Action<string> StatusChanged;
        public event Action<Exception> ErrorOccurred;

        private HttpApiServer httpApiServer;

        public ServerView()
        {
            InitializeComponent();
            InitializeHttpServer();
            this.Loaded += ServerView_Loaded;
            this.Unloaded += ServerView_Unloaded;
        }

        private void InitializeHttpServer()
        {
            httpApiServer = new HttpApiServer();
            httpApiServer.RequestReceived += OnRequestReceived;
            httpApiServer.StatusChanged += OnStatusChanged;
            httpApiServer.ErrorOccurred += OnErrorOccurred;
        }
        private void ServerView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }
        private void ServerView_Unloaded(object sender, RoutedEventArgs e)
        {
            httpApiServer?.Stop();
        }

        private void OnRequestReceived(string request)
        {
            Dispatcher.Invoke(() =>
            {
                MessageLogTextBox.AppendText($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {request}\r\n");
                MessageLogTextBox.ScrollToEnd();
            });

            // 转发事件给外部
            MessageReceived?.Invoke(request);
        }
        private void OnStatusChanged(string status)
        {
            // 在UI线程中更新状态
            Dispatcher.Invoke(() =>
            {
                ServerStatusTextBlock.Text = $"服务器状态: {status}";
                UpdateUI();
            });

            // 转发事件给外部
            StatusChanged?.Invoke(status);
        }

        private void OnErrorOccurred(Exception error)
        {
            // 在UI线程中显示错误
            Dispatcher.Invoke(() =>
            {
                string errorMessage = "HTTP服务器错误: " + error.Message;
                string helpText = "";

                // 检查是否是权限错误
                if (error is HttpListenerException httpEx)
                {
                    if (httpEx.ErrorCode == 5) // 拒绝访问
                    {
                        helpText = "\n\n解决方案：\n" +
                                 "1. 尝试以管理员身份运行程序\n" +
                                 "2. 或者更换端口号（如8080、3000等）\n" +
                                 "3. 或者在命令行中执行以下命令授权：\n" +
                                 $"   netsh http add urlacl url=http://+:{PortTextBox.Text}/ user=Everyone";
                    }
                    else if (httpEx.ErrorCode == 183 || httpEx.ErrorCode == 10048)
                    {
                        helpText = "\n\n解决方案：请尝试使用其他端口号";
                    }
                }

                MessageBox.Show(errorMessage + helpText, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // 转发事件给外部
            ErrorOccurred?.Invoke(error);
        }

        private void UpdateUI()
        {
            StartServerButton.IsEnabled = !httpApiServer.IsRunning;
            StopServerButton.IsEnabled = httpApiServer.IsRunning;
        }
        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PortTextBox.Text, out int port))
            {
                await httpApiServer.StartAsync(port);
            }
            else
            {
                MessageBox.Show("请输入有效的端口号", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            httpApiServer.Stop();
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!httpApiServer.IsRunning)
            {
                MessageBox.Show("请先启动HTTP服务器", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                MessageBox.Show("端口号无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await TestHttpConnection(port);
        }

        private async Task TestHttpConnection(int port)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    var testUrls = new[]
                    {
                        $"http://localhost:{port}/",
                        $"http://127.0.0.1:{port}/",
                        $"http://localhost:{port}/photos",
                        $"http://localhost:{port}/status"
                    };

                    foreach (var url in testUrls)
                    {
                        try
                        {
                            var response = await client.GetAsync(url);
                            var content = await response.Content.ReadAsStringAsync();
                            
                            MessageLogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - 测试 {url} - {response.StatusCode} - {content.Substring(0, Math.Min(100, content.Length))}...\r\n");
                        }
                        catch (Exception ex)
                        {
                            MessageLogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - 测试 {url} - 失败: {ex.Message}\r\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageLogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - HTTP连接测试异常: {ex.Message}\r\n");
            }
            
            MessageLogTextBox.ScrollToEnd();
        }

        public bool IsServerRunning => httpApiServer?.IsRunning ?? false;

        public async System.Threading.Tasks.Task<bool> StartServerAsync(int port)
        {
            PortTextBox.Text = port.ToString();
            return await httpApiServer.StartAsync(port);
        }

        public void StopServer()
        {
            httpApiServer?.Stop();
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            MessageLogTextBox.Clear();
        }

        public string SelectedEncoding { get; private set; } = "auto";

        
    }
}
