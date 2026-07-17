using Fleck;
using QRCoder;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopStage
{
    public partial class MainWindow : Window
    {
        private WebSocketServer _webSocketServer;
        private HttpListener _httpListener;

        public MainWindow()
        {
            InitializeComponent();
            StartLocalServices();
        }

        private void StartLocalServices()
        {
            string localIp = GetLocalIPAddress();

            _webSocketServer = new WebSocketServer($"ws://{localIp}:8081");
            _webSocketServer.Start(socket =>
            {
                socket.OnOpen = () => Dispatcher.Invoke(() => StatusText.Text = "Phone Connected!");
                socket.OnClose = () => Dispatcher.Invoke(() => StatusText.Text = "Phone Disconnected. Scan again.");
                socket.OnMessage = message => Dispatcher.Invoke(() => HandlePhoneCommand(message));
            });

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:8080/");
            _httpListener.Start();
            Task.Run(() => ListenForHttpRequests());

            IpAddressText.Text = $"http://{localIp}:8080";
            StatusText.Text = "Waiting for phone connection...";

            Dispatcher.Invoke(() => {
                string url = $"http://{localIp}:8080";
                IpAddressText.Text = url;
                StatusText.Text = "Waiting for phone connection...";

                DisplayQrCode(url);
            });
        }

        private async Task ListenForHttpRequests()
        {
            while (_httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    var response = context.Response;

                    string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remote.html");
                    string htmlResponse = File.Exists(htmlPath) ? File.ReadAllText(htmlPath) : "<h1>HTML file missing!</h1>";

                    byte[] buffer = Encoding.UTF8.GetBytes(htmlResponse);
                    response.ContentLength64 = buffer.Length;

                    using (var output = response.OutputStream)
                    {
                        await output.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                catch (HttpListenerException)
                {
                    // catch expected exception when server stops
                }
            }
        }

        private void HandlePhoneCommand(string message)
        {
            CommandLogText.Text = $"Command received: {message}";

            if (message == "TOGGLE_SPOTLIGHT")
            {
                MessageBox.Show("Phase 1 Connection Successful!");
            }
        }

        private string GetLocalIPAddress()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
        }

        private void DisplayQrCode(string url)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeBytes = qrCode.GetGraphic(20);

                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream ms = new MemoryStream(qrCodeBytes))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze(); // required for cross-thread safety

                Dispatcher.Invoke(() => QrCodeImage.Source = bitmapImage);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _webSocketServer?.Dispose();
            _httpListener?.Stop();
            base.OnClosed(e);
        }
    }
}