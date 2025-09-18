using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Net.WebSockets;
using System.Windows.Forms;
using Microsoft.Win32;

// Add custom attributes
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class SPAmodeAttribute : Attribute {
	private bool _spaMode;

	public SPAmodeAttribute() : this(false) { }
	public SPAmodeAttribute(bool spaMode) {
		_spaMode = spaMode;
	}
	public bool spaMode {
		get { return _spaMode; }
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class StartMaximizedAttribute : Attribute {
	private bool _startMaximized;

	public StartMaximizedAttribute() : this(false) { }
	public StartMaximizedAttribute(bool startMaximized) {
		_startMaximized = startMaximized;
	}
	public bool startMaximized {
		get { return _startMaximized; }
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class KioskAttribute : Attribute {
	private bool _kiosk;

	public KioskAttribute() : this(false) { }
	public KioskAttribute(bool kiosk) {
		_kiosk = kiosk;
	}
	public bool kiosk {
		get { return _kiosk; }
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MultipleInstanceAttribute : Attribute {
	private string _multipleInstance;
	
	public MultipleInstanceAttribute() : this("block") { }
	public MultipleInstanceAttribute(string multipleInstance) {
		_multipleInstance = multipleInstance;
	}
	public string multipleInstance {
		get { return _multipleInstance; }
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class StatelessAttribute : Attribute {
	private bool _stateless;

	public StatelessAttribute() : this(false) { }
	public StatelessAttribute(bool stateless) {
		_stateless = stateless;
	}
	public bool stateless {
		get { return _stateless; }
	}
}

// Web server based on https://www.technical-recipes.com/2016/creating-a-web-server-in-c/
public class WebServer {
	private readonly HttpListener _listener = new HttpListener();
	private readonly Func<HttpListenerRequest, HttpListenerResponse, byte[]> _responderMethod;
	private readonly Func<HttpListenerContext, Task> _webSocketHandler;
	
	public static IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
		{".css", "text/css"},
		{".gif", "image/gif"},
		{".html", "text/html"},
		{".ico", "image/x-icon"},
		{".jpeg", "image/jpeg"},
		{".jpg", "image/jpeg"},
		{".js", "application/javascript"},
		{".json", "application/json"},
		{".mjs", "application/javascript"},
		{".png", "image/png"},
		{".svg", "image/svg+xml"},
		{".ttf", "font/ttf"},
		{".txt", "text/plain"},
		{".woff2", "font/woff2"}
	}; // See https://github.com/Microsoft/referencesource/blob/master/System.Web/MimeMapping.cs or https://github.com/samuelneff/MimeTypeMap/blob/master/MimeTypeMap.cs to add some

	public WebServer(string prefix, Func<HttpListenerRequest, HttpListenerResponse, byte[]> httpHandler, Func<HttpListenerContext, Task> webSocketHandler) {
		// URI prefix is required eg: "http://localhost:8080/test/"
		if (string.IsNullOrEmpty(prefix)) {
			throw new ArgumentException("URI prefix is required");
		}
		
		_listener.Prefixes.Add(prefix);
		_responderMethod = httpHandler;
		_webSocketHandler = webSocketHandler;
		_listener.Start();
	}

	public void Run() {
		ThreadPool.QueueUserWorkItem(o => {
			Console.WriteLine("Webserver running...");
			try {
				while (_listener.IsListening) {
					ThreadPool.QueueUserWorkItem(async (ctx) => {
						HttpListenerContext context = ctx as HttpListenerContext;
						try {
							if (context.Request.IsWebSocketRequest) {
								await _webSocketHandler(context);
							} else {
								var buf = _responderMethod(context.Request, context.Response);
								context.Response.ContentLength64 = buf.Length;
								context.Response.OutputStream.Write(buf, 0, buf.Length);
							}
						} finally {
							// Always close the stream
							if (context != null) {
								context.Response.OutputStream.Close();
							}
						}
					}, _listener.GetContext());
				}
			} catch {
			}
		});
	}

	public void Stop() {
		_listener.Stop();
		_listener.Close();
	}
}

class Web2exe {
	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	const int SW_RESTORE = 9; // Restaure la fenêtre si elle est minimisée

	private static WebSocket webSocket;
	private static bool spaMode = false;
	private static string assemblyProduct;
	private static Process chromiumProcess = null;

	// Handle the HTTP requests
	public static byte[] SendResponse(HttpListenerRequest request, HttpListenerResponse response) {
		var filename = Uri.UnescapeDataString(request.Url.AbsolutePath.Substring(1));
		if (response.StatusCode == 404) { // Single Page Application mode: if the file has not been found, redirect to the root
			response.StatusCode = 200;
			filename = "";
		}
		Console.WriteLine("Request: /" + filename);
		if (filename == "") { // root URL -> index.html
			filename = "index.html";
		}
		
		// Set MIME type based on extension
		string mime;
		response.ContentType = WebServer._mimeTypeMappings.TryGetValue(Path.GetExtension(filename), out mime)?mime:"application/octet-stream";
		// MIME types are often defined in the registry, enabling a wide compatibility with a lot of extension
		response.ContentType = (string) Registry.GetValue(@"HKEY_CLASSES_ROOT\" + Path.GetExtension(filename), "Content Type", null) ?? response.ContentType;
		
		// Find file
		if (Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(filename + ".gz")) { // Check if the resource is embedded inside the executable
			Console.WriteLine("Reply (gzipped resource): " + filename + ".gz");
			response.AddHeader("Content-Encoding", "gzip");
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename + ".gz"))
			using (MemoryStream ms = new MemoryStream()) {
				stream.CopyTo(ms);
				return ms.ToArray();
			}
		} else if (Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(filename)) { // Check if the resource is embedded inside the executable
			Console.WriteLine("Reply (resource): " + filename);
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename))
			using (MemoryStream ms = new MemoryStream()) {
				stream.CopyTo(ms);
				return ms.ToArray();
			}
		} else {
			filename = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), filename);
			if (File.Exists(filename)) { // Check if the file exists in the same directory as the executable
				Console.WriteLine("Reply (file): " + filename);
				return File.ReadAllBytes(filename);
			}
		}
		if (Web2exe.spaMode && request.Url.AbsolutePath != "/") {
			response.StatusCode = 404;
			return SendResponse(request, response);
		}
		Console.WriteLine("File not found");
		response.StatusCode = 404;
		return null; // Otherwise, return nothing
	}
	
	// Handle WebSocket
	public static async Task WebSocketHandler(HttpListenerContext context) {
		HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
		Web2exe.webSocket = webSocketContext.WebSocket;

		Console.WriteLine("WebSocket client connected");

		byte[] buffer = new byte[1024];
		while (Web2exe.webSocket.State == WebSocketState.Open) {
			WebSocketReceiveResult result = await Web2exe.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
			if (result.MessageType == WebSocketMessageType.Close) {
				Console.WriteLine("Fermeture du WebSocket demandée.");
				await Web2exe.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fermeture demandée", CancellationToken.None);
				break;
			}
		}
		Web2exe.webSocket.Dispose();
	}

	// Listen to querystring sent from another instance, and communicate it to the web app via WebSocket
	static void HandleQuerystringFromAnotherInstance() {
		Thread listenerThread = new Thread(async () => {
			while (true) {
				using (NamedPipeServerStream server = new NamedPipeServerStream(Web2exe.assemblyProduct, PipeDirection.In)) { // NamedPipeServerStream can accept only one connection before being closed
					server.WaitForConnection();
					using (StreamReader reader = new StreamReader(server)) {
						string queryString = reader.ReadLine();
						// Set window to foreground
						if (Web2exe.chromiumProcess != null) {
							try {
								IntPtr hWnd = Web2exe.chromiumProcess.MainWindowHandle;
								if (hWnd != IntPtr.Zero) {
									ShowWindow(hWnd, SW_RESTORE);
									keybd_event(0, 0, 0, 0); // Simulate a key press. Needed for SetForegroundWindow to work in all cases
									SetForegroundWindow(hWnd);
								}
							} catch { }
						}
						// Send parameters through the websocket
						if (!string.IsNullOrEmpty(queryString) && Web2exe.webSocket != null && Web2exe.webSocket.State == WebSocketState.Open) {
							byte[] responseBuffer = Encoding.UTF8.GetBytes(queryString);
							await Web2exe.webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
						}
					}
				}
			}
		});
		listenerThread.IsBackground = true;
		listenerThread.Start();
	}

	// Send a message to an existing instance
	static void SendMessageToExistingInstance(string message) {
		try {
			using (NamedPipeClientStream client = new NamedPipeClientStream(".", Web2exe.assemblyProduct, PipeDirection.Out)) {
				client.Connect(1000); // Wait up to 1 second for connection to be established
				using (StreamWriter writer = new StreamWriter(client)) {
					writer.WriteLine(message);
				}
			}
		} catch {
			Console.WriteLine("Unable to send arguments to existing instance.");
		}
	}
	
	static void Main(string[] args) {
		Assembly assembly = Assembly.GetExecutingAssembly();
		Web2exe.assemblyProduct = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute), false)).Product;
		Web2exe.spaMode = ((SPAmodeAttribute)Attribute.GetCustomAttribute(assembly, typeof(SPAmodeAttribute), false)).spaMode;
		bool startMaximized = ((StartMaximizedAttribute)Attribute.GetCustomAttribute(assembly, typeof(StartMaximizedAttribute), false)).startMaximized;
		bool kiosk = ((KioskAttribute)Attribute.GetCustomAttribute(assembly, typeof(KioskAttribute), false)).kiosk;
		string multipleInstance = ((MultipleInstanceAttribute)Attribute.GetCustomAttribute(assembly, typeof(MultipleInstanceAttribute), false)).multipleInstance;
		bool stateless = ((StatelessAttribute)Attribute.GetCustomAttribute(assembly, typeof(StatelessAttribute), false)).stateless;

		// Transform the arguments into a querystring
		string queryString = "";
		if (args.Length > 0) {
			queryString = string.Join("&", args.Select(Uri.EscapeDataString));
		}

		string pidPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), Web2exe.assemblyProduct + ".pid");
		using (Mutex mutex = new Mutex(false, Web2exe.assemblyProduct)) {
			if (!mutex.WaitOne(0, false)) { // Another instance of the application is already running
				if (multipleInstance == "block") {
					MessageBox.Show("Another instance of this application is already running.", "Error: application already running", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				} else if (multipleInstance == "unique") {
					SendMessageToExistingInstance(queryString);
					return;
				}
			}
			
			// Set browser profile path
			string profilePath = Path.GetTempPath() + Web2exe.assemblyProduct + "#" + Process.GetCurrentProcess().Id + " Chromium profile";
			if (!stateless) {
				profilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), Web2exe.assemblyProduct + " data");
			}
			
			// Find an empty port
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			
			if (!stateless) { // Then, always use the same port to run the webserver
				string portFilePath = Path.Combine(profilePath, "web2exe.port");
				if (File.Exists(portFilePath)) {
					port = Int32.Parse(File.ReadAllText(portFilePath));
				} else {
					Directory.CreateDirectory(profilePath);
					File.WriteAllText(portFilePath, port.ToString());
				}
			}
			
			// Start the Web server
			var ws = new WebServer("http://localhost:" + port + "/", SendResponse, WebSocketHandler);
			ws.Run();
			Console.WriteLine("Listening on port " + port + "...");

			// Browser command line switches
			// --disable-extensions							Avoid extensions interference with the app
			// --disable-features=Translate,TranslateUI		Disable Chrome translation popup ("Would you like to translate this page?")
			// --disable-features=PersistentHistograms		Avoid the creation of a 4MB file in the profile folder
			// --disable-web-security						Disable CORS checks
			// --test-type									Has the effect to hide the warning message from the previous parameter
			// --disk-cache-dir=null						Disable file caching
			// --user-data-dir								Use a totally new profile. This way, the Chrome process is independant from any other than could already run on the device
			// --no-default-browser-check					Disable the default browser check
			// --app										Open the followed URL in app mode: no menu, no toolbar, no address bar...
			string switches = "--disable-extensions --disable-features=Translate,TranslateUI,PersistentHistograms --disable-web-security --test-type --disk-cache-dir=null --user-data-dir=\"" + profilePath + "\" --no-default-browser-check --app=http://localhost:" + port + "/?" + queryString;
			if (startMaximized) {
				switches += " --start-maximized";
			} else if (kiosk) {
				switches += " --kiosk";
			}

			// Open the website in a Chrome-based browser. Try Chrome first, then Edge if not found
			List<string>.Enumerator browsersEnum = (new List<string> {"chrome", "msedge"}).GetEnumerator();
			while (browsersEnum.MoveNext() && Web2exe.chromiumProcess == null) {
				try {
					// Start Chrome-based browser
					Web2exe.chromiumProcess = Process.Start(browsersEnum.Current, switches);
					Thread.Sleep(300);
				} catch { }
			}
			try {
				if (multipleInstance == "unique") {
					HandleQuerystringFromAnotherInstance();
				}
				Console.WriteLine("Chromium process ID: " + Web2exe.chromiumProcess.Id);
				Web2exe.chromiumProcess.WaitForExit();
				// Delete the temporary Chromium profile folder
				if (stateless && Directory.Exists(profilePath)) {
					Directory.Delete(profilePath, true);
				}
			} catch (Exception e) {
				Console.WriteLine(e.ToString());
				MessageBox.Show("Google Chrome or Microsoft Edge has to be installed on this computer in order to use this tool.", "Error: Chrome and Edge not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
			} finally {
				ws.Stop(); // Stop the HTTP server
			}
		}
	}
}