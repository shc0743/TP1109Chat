using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;

class Program
{
	static async Task Main()
	{
		int port = new Random().Next(60000, 65536);
		string url = $"http://localhost:{port}/";

        //foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        //{
        //	Console.WriteLine(name);
        //}

        // 解压 web/dist 到临时目录
        string exeDir = AppContext.BaseDirectory;
        string appRoot = Path.Combine(exeDir, "data");
        // 检查是否安装版
        string setupFlag = Path.Combine(exeDir, "setup.flags");
        if (File.Exists(setupFlag))
        {
            // 安装版 → 放到 %LocalAppData%\MyApp\data
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            appRoot = Path.Combine(localAppData, "TP1109ChatWebApp", "data");
        }
        Directory.CreateDirectory(appRoot);

        // 启动 EmbedIO 服务器
        using var server = new WebServer(o => o
			.WithUrlPrefix(url)
			.WithMode(HttpListenerMode.EmbedIO));

		server.WithWebApi("/api", m => m.WithController<ApiController>())
			  .WithModule(new EmbeddedStaticFileModule("/"));

		_ = server.RunAsync();

        // 提取并运行 win64-webview.exe
        string webviewPath = Path.Combine(appRoot, "win64-webview.exe");
        try
        {
            ExtractResource("TP1109ChatWebApp.win64-webview.exe", webviewPath);
            ExtractResource("TP1109ChatWebApp.WebView2Loader.dll", Path.Combine(appRoot, "WebView2Loader.dll"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        if (!File.Exists(webviewPath))
            throw new Exception("win64-webview.exe not found after extraction!");

        using var webviewProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = webviewPath,
                Arguments = $"{url} ./",
                UseShellExecute = false
            }
        };

        webviewProcess.Start();

        // 等待浏览器关闭
        await webviewProcess.WaitForExitAsync();

        // 浏览器关闭后停止 EmbedIO
        server.Dispose();

        Console.WriteLine("Browser closed. Server stopped. Exiting...");
    }

	// 简单的 API
	public class ApiController : WebApiController
	{
        private static SerialPort? _serialPort;
        private static readonly object _lock = new object();

        [Route(HttpVerbs.Get, "/serial/scan")]
        public object ScanPorts()
        {
            var ports = SerialPort.GetPortNames();
            return new { ports };
        }

        [Route(HttpVerbs.Post, "/serial/connect")]
        public async Task<object> Connect()
        {
            var data = await HttpContext.GetRequestDataAsync<ConnectRequest>();

            lock (_lock)
            {
                if (_serialPort?.IsOpen == true)
                    return new { error = "串口已连接" };

                try
                {
                    _serialPort = new SerialPort(data.Port, data.BaudRate);
                    _serialPort.Open();
                    return new { success = true };
                }
                catch (Exception ex)
                {
                    return new { error = ex.Message };
                }
            }
        }

        [Route(HttpVerbs.Post, "/serial/disconnect")]
        public object Disconnect()
        {
            lock (_lock)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
                return new { success = true };
            }
        }

        [Route(HttpVerbs.Post, "/serial/mode")]
        public async Task<object> ToggleMode()
        {
            var data = await HttpContext.GetRequestDataAsync<ModeRequest>();

            lock (_lock)
            {
                if (_serialPort?.IsOpen != true)
                    return new { error = "串口未连接" };

                try
                {
                    // AT模式切换逻辑
                    string command = data.Mode == 0 ? "AT+MODE=0\r\n" : "AT+MODE=1\r\n";
                    _serialPort.Write(command);

                    // 读取响应
                    Thread.Sleep(100);
                    string response = _serialPort.ReadExisting();

                    return new { success = response.Contains("OK") };
                }
                catch (Exception ex)
                {
                    return new { error = ex.Message };
                }
            }
        }
    }

    // 提取单个文件
    static void ExtractResource(string resourceName, string outputPath)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new Exception($"Resource {resourceName} not found.");
		using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
		stream.CopyTo(file);
	}
}


class EmbeddedStaticFileModule : WebModuleBase
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly string _rootNamespace = "TP1109ChatWebApp"; // 根据你的项目默认命名空间

    public EmbeddedStaticFileModule(string urlPath) : base(urlPath) { }

    public override bool IsFinalHandler => true;

    protected override async Task OnRequestAsync(IHttpContext context)
    {
        string path = context.RequestedPath.TrimStart('/');

        // 嵌入资源名匹配
        string resourceName = path switch
        {
            "" => _rootNamespace + ".index.html", // SPA 首页
            _ => _rootNamespace + "." + path.Replace("/", ".")
        };

        var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // SPA fallback
            stream = _assembly.GetManifestResourceStream(_rootNamespace + ".index.html");
        }

        if (stream != null)
        {
            context.Response.ContentType = GetContentType(resourceName);
            await stream.CopyToAsync(context.Response.OutputStream);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    }

    private static string GetContentType(string resourceName)
    {
        return resourceName.EndsWith(".css") ? "text/css"
             : resourceName.EndsWith(".js") ? "application/javascript"
             : resourceName.EndsWith(".html") ? "text/html"
             : resourceName.EndsWith(".svg") ? "image/svg+xml"
             : "application/octet-stream";
    }
}

public class ConnectRequest
{
    public string Port { get; set; } = "";
    public int BaudRate { get; set; }
}

public class ModeRequest
{
    public int Mode { get; set; }
}
