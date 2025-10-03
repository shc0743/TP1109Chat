using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Graph.Models;
using Microsoft.Win32;
using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Windows.Foundation.Collections;

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
class ShellLink
{
}
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
interface IShellLinkW
{
	void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
	void GetIDList(out IntPtr ppidl);
	void SetIDList(IntPtr pidl);
	void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
	void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
	void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
	void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
	void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
	void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
	void GetHotkey(out ushort pwHotkey);
	void SetHotkey(ushort wHotkey);
	void GetShowCmd(out int piShowCmd);
	void SetShowCmd(int iShowCmd);
	void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
	void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
	void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
	void Resolve(IntPtr hwnd, uint fFlags);
	void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
interface IPropertyStore
{
	void GetCount(out uint cProps);
	void GetAt(uint iProp, out PropertyKey pkey);
	void GetValue(ref PropertyKey key, out PropVariant pv);
	void SetValue(ref PropertyKey key, ref PropVariant pv);
	void Commit();
}
[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
	public Guid fmtid;
	public uint pid;

	public PropertyKey(Guid fmtid, uint pid)
	{
		this.fmtid = fmtid;
		this.pid = pid;
	}
}
[StructLayout(LayoutKind.Explicit)]
public struct PropVariant
{
	[FieldOffset(0)] public ushort vt;
	[FieldOffset(8)] public IntPtr pointerValue;
	[FieldOffset(8)] public byte byteValue;
	[FieldOffset(8)] public long longValue;
	[FieldOffset(8)] public double doubleValue;

	public static PropVariant FromString(string value)
	{
		var pv = new PropVariant();
		pv.vt = 31; // VT_LPWSTR
		pv.pointerValue = Marshal.StringToCoTaskMemUni(value);
		return pv;
	}

	public void Clear()
	{
		if (vt == 31 && pointerValue != IntPtr.Zero) // VT_LPWSTR
		{
			Marshal.FreeCoTaskMem(pointerValue);
		}
		vt = 0;
		pointerValue = IntPtr.Zero;
	}
}

class Program
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
	[System.Runtime.InteropServices.DllImport("shell32.dll")]
	static extern bool IsUserAnAdmin();

	static async Task<int> Main(string[] args)
	{
		if (args.Length == 0)
		{
			await MainAppAsync();
			return 0;
		}

		var rootCommand = new RootCommand();

		var installCommand = new Command("install", "Install the application");
		var forAllUsersOption = new Option<bool>("--for-all-users", "Install for all users");
		var installLocationOption = new Option<string>("--install-location", "Installation directory");

		installCommand.AddOption(forAllUsersOption);
		installCommand.AddOption(installLocationOption);

		installCommand.SetHandler((forAllUsers, location) =>
			InstallAsync(forAllUsers, location), forAllUsersOption, installLocationOption);

		var uninstallCommand = new Command("uninstall", "Uninstall the application");
		uninstallCommand.SetHandler(UninstallAsync);

		rootCommand.AddCommand(installCommand);
		rootCommand.AddCommand(uninstallCommand);

		return await rootCommand.InvokeAsync(args);
	}

	private static void SetPropertyString(IPropertyStore propertyStore, PropertyKey key, string value)
	{
		var pv = PropVariant.FromString(value);
		try
		{
			propertyStore.SetValue(ref key, ref pv);
		}
		finally
		{
			pv.Clear();
		}
	}
	private static readonly PropertyKey PKEY_AppUserModel_ID = new PropertyKey(new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"), 5);

	static async Task<int> InstallAsync(bool forAllUsers, string installLocation)
	{
		Console.WriteLine($"Installing for all users: {forAllUsers}");
		Console.WriteLine($"Install location: {installLocation}");

#if DEBUG
		MessageBoxW(IntPtr.Zero, "调试版本无法安装", "错误", 0x10);
		return 1;
#endif
		// 处理installLocation
		if (String.IsNullOrEmpty(installLocation))
		{
			if (forAllUsers) installLocation = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			else installLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

			installLocation = Path.Combine(installLocation, AppUserModel.AppId);
		}

		// 如果不是以管理员身份运行并且forAllUsers那么提权
		if (forAllUsers && !IsUserAnAdmin())
		{
			var process = new System.Diagnostics.Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = Environment.ProcessPath,
					Arguments = $"install --for-all-users --install-location=\"{installLocation}\"",
					Verb = "runas", // 请求管理员权限
					UseShellExecute = true
				}
			};
			process.Start();
			return 0; // 当前进程退出
		}

		if (MessageBoxW(IntPtr.Zero, "确认安装到：" + installLocation, AppUserModel.AppId, 4) != 6)
		{
			return 1223;
		}

