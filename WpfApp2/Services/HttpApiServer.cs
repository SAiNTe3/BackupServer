using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BackupServer.Services
{
    public class HttpApiServer
    {
        private HttpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;

        public event Action<string> RequestReceived;
        public event Action<string> StatusChanged;
        public event Action<Exception> ErrorOccurred;

        public bool IsRunning => _isRunning;
        public int Port { get; private set; }

        public async Task<bool> StartAsync(int port)
        {
            try
            {
                if (_isRunning)
                {
                    StatusChanged?.Invoke("HTTP服务器已在运行中");
                    return false;
                }

                Port = port;
                _listener = new HttpListener();
                
                // 尝试不同的监听地址配置
                bool listenersAdded = TryAddListenerPrefixes(port);
                if (!listenersAdded)
                {
                    StatusChanged?.Invoke("无法配置HTTP监听器，请检查端口或权限");
                    return false;
                }
                
                _cancellationTokenSource = new CancellationTokenSource();

                _listener.Start();
                _isRunning = true;

                StatusChanged?.Invoke($"HTTP API服务器已启动，监听端口: {port}");
                StatusChanged?.Invoke($"本机访问地址: http://localhost:{port}/");
                
                // 显示本机IP地址供局域网访问
                var localIP = GetLocalIPAddress();
                if (!string.IsNullOrEmpty(localIP))
                {
                    StatusChanged?.Invoke($"局域网访问地址: http://{localIP}:{port}/");
                }

                // 在后台线程中开始处理请求
                _ = Task.Run(async () => await ProcessRequestsAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (HttpListenerException hex)
            {
                string errorMsg = GetHttpListenerErrorMessage(hex);
                ErrorOccurred?.Invoke(hex);
                StatusChanged?.Invoke($"启动HTTP服务器失败: {errorMsg}");
                
                // 提供详细的解决建议
                if (hex.ErrorCode == 5) // ACCESS_DENIED
                {
                    StatusChanged?.Invoke("解决方案:");
                    StatusChanged?.Invoke("1. 尝试以管理员身份运行程序");
                    StatusChanged?.Invoke("2. 或者更换端口号(如8080、3000等)");
                    StatusChanged?.Invoke($"3. 或者在命令行中执行以下命令授权:");
                    StatusChanged?.Invoke($"   netsh http add urlacl url=http://+:{port}/ user=Everyone");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
                StatusChanged?.Invoke($"启动HTTP服务器失败: {ex.Message}");
                return false;
            }
        }

        private bool TryAddListenerPrefixes(int port)
        {
            try
            {
                // 先尝试localhost（不需要管理员权限，仅本机访问）
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                
                // 尝试添加局域网访问支持
                try
                {
                    // 获取本机IP地址并添加监听
                    var localIP = GetLocalIPAddress();
                    if (!string.IsNullOrEmpty(localIP))
                    {
                        _listener.Prefixes.Add($"http://{localIP}:{port}/");
                        StatusChanged?.Invoke($"已添加局域网访问支持: {localIP}:{port}");
                    }
                    
                    // 尝试添加通配符监听（需要管理员权限）
                    _listener.Prefixes.Add($"http://+:{port}/");
                    StatusChanged?.Invoke("已添加通配符监听支持");
                }
                catch (Exception ipEx)
                {
                    StatusChanged?.Invoke($"无法添加局域网支持 (需要管理员权限): {ipEx.Message}");
                    StatusChanged?.Invoke("当前仅支持本机访问");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"配置监听器失败: {ex.Message}");
                return false;
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // 方法1：通过连接外部地址获取本机IP
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                    {
                        return endPoint.Address.ToString();
                    }
                }
            }
            catch
            {
                // 方法2：遍历网络接口
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        {
                            return ip.ToString();
                        }
                    }
                }
                catch
                {
                    // 忽略错误，返回null
                }
            }
            
            return null;
        }

        private string GetHttpListenerErrorMessage(HttpListenerException ex)
        {
            switch (ex.ErrorCode)
            {
                case 5: // ERROR_ACCESS_DENIED
                    return "拒绝访问。请尝试以管理员身份运行程序，或使用其他端口。";
                case 183: // ERROR_ALREADY_EXISTS
                    return "端口已被占用，请尝试其他端口。";
                case 10048: // WSAEADDRINUSE
                    return "地址已在使用中，请更换端口号。";
                default:
                    return $"HTTP监听器错误 (错误代码: {ex.ErrorCode}): {ex.Message}";
            }
        }

        public void Stop()
        {
            try
            {
                if (!_isRunning)
                    return;

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
                _listener?.Stop();

                StatusChanged?.Invoke("HTTP API服务器已停止");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _listener = null;
            }
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(async () => await HandleRequestAsync(context, cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // 监听器已被释放，正常退出
                    break;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(ex);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // 设置CORS头
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                // 处理OPTIONS预检请求
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                string responseJson = "";
                int statusCode = 200;

                // 路由处理
                switch (request.Url.AbsolutePath.ToLower())
                {
                    case "/":
                    case "/ping":
                        responseJson = HandlePing(request);
                        break;

                    case "/photos":
                        responseJson = HandlePhotos(request);
                        break;

                    case "/upload":
                        responseJson = await HandleUpload(request);
                        break;

                    case "/status":
                        responseJson = HandleStatus(request);
                        break;

                    default:
                        statusCode = 404;
                        responseJson = CreateJsonResponse("error", "API endpoint not found", null);
                        break;
                }

                // 记录请求日志
                RequestReceived?.Invoke($"{request.HttpMethod} {request.Url.AbsolutePath} - {statusCode}");

                // 发送响应
                response.StatusCode = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                
                byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                response.Close();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        private string HandlePing(HttpListenerRequest request)
        {
            var data = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                version = "1.0.0",
                server = "WpfApp2 HTTP API Server"
            };

            return CreateJsonResponse("success", "Server is running", data);
        }

        private string HandlePhotos(HttpListenerRequest request)
        {
            try
            {
                // 获取上传目录路径
                string uploadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WpfApp2_Uploads");
                
                var photoList = new List<object>();
                
                // 检查上传目录是否存在
                if (Directory.Exists(uploadsPath))
                {
                    // 获取支持的图片文件扩展名
                    string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
                    
                    // 遍历上传目录中的所有图片文件
                    var imageFiles = Directory.GetFiles(uploadsPath)
                        .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
                        .OrderByDescending(file => new FileInfo(file).CreationTime) // 按创建时间倒序排列
                        .ToArray();
                    
                    StatusChanged?.Invoke($"找到 {imageFiles.Length} 个图片文件");
                    
                    foreach (string filePath in imageFiles)
                    {
                        try
                        {
                            FileInfo fileInfo = new FileInfo(filePath);
                            string fileName = fileInfo.Name;
                            
                            // 计算文件的MD5哈希值
                            string fileHash = CalculateFileHash(filePath);
                            
                            // 生成访问URL
                            string imageUrl = $"http://{GetLocalIPAddress()}:{Port}/uploads/{fileName}";
                            
                            var photo = new
                            {
                                id = Path.GetFileNameWithoutExtension(fileName), // 使用文件名作为ID
                                img_src = imageUrl,
                                hash = fileHash
                            };
                            
                            photoList.Add(photo);
                        }
                        catch (Exception ex)
                        {
                            StatusChanged?.Invoke($"处理文件 {filePath} 时出错: {ex.Message}");
                        }
                    }
                }
                else
                {
                    StatusChanged?.Invoke("上传目录不存在，返回空列表");
                }
                
                StatusChanged?.Invoke($"返回 {photoList.Count} 张照片");
                return SimpleJsonSerializer.Serialize(photoList.ToArray());
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"获取照片列表失败: {ex.Message}");
                ErrorOccurred?.Invoke(ex);
                
                // 返回空数组而不是错误，保持API一致性
                return SimpleJsonSerializer.Serialize(new object[0]);
            }
        }

        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hashBytes = md5.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch
            {
                // 如果无法计算哈希值，返回基于文件名和大小的简单哈希
                FileInfo fileInfo = new FileInfo(filePath);
                return $"file_{fileInfo.Name.GetHashCode():x8}_{fileInfo.Length:x8}";
            }
        }

        private async Task<string> HandleUpload(HttpListenerRequest request)
        {
            try
            {
                if (request.HttpMethod != "POST")
                {
                    return CreateJsonResponse("error", "Only POST method allowed for upload", null);
                }

                StatusChanged?.Invoke($"收到上传请求 - Content-Type: {request.ContentType}");
                StatusChanged?.Invoke($"Content-Length: {request.ContentLength64}");

                // 读取整个请求体
                byte[] requestData;
                using (var memoryStream = new MemoryStream())
                {
                    await request.InputStream.CopyToAsync(memoryStream);
                    requestData = memoryStream.ToArray();
                }

                StatusChanged?.Invoke($"读取请求数据: {requestData.Length} 字节");

                // 解析multipart数据
                var parseResult = ParseMultipartDataImproved(requestData, request.ContentType);
                
                if (parseResult.photoData == null || string.IsNullOrEmpty(parseResult.hashValue))
                {
                    StatusChanged?.Invoke("解析multipart数据失败");
                    return CreateJsonResponse("error", "Failed to parse multipart data", null);
                }

                StatusChanged?.Invoke($"解析成功 - 文件大小: {parseResult.photoData.Length} 字节");
                StatusChanged?.Invoke($"文件名: {parseResult.fileName}");
                StatusChanged?.Invoke($"Hash值: {parseResult.hashValue}");

                // 确定文件扩展名
                string fileExtension = GetFileExtension(parseResult.fileName, parseResult.contentType);
                string savedFileName = $"uploaded_{DateTime.Now.Ticks}{fileExtension}";
                
                // 创建保存目录
                string savedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WpfApp2_Uploads");
                Directory.CreateDirectory(savedPath);
                
                // 保存文件
                string fullPath = Path.Combine(savedPath, savedFileName);
                File.WriteAllBytes(fullPath, parseResult.photoData);
                
                StatusChanged?.Invoke($"文件已保存到: {fullPath}");

                // 创建上传结果 - 直接返回Photo对象格式
                var uploadResult = new
                {
                    id = Guid.NewGuid().ToString(),
                    img_src = $"http://{GetLocalIPAddress()}:{Port}/uploads/{savedFileName}",
                    hash = parseResult.hashValue
                };

                StatusChanged?.Invoke("上传处理完成");
                return SimpleJsonSerializer.Serialize(uploadResult);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"上传失败: {ex.Message}");
                return CreateJsonResponse("error", $"Upload failed: {ex.Message}", null);
            }
        }

        private (byte[] photoData, string hashValue, string fileName, string contentType) ParseMultipartDataImproved(byte[] data, string requestContentType)
        {
            try
            {
                // 提取boundary
                string boundary = ExtractBoundary(requestContentType);
                if (string.IsNullOrEmpty(boundary))
                {
                    return (null, null, null, null);
                }

                string boundaryStr = "--" + boundary;
                string endBoundaryStr = "--" + boundary + "--";
                
                // 将数据转换为字符串进行初步解析
                string dataString = Encoding.UTF8.GetString(data);
                
                // 分割各个部分
                string[] parts = dataString.Split(new string[] { boundaryStr }, StringSplitOptions.RemoveEmptyEntries);
                
                byte[] photoData = null;
                string hashValue = null;
                string fileName = null;
                string contentType = null;

                foreach (string part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part) || part.StartsWith("--"))
                        continue;

                    // 分离头部和内容
                    int headerEndIndex = part.IndexOf("\r\n\r\n");
                    if (headerEndIndex < 0)
                        continue;

                    string headers = part.Substring(0, headerEndIndex);
                    string content = part.Substring(headerEndIndex + 4);

                    if (headers.Contains("name=\"photo\""))
                    {
                        // 文件部分
                        fileName = ExtractFileName(headers);
                        contentType = ExtractContentType(headers);
                        
                        // 移除末尾的boundary标记
                        content = content.TrimEnd('\r', '\n', '-');
                        
                        // 对于二进制文件，我们需要重新从原始字节数据中提取
                        int startIndex = IndexOfBytes(data, Encoding.UTF8.GetBytes("\r\n\r\n"), 
                            IndexOfBytes(data, Encoding.UTF8.GetBytes("name=\"photo\"")));
                        
                        if (startIndex > 0)
                        {
                            startIndex += 4; // 跳过 \r\n\r\n
                            
                            // 查找下一个boundary的位置
                            int endIndex = IndexOfBytes(data, Encoding.UTF8.GetBytes("\r\n--" + boundary), startIndex);
                            if (endIndex > startIndex)
                            {
                                int length = endIndex - startIndex;
                                photoData = new byte[length];
                                Array.Copy(data, startIndex, photoData, 0, length);
                            }
                        }
                    }
                    else if (headers.Contains("name=\"hash\""))
                    {
                        // hash字段
                        hashValue = content.Trim('\r', '\n', '-');
                    }
                }

                return (photoData, hashValue, fileName, contentType);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"解析multipart数据时出错: {ex.Message}");
                return (null, null, null, null);
            }
        }

        private string ExtractBoundary(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return null;

            string[] parts = contentType.Split(';');
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                if (trimmedPart.StartsWith("boundary="))
                {
                    return trimmedPart.Substring("boundary=".Length);
                }
            }
            return null;
        }

        private string ExtractFileName(string headers)
        {
            // 查找 filename="xxx"
            int filenameIndex = headers.IndexOf("filename=\"");
            if (filenameIndex >= 0)
            {
                int startIndex = filenameIndex + "filename=\"".Length;
                int endIndex = headers.IndexOf("\"", startIndex);
                if (endIndex > startIndex)
                {
                    return headers.Substring(startIndex, endIndex - startIndex);
                }
            }
            return "unknown";
        }

        private string ExtractContentType(string headers)
        {
            // 查找 Content-Type: xxx
            int contentTypeIndex = headers.IndexOf("Content-Type:");
            if (contentTypeIndex >= 0)
            {
                int startIndex = contentTypeIndex + "Content-Type:".Length;
                int endIndex = headers.IndexOf("\r\n", startIndex);
                if (endIndex < 0) endIndex = headers.Length;
                return headers.Substring(startIndex, endIndex - startIndex).Trim();
            }
            return "application/octet-stream";
        }

        private string GetFileExtension(string fileName, string contentType)
        {
            // 首先尝试从文件名获取扩展名
            if (!string.IsNullOrEmpty(fileName) && fileName.Contains("."))
            {
                return Path.GetExtension(fileName);
            }
            
            // 根据Content-Type确定扩展名
            if (!string.IsNullOrEmpty(contentType))
            {
                switch (contentType.ToLower())
                {
                    case "image/jpeg":
                    case "image/jpg":
                        return ".jpg";
                    case "image/png":
                        return ".png";
                    case "image/gif":
                        return ".gif";
                    case "image/webp":
                        return ".webp";
                    default:
                        return ".jpg"; // 默认为jpg
                }
            }
            
            return ".jpg"; // 默认扩展名
        }

        private int IndexOfBytes(byte[] array, byte[] pattern, int startIndex = 0)
        {
            if (pattern.Length > array.Length - startIndex)
                return -1;

            for (int i = startIndex; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        private string HandleStatus(HttpListenerRequest request)
        {
            var statusData = new
            {
                server_running = _isRunning,
                port = Port,
                start_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                requests_handled = "N/A"
            };

            return CreateJsonResponse("success", "Server status", statusData);
        }

        private string CreateJsonResponse(string status, string message, object data)
        {
            var response = new
            {
                //status = status,
                //message = message,
                data = data,
                //timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return SimpleJsonSerializer.Serialize(response);
        }
    }
}