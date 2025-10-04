using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using EmbedIO.WebSockets;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050:在命名空间中声明类型", Justification = "<挂起>")]
public class GerenicRequest
{
#pragma warning disable IDE1006 // 命名样式
	public int number1 { get; set; } = 0;
	public int number2 { get; set; } = 0;
	public long number3 { get; set; } = 0;
	public ulong number4 { get; set; } = 0;
	public string text1 { get; set; } = "";
	public string text2 { get; set; } = "";
	public string text3 { get; set; } = "";
	public string text4 { get; set; } = "";
#pragma warning restore IDE1006 // 命名样式
}

public class ApiController : WebApiController
{
	private static SerialPort? _serialPort;
	private static readonly object _lock = new object();

	/* ---- */
	// 选取文件和目录
	[Route(HttpVerbs.Get, "/pick-directory")]
	public async Task<object> PickDirectory()
	{
		// 由于在桌面环境中，可以直接显示对话框
		var tcs = new TaskCompletionSource<object>();

		var thread = new Thread(() =>
		{
			try
			{
				using (var folderDialog = new FolderBrowserDialog())
				{
					folderDialog.Description = "请选择目录";
					folderDialog.ShowNewFolderButton = true;
					folderDialog.RootFolder = Environment.SpecialFolder.Desktop;

					if (folderDialog.ShowDialog() == DialogResult.OK)
					{
						tcs.SetResult(new { value = folderDialog.SelectedPath });
					}
					else
					{
						tcs.SetResult(string.Empty);
					}
				}
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);
			}
		});

		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();