		// 把程序本体复制过去
		if (File.Exists(installLocation) || Directory.Exists(installLocation))
		{
			if (MessageBoxW(IntPtr.Zero, "目标文件夹已存在，是否确认? 继续操作将删除目标文件夹的内容!\n" + installLocation, AppUserModel.AppId, 4) != 6)
			{
				return 1223;
			}
			try
			{
				Directory.Delete(installLocation, true);
			}
			catch {
				MessageBoxW(IntPtr.Zero, "删除失败，无法安装!", "错误", 0x10);
			}
		}
		if (Environment.ProcessPath == null)
		{
			MessageBoxW(IntPtr.Zero, "环境异常，无法安装!", "错误", 0x10);
			return 1;
		}

		await Task.Delay(100);
		Directory.CreateDirectory(installLocation);
		string exeName = Path.GetFileName(Environment.ProcessPath);
		File.Copy(Environment.ProcessPath, Path.Combine(installLocation, exeName), true);
		File.Create(Path.Combine(installLocation, "setup.flag"));
		if (forAllUsers) File.Create(Path.Combine(installLocation, "setup-global.flag"));

		// 写入注册表
		RegistryKey registry = forAllUsers ? Registry.LocalMachine : Registry.CurrentUser;
		// 在 Uninstall 下面创建 {AppUserModel.AppId} 项
		string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppUserModel.AppId;
		using (RegistryKey uninstallKey = registry.CreateSubKey(uninstallPath))
		{
			if (uninstallKey == null)
			{
				MessageBoxW(IntPtr.Zero, "安装信息创建失败，无法安装!", "错误", 0x10);
				return -1;
			}
			uninstallKey.SetValue("DisplayName", "TP1109 Chat App");
			uninstallKey.SetValue("DisplayVersion", "1.0.0");
			uninstallKey.SetValue("InstallLocation", installLocation);
			uninstallKey.SetValue("UninstallString", $"\"{Path.Combine(installLocation, exeName)}\" uninstall");
			uninstallKey.SetValue("DisplayIcon", Path.Combine(installLocation, exeName));
			uninstallKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
			uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
			uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
		}

		// 在开始菜单创建快捷方式
		var shellLink = (IShellLinkW)new ShellLink();
		shellLink.SetPath(Path.Combine(installLocation, exeName));
		shellLink.SetArguments("");
		shellLink.SetWorkingDirectory(installLocation);
		shellLink.SetDescription("TP1109 Chat Web App");
		var propertyStore = (IPropertyStore)shellLink;
		SetPropertyString(propertyStore, PKEY_AppUserModel_ID, AppUserModel.AppId);
		var persistFile = (IPersistFile)shellLink;
		string path = forAllUsers ?
			Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) :
			Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
		path = Path.Combine(path, "TP1109 Chat Web App.lnk");
		persistFile.Save(path, false);

		// 安装成功~！
		if (MessageBoxW(IntPtr.Zero, "安装成功! 是否启动?", AppUserModel.AppId, 0x41) == 0x1)
		{
			System.Diagnostics.Process.Start(Path.Combine(installLocation, exeName));
		}

		return 0;
	}

	static async Task<int> UninstallAsync()
	{
		await Task.Delay(100);
		if (String.IsNullOrEmpty(Environment.ProcessPath))
		{
			MessageBoxW(IntPtr.Zero, "环境异常，无法安装!", "错误", 0x10);
			return 1;
		}
		string appPath = Environment.ProcessPath!;
		if (String.IsNullOrEmpty(appPath))
		{
			MessageBoxW(IntPtr.Zero, "环境异常，无法安装!", "错误", 0x10);
			return 1;
		}
		string appDir = Path.GetDirectoryName(appPath)!;
		if (String.IsNullOrEmpty(appDir))
		{
			MessageBoxW(IntPtr.Zero, "环境异常，无法安装!", "错误", 0x10);
			return 1;
		}
		bool forAllUsers = File.Exists(Path.Combine(appDir, "setup-global.flag"));
		if (forAllUsers && !IsUserAnAdmin())
		{
			var process = new System.Diagnostics.Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = Environment.ProcessPath,
					Arguments = $"uninstall",
					Verb = "runas", // 请求管理员权限
					UseShellExecute = true
				}
			};
			process.Start();
			return 0; // 当前进程退出
		}

		//  （确认对话框）
		if (MessageBoxW(IntPtr.Zero, "确定要卸载 TP1109 Chat App 吗？", AppUserModel.AppId, 0x34) != 6)
		{
			return 1223;
		}

		// 删除注册表
		string appName = Path.GetFileName(appPath);
		string dataDir = Path.Combine(appDir, "data");
		RegistryKey registry = forAllUsers ? Registry.LocalMachine : Registry.CurrentUser;
		string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppUserModel.AppId;
		try
		{
			registry.DeleteSubKeyTree(uninstallPath, false);
		}
		catch (Exception)
		{
			MessageBoxW(IntPtr.Zero, "注册表删除失败！这会导致残留！请手动清理！", "警告", 0x10);
		}

		// 删除文件
		try
		{
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string appDataRoot = Path.Combine(localAppData, AppUserModel.AppId);
			File.Delete(Path.Combine(appDir, "setup.flag"));
			if (forAllUsers) File.Delete(Path.Combine(appDir, "setup-global.flag"));
			string shortcutPath = forAllUsers ?
				Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) :
				Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
			shortcutPath = Path.Combine(shortcutPath, "TP1109 Chat Web App.lnk");
			File.Delete(shortcutPath);
			string batContent = $@"
@echo off
chcp 65001
:a
del /f /q ""{appPath}""
timeout /t 1
if exist ""{appPath}"" goto a
:b
rd /s /q ""{dataDir}""
timeout /t 1
if exist ""{dataDir}"" goto b
rd /s /q ""{appDataRoot}""
rd ""{appDir}""
del /f /q ""%~f0""
";

			// 写入临时BAT文件
			string tempBat = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bat");
			File.WriteAllText(tempBat, batContent, new UTF8Encoding());

			// 启动BAT文件（隐藏窗口）
			var process = new System.Diagnostics.Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = tempBat,
					WindowStyle = ProcessWindowStyle.Hidden,
					UseShellExecute = true
				}
			};
			process.Start();

			MessageBoxW(IntPtr.Zero, "TP1109ChatApp 已卸载。点击“确定”以完成卸载。", "成功", 0x40);
		}
		catch (Exception ex)
		{
			MessageBoxW(IntPtr.Zero, "卸载失败! " + ex, AppUserModel.AppId, 0x10);
			return 1;
		}

		return 0;
	}

	public static string appDataDir = "";

	static async Task<int> MainAppAsync()
	{
		int port = new Random().Next(60000, 65536);
		string url = $"http://localhost:{port}/";

		foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
		{
			Console.WriteLine(name);
		}

		// 解压 web/dist 到临时目录
		string exeDir = AppContext.BaseDirectory;
		Directory.SetCurrentDirectory(exeDir);
		string appRoot = Path.Combine(exeDir, "data");
		// 检查是否安装版
		string setupFlag = Path.Combine(exeDir, "setup.flag");
		if (File.Exists(setupFlag))
		{
			// 安装版 → 放到 %LocalAppData%\MyApp\data
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			appRoot = Path.Combine(localAppData, AppUserModel.AppId);
		}
		Directory.CreateDirectory(appRoot);
		Program.appDataDir = appRoot;

		bool createdNew = false, shouldSelectProfile = false;
		var mutex = new Mutex(false, $"Global\\{AppUserModel.AppId}@{Environment.UserName}", out createdNew);
		if (!createdNew) shouldSelectProfile = true;

		// 启动 EmbedIO 服务器
		using var server = new WebServer(o => o
			.WithUrlPrefix(url)
			.WithMode(HttpListenerMode.EmbedIO));
		
		server.WithWebApi("/api", m => m.WithController<ApiController>())
			  .WithModule(new ChatModule("/ws/chat"))
			  .WithModule(new EmbeddedStaticFileModule("/"));

		_ = server.RunAsync();

		// 设置 AppUserModelId
		AppUserModel.Initialize();

		// 提取并运行 win64-webview.exe
		string webviewPath = Path.Combine(appRoot, "win64-webview@1.3.0.exe");
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
		{
			_ = MessageBoxW(IntPtr.Zero, "Fatal Error: win64-webview.exe not found after extraction!", "Error", 0x10);
			throw new Exception("win64-webview.exe not found after extraction!");
		}

		string run_url = shouldSelectProfile ? $"{url}?select_profile=true" : url;

		using var webviewProcess = new System.Diagnostics.Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = webviewPath,
				Arguments = $"{run_url} ./ 800x600 {AppUserModel.AppId} {AppUserModel.AppId}",
				UseShellExecute = false
			}
		};

		webviewProcess.Start();

		// 等待浏览器关闭
		await webviewProcess.WaitForExitAsync();

		// 浏览器关闭后停止 EmbedIO
		server.Dispose();
		mutex?.Close();
		Console.WriteLine("Browser closed. Server stopped. Exiting...");
		return 0;
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


class EmbeddedStaticFileModule(string urlPath) : WebModuleBase(urlPath)
{
	private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
	private readonly string _rootNamespace = "TP1109ChatWebApp"; // 根据你的项目默认命名空间

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
		// SPA fallback
		stream ??= _assembly.GetManifestResourceStream(_rootNamespace + ".index.html");

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