		return await tcs.Task;
	}

	/* ---- */
	// 安装与卸载
	[Route(HttpVerbs.Get, "/settings/installed")]
	public object CheckInstalled()
	{
		return new { installed = File.Exists("setup.flag") };
	}

	[Route(HttpVerbs.Post, "/settings/install")]
	public async Task<object> InstallApp()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			try
			{
				bool forAllUsers = data.number1 == 1;
				string installLocation = data.text1;

				var args = new List<string> { "install" };

				if (data.number1 == 1)
					args.Add("--for-all-users");

				if (!string.IsNullOrEmpty(data.text1))
					args.Add($"--install-location={data.text1}");

				// 调用自己
				var process = Process.Start(new ProcessStartInfo
				{
					FileName = Environment.ProcessPath,
					Arguments = string.Join(" ", args),
					UseShellExecute = false
				});

				return new { success = true };
			}
			catch (Exception ex)
			{
				return new { error = ex.Message };
			}
		}
	}

	[Route(HttpVerbs.Post, "/settings/uninstall")]
	public object UninstallApp()
	{
		lock (_lock)
		{
			try
			{
				var process = Process.Start(new ProcessStartInfo
				{
					FileName = Environment.ProcessPath,
					Arguments = "uninstall",
					UseShellExecute = false
				});

				return new { success = true };
			}
			catch (Exception ex)
			{
				return new { error = ex.Message };
			}
		}
	}
	[Route(HttpVerbs.Get, "/platform")]
	public object GetPlatformInfo()
	{
		return new {
			platform = "Windows",
			version = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
			arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
			runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
		};
	}

	/* ---- */
	// 文件系统
	[Route(HttpVerbs.Get, "/fs/object")]
	public async Task GetFile()
	{
		var name = HttpContext.Request.QueryString["name"];

		if (string.IsNullOrEmpty(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("文件名不能为空", "text/plain", Encoding.UTF8);
			return;
		}

		// 防止路径遍历攻击
		if (name.Contains("..") || Path.IsPathRooted(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("无效的文件名", "text/plain", Encoding.UTF8);
			return;
		}

		var filePath = Path.Combine(Program.appDataDir, name);

		try
		{
			if (!File.Exists(filePath))
			{
				HttpContext.Response.StatusCode = 404;
				await HttpContext.SendStringAsync("文件不存在", "text/plain", Encoding.UTF8);
				return;
			}

			var fileBytes = await File.ReadAllBytesAsync(filePath);
			HttpContext.Response.ContentType = "application/octet-stream";
			await HttpContext.Response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
		}
		catch (Exception ex)
		{
			HttpContext.Response.StatusCode = 500;
			await HttpContext.SendStringAsync($"读取文件失败: {ex.Message}", "text/plain", Encoding.UTF8);
			return;
		}
	}

	[Route(HttpVerbs.Head, "/fs/object")]
	public async Task HeadFile()
	{
		var name = HttpContext.Request.QueryString["name"];
		await Task.FromResult(0);

		if (string.IsNullOrEmpty(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("文件名不能为空", "text/plain", Encoding.UTF8);
			return;
		}

		// 防止路径遍历攻击
		if (name.Contains("..") || Path.IsPathRooted(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("无效的文件名", "text/plain", Encoding.UTF8);
			return;
		}

		var filePath = Path.Combine(Program.appDataDir, name);

		try
		{
			if (!File.Exists(filePath))
			{
				HttpContext.Response.StatusCode = 404;
				await HttpContext.SendStringAsync("文件不存在", "text/plain", Encoding.UTF8);
				return;
			}

			var fileInfo = new FileInfo(filePath);

			// 设置文件元数据头
			HttpContext.Response.Headers["X-File-Size"] = fileInfo.Length.ToString();
			HttpContext.Response.Headers["X-File-Creation"] = fileInfo.CreationTimeUtc.ToString("yyyyMMddTHHmmss.fffZ");
			HttpContext.Response.Headers["X-File-Last-Modified"] = fileInfo.LastWriteTimeUtc.ToString("yyyyMMddTHHmmss.fffZ");
			HttpContext.Response.Headers["X-File-Last-Access"] = fileInfo.LastAccessTimeUtc.ToString("yyyyMMddTHHmmss.fffZ");

			// HEAD请求不返回内容体，只返回头部
			HttpContext.Response.StatusCode = 200;
			return;
		}
		catch (Exception ex)
		{
			HttpContext.Response.StatusCode = 500;
			await HttpContext.SendStringAsync($"获取文件信息失败: {ex.Message}", "text/plain", Encoding.UTF8);
			return;
		}
	}

	[Route(HttpVerbs.Put, "/fs/object")]
	public async Task PutFile()
	{
		var name = HttpContext.Request.QueryString["name"];

		if (string.IsNullOrEmpty(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("文件名不能为空", "text/plain", Encoding.UTF8);
			return;
		}

		// 防止路径遍历攻击
		if (name.Contains("..") || Path.IsPathRooted(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("无效的文件名", "text/plain", Encoding.UTF8);
			return;
		}

		var filePath = Path.Combine(Program.appDataDir, name);

		try
		{
			// 确保目录存在
			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// 读取请求体中的二进制数据
			using var stream = HttpContext.OpenRequestStream();
			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);
			var fileBytes = memoryStream.ToArray();

			// 写入文件
			await File.WriteAllBytesAsync(filePath, fileBytes);

			HttpContext.Response.StatusCode = 201;
			return;
		}
		catch (Exception ex)
		{
			HttpContext.Response.StatusCode = 500;
			await HttpContext.SendStringAsync($"写入文件失败: {ex.Message}", "text/plain", Encoding.UTF8);
			return;
		}
	}

	[Route(HttpVerbs.Delete, "/fs/object")]
	public async Task DeleteFile()
	{
		var name = HttpContext.Request.QueryString["name"];
		await Task.FromResult(0);

		if (string.IsNullOrEmpty(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("文件名不能为空", "text/plain", Encoding.UTF8);
			return;
		}

		// 防止路径遍历攻击
		if (name.Contains("..") || Path.IsPathRooted(name))
		{
			HttpContext.Response.StatusCode = 400;
			await HttpContext.SendStringAsync("无效的文件名", "text/plain", Encoding.UTF8);
			return;
		}

		var filePath = Path.Combine(Program.appDataDir, name);

		try
		{
			if (!File.Exists(filePath))
			{
				HttpContext.Response.StatusCode = 404;
				await HttpContext.SendStringAsync("文件不存在", "text/plain", Encoding.UTF8);
				return;
			}

			File.Delete(filePath);

			// 返回204 No Content
			HttpContext.Response.StatusCode = 204;
			return;
		}
		catch (Exception ex)
		{
			HttpContext.Response.StatusCode = 500;
			await HttpContext.SendStringAsync($"删除文件失败: {ex.Message}", "text/plain", Encoding.UTF8);
			return;
		}
	}

	/* ---- */
	// 串口

	[Route(HttpVerbs.Get, "/serial/scan")]
	public object ScanPorts()
	{
		var ports = SerialPort.GetPortNames();
		return new { ports };
	}

	[Route(HttpVerbs.Post, "/serial/connect")]
	public async Task<object> Connect()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			if (_serialPort?.IsOpen == true)
				return new { success = true };

			try
			{
				_serialPort = new SerialPort(data.text1, data.number1);
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
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { error = "串口未连接" };

			try
			{
				// AT模式切换逻辑
				string command = data.number1 == 0 ? "+++\r\n" : $"AT+EXIT={data.number1}\r\n";
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

	[Route(HttpVerbs.Post, "/serial/baudrate")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:不需要赋值", Justification = "<挂起>")]
	public async Task<object> SetBaudrate()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { data = "串口未连接" };

			return new { data = "未实现" };
		}
	}

	[Route(HttpVerbs.Post, "/serial/moduleaddr")]
	public async Task<object> SetModuleAddr()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { data = "串口未连接" };

			try
			{
				_serialPort.Write($"AT+ADDR={data.number1}\r\n");

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

	[Route(HttpVerbs.Get, "/serial/moduleaddr")]
	public object GetModuleAddr()
	{
		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { error = "串口未连接" };

			try
			{
				_serialPort.Write("AT+ADDR?\r\n");
				Thread.Sleep(100);
				string response = _serialPort.ReadExisting();

				return new { data = response };
			}
			catch (Exception ex)
			{
				return new { error = ex.Message };
			}
		}
	}

	[Route(HttpVerbs.Post, "/serial/send")]
	public async Task<object> SendData()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { data = "串口未连接" };

			try
			{
				_serialPort.Write(data.text1);

				return new { };
			}
			catch (Exception ex)
			{
				return new { data = ex.Message };
			}
		}
	}

	[Route(HttpVerbs.Get, "/serial/recv")]
	public object RecvData()
	{
		lock (_lock)
		{
			if (_serialPort?.IsOpen != true)
				return new { data = "串口未连接" };

			try
			{
				string response = _serialPort.ReadExisting();

				return new { data = response };
			}
			catch (Exception ex)
			{
				return new { data = ex.Message };
			}
		}
	}

	/* ---- */
	// 蓝牙
	static readonly Guid ServiceUuid = Guid.Parse("00002760-08c2-11e1-9073-0e8ac72e1001");
	static readonly Guid NotifyCharUuid = Guid.Parse("00002760-08c2-11e1-9073-0e8ac72e0002");
	static readonly Guid WriteCharUuid = Guid.Parse("00002760-08c2-11e1-9073-0e8ac72e0001");

	static readonly object _recvLock = new();
	static readonly List<byte> _recvBuffer = [];
	static DateTime _lastRecv = DateTime.MinValue;
	static CancellationTokenSource _recvMonitorCts = new();
	static bool bluetoothConnected = false;

	static async Task RecvMonitorLoopAsync(CancellationToken token)
	{
		var inactivityMs = 80; // 若设备很碎片化，这个值可调大一点（如 150ms）
		while (!token.IsCancellationRequested)
		{
			try
			{
				List<byte> frame = [];
				lock (_recvLock)
				{
					if (_recvBuffer.Count > 0 && (DateTime.UtcNow - _lastRecv).TotalMilliseconds > inactivityMs)
					{
#pragma warning disable IDE0028 // 简化集合初始化
						frame = new List<byte>(_recvBuffer);
						_recvBuffer.Clear();
					}
				}

				if (frame.Count > 0)
				{
					ProcessFrame(frame.ToArray());
#pragma warning restore IDE0028 // 简化集合初始化
				}

				await Task.Delay(30, token);
			}
			catch (TaskCanceledException) { break; }
			catch (Exception ex)
			{
				Console.WriteLine($"RecvMonitor 异常: {ex.Message}");
			}
		}
	}

	[Route(HttpVerbs.Get, "/bluetooth/scandevices")]
	public async Task<object> ScanBluetoothDevices()
	{
		try
		{
			var found = new ConcurrentDictionary<ulong, (string name, short rssi)>();

			var watcher = new BluetoothLEAdvertisementWatcher
			{
				ScanningMode = BluetoothLEScanningMode.Active
			};

			_recvMonitorCts = new CancellationTokenSource();
			_ = Task.Run(() => RecvMonitorLoopAsync(_recvMonitorCts.Token));

			watcher.Received += (s, e) =>
			{
				var addr = e.BluetoothAddress;
				var name = e.Advertisement?.LocalName;
				if (string.IsNullOrWhiteSpace(name)) name = "";
				// 更新字典（保留最近一次 RSSI）
				found.AddOrUpdate(addr, (name, e.RawSignalStrengthInDBm), (k, v) => (name, e.RawSignalStrengthInDBm));
			};

			watcher.Stopped += (s, e) =>
			{
				Console.WriteLine($"Watcher stopped: {e.Error}");
			};

			Console.WriteLine("开始扫描（10 秒）...");
			watcher.Start();
			await Task.Delay(TimeSpan.FromSeconds(10));
			watcher.Stop();

			if (found.IsEmpty)
			{
				return new { error = "未发现设备。请确认蓝牙已打开并靠近设备。" };
			}

			// 列出设备
			var list = found.OrderByDescending(kv => kv.Value.rssi).ToList();
			var ret = new List<object>();
			for (int i = 0; i < list.Count; i++)
			{
				var kv = list[i];
				ret.Add(new
				{
					index = i,
					addr = (kv.Key).ToString(),
					kv.Value.rssi,
					kv.Value.name,
				});
			}

			return new { devices = ret };
		}
		catch (Exception ex)
		{
			return new { error = ex.Message };
		}
	}

	[Route(HttpVerbs.Get, "/bluetooth/uuid")]
	public object GetBluetoothUUID()
	{
		return new
		{
			serviceUUID = ServiceUuid,
			characteristicUUID = WriteCharUuid,
			notificationUUID = NotifyCharUuid,
		};
	}

	public static BluetoothLEDevice? ble;
	public static GattDeviceService? ble_svc;
	public static GattCharacteristic? ble_notifyChar;
	public static GattCharacteristic? ble_writeChar;

	[Route(HttpVerbs.Post, "/bluetooth/connect")]
	public async Task<object> ConnectBluetoothDevice()
	{
		var data = await HttpContext.GetRequestDataAsync<GerenicRequest>();

		bool already = false;
		lock (_lock)
		{
			already = bluetoothConnected;
		}

		if (already) return new { success = true };
		if (!ulong.TryParse(data.text1, out ulong addr))
		{
			return new { success = false, error = "无效的设备地址" };
		}
		try
		{
			ble = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);
			if (ble == null)
			{
				lock (_lock) { bluetoothConnected = false; }
				return new { success = false, error = "连接失败：无法创建 BluetoothLEDevice。" };
			}
			var svcResult = await ble.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
			if (svcResult.Status != GattCommunicationStatus.Success || svcResult.Services.Count == 0)
			{
				ble.Dispose();
				ble = null;
				return new { success = false, error = $"未找到服务 {ServiceUuid}。Status={svcResult.Status}" };
			}
			ble_svc = svcResult.Services[0];
			Console.WriteLine($"找到服务：{ble_svc.Uuid}");
			var notifyRes = await ble_svc.GetCharacteristicsForUuidAsync(NotifyCharUuid, BluetoothCacheMode.Uncached);
			var writeRes = await ble_svc.GetCharacteristicsForUuidAsync(WriteCharUuid, BluetoothCacheMode.Uncached);
			ble_notifyChar = notifyRes.Characteristics[0];
			ble_writeChar = writeRes.Characteristics[0];
			ble_notifyChar.ValueChanged += NotifyChar_ValueChanged;
			var configStatus = await ble_notifyChar.WriteClientCharacteristicConfigurationDescriptorAsync(
				GattClientCharacteristicConfigurationDescriptorValue.Notify);
			if (configStatus != GattCommunicationStatus.Success)
			{
				ble_notifyChar.ValueChanged -= NotifyChar_ValueChanged;
				ble_notifyChar?.Service?.Dispose();
				ble_writeChar?.Service?.Dispose();
				ble?.Dispose();
				return new { success = false, error = $"订阅通知失败：{configStatus}" };
			}
			lock (_lock) { bluetoothConnected = true; }
			return new { success = true };
		}
		catch (Exception ex)
		{
			// 发生错误时重置连接状态
			lock (_lock) { bluetoothConnected = false; }
			return new { success = false, error = ex.Message };
		}
	}

	[Route(HttpVerbs.Post, "/bluetooth/disconnect")]
	public async Task<object> DisconnectBluetoothDevice()
	{
		lock (_lock)
		{
			if (!bluetoothConnected) return new { success = true };
		}

		try
		{
			if (ble_notifyChar != null)
			{
				try
				{
					ble_notifyChar.ValueChanged -= NotifyChar_ValueChanged;
					await ble_notifyChar.WriteClientCharacteristicConfigurationDescriptorAsync(
						GattClientCharacteristicConfigurationDescriptorValue.None);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"取消通知订阅时出错: {ex.Message}");
				}
				ble_notifyChar = null;
			}

			try
			{
				ble_writeChar?.Service?.Dispose();
				ble_notifyChar?.Service?.Dispose();
				ble_writeChar = null;
				ble_notifyChar = null;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"释放特征时出错: {ex.Message}");
			}

			if (ble != null)
			{
				try
				{
					ble.Dispose();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"断开设备时出错: {ex.Message}");
				}
				ble = null;
			}

			ble_svc = null; 
			lock (_lock) { bluetoothConnected = false; }
			return new { success = true };
		}
		catch (Exception ex)
		{
			return new { success = false, error = ex.Message };
		}
	}

	private static void NotifyChar_ValueChanged(GattCharacteristic s, GattValueChangedEventArgs e)
	{
		try
		{
			var reader = DataReader.FromBuffer(e.CharacteristicValue);
			var bytes = new byte[e.CharacteristicValue.Length];
			reader.ReadBytes(bytes);

			lock (_recvLock)
			{
				_recvBuffer.AddRange(bytes);
				_lastRecv = DateTime.UtcNow;
			}

		}
		catch (Exception ex)
		{
			Console.WriteLine($"Notify error: {ex.Message} (0x{ex.HResult:X})");
		}
	}

	private List<byte[]> framesCache = [];
	
	static void ProcessFrame(byte[] frame)
	{
		if (frame == null || frame.Length == 0) return;

		ChatModule.ForwardToWebSocket(frame);
	}
}


public class ChatModule : WebSocketModule
{
	private static ChatModule? _instance;
	private IWebSocketContext? _currentClient;
	private readonly object _clientLock = new();

	public ChatModule(string urlPath) : base(urlPath, true)
	{
		_instance = this;
	}

	// 从蓝牙接收数据后直接转发给WebSocket客户端
	public static void ForwardToWebSocket(byte[] data)
	{
		_instance?._ForwardToWebSocket(data);
	}

	private void _ForwardToWebSocket(byte[] data)
	{
		lock (_clientLock)
		{
			if (_currentClient?.WebSocket.State == WebSocketState.Open)
			{
				_ = SendAsync(_currentClient, data);
			}
		}
	}

	protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
	{
		// 直接将收到的二进制数据转发到蓝牙设备
		SendToBluetoothDevice(buffer);
		return Task.CompletedTask;
	}

	protected override Task OnClientConnectedAsync(IWebSocketContext context)
	{
		lock (_clientLock)
		{
			_currentClient = context;
		}
		return Task.CompletedTask;
	}

	protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
	{
		lock (_clientLock)
		{
			if (_currentClient == context)
			{
				_currentClient = null;
			}
		}
		return Task.CompletedTask;
	}

	private async void SendToBluetoothDevice(byte[] data)
	{
		if (ApiController.ble_writeChar != null)
		try {
			var writer = new DataWriter();
			writer.WriteBytes(data);
			var status = await ApiController.ble_writeChar.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
				Console.WriteLine($"写入状态: {status}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"写入异常: {ex.Message} (0x{ex.HResult:X})");
		}
	}
}

