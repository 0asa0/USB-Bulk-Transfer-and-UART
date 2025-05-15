using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CyUSB;
using System.IO.Ports; // UART için eklendi
using System.Diagnostics; // Stopwatch için eklendi
using System.Linq; // COM portlarını bulmak için eklendi

namespace usb_bulk_2
{
    public partial class MainForm : Form
    {
        // USB Değişkenleri
        private USBDeviceList usbDevices;
        private CyUSBDevice myDevice;
        private CyBulkEndPoint outEndpoint;
        private CyBulkEndPoint inEndpoint;
        private Timer deviceCheckTimer;
        private Timer usbEchoTimer;
        private bool isUsbEchoRunning = false;

        private double lastUsbSendTime = 0;
        private double lastUsbResponseTime = 0;
        private int usbEchoPacketCount = 0;
        private double totalUsbSendTime = 0;
        private double totalUsbResponseTime = 0;
        private double minUsbSendTime = double.MaxValue;
        private double maxUsbSendTime = 0;
        private double minUsbResponseTime = double.MaxValue;
        private double maxUsbResponseTime = 0;

        private const int USB_VID = 0x04B4;
        private const int USB_PID = 0xF001;

        // UART Değişkenleri
        private SerialPort uartPort;
        private Timer uartEchoTimer;
        private bool isUartEchoRunning = false;
        private string selectedComPort = null; // Seçilen veya bulunan COM portu
        private const int UART_BAUD_RATE = 115200; // Varsayılan baud rate

        private double lastUartSendTime = 0;
        private double lastUartResponseTime = 0;
        private int uartEchoPacketCount = 0;
        private double totalUartSendTime = 0;
        private double totalUartResponseTime = 0;
        private double minUartSendTime = double.MaxValue;
        private double maxUartSendTime = 0;
        private double minUartResponseTime = double.MaxValue;
        private double maxUartResponseTime = 0;
        private StringBuilder uartReceiveBuffer = new StringBuilder();

        // Log renkleri
        private readonly Color usbEchoLogColor = Color.DarkCyan;
        private readonly Color uartEchoLogColor = Color.DarkMagenta;
        private readonly Color errorLogColor = Color.OrangeRed;


        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            SetupControls();
            SetupUsbMonitoring();
            SetupUsbEchoTimer();
            SetupUart(); // UART kurulumu
            SetupUartEchoTimer(); // UART echo timer kurulumu
        }

        private void SetupControls()
        {
            cmbCommands.Items.Add(new CommandItem("Read", UsbPacket.CMD_READ));
            cmbCommands.Items.Add(new CommandItem("Write", UsbPacket.CMD_WRITE));
            cmbCommands.Items.Add(new CommandItem("Status", UsbPacket.CMD_STATUS));
            cmbCommands.Items.Add(new CommandItem("Reset", UsbPacket.CMD_RESET));
            cmbCommands.Items.Add(new CommandItem("Version", UsbPacket.CMD_VERSION));
            cmbCommands.Items.Add(new CommandItem("USB String Echo", UsbPacket.CMD_ECHO_STRING));
            cmbCommands.Items.Add(new CommandItem("UART String Echo", UsbPacket.CMD_UART_ECHO_STRING)); // UART Echo eklendi
            cmbCommands.SelectedIndex = 0;

            btnSend.Click += BtnSend_Click;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ReadOnly = true;

            cmbCommands.SelectedIndexChanged += CmbCommands_SelectedIndexChanged;
        }

        private void CmbCommands_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            var selected = cmbCommands.SelectedItem as CommandItem;
            if (selected != null)
            {
                if (selected.CommandId == UsbPacket.CMD_ECHO_STRING) // USB Echo
                {
                    btnSend.Text = isUsbEchoRunning ? "Stop USB Echo" : "Start USB Echo";
                }
                else if (selected.CommandId == UsbPacket.CMD_UART_ECHO_STRING) // UART Echo
                {
                    btnSend.Text = isUartEchoRunning ? "Stop UART Echo" : "Start UART Echo";
                }
                else
                {
                    btnSend.Text = "Send";
                }
            }
            else
            {
                btnSend.Text = "Send";
            }
        }

        #region USB İletişimi
        private void SetupUsbMonitoring()
        {
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            usbDevices.DeviceAttached += USBDeviceAttached;
            usbDevices.DeviceRemoved += USBDeviceRemoved;

            deviceCheckTimer = new Timer { Interval = 2000 };
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick;
            deviceCheckTimer.Start();

            FindDevice();
        }

        private void SetupUsbEchoTimer()
        {
            usbEchoTimer = new Timer { Interval = 100 };
            usbEchoTimer.Tick += UsbEchoTimer_Tick;
        }

        private void UsbEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUsbEchoRunning) return;
            SendUsbEchoPacket();
        }

        private void SendUsbEchoPacket()
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null) return;

            string input = txtData.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                input = "USB Echo Test";
            }

            var packet = new UsbPacket { CommandId = UsbPacket.CMD_ECHO_STRING };
            packet.SetDataFromText(input);

            var resp = SendUsbPacket(packet); // SendUsbPacket handles its own colored logging for echo
            if (resp != null)
            {
                usbEchoPacketCount++;
                totalUsbSendTime += lastUsbSendTime;
                totalUsbResponseTime += lastUsbResponseTime;

                minUsbSendTime = Math.Min(minUsbSendTime, lastUsbSendTime);
                maxUsbSendTime = Math.Max(maxUsbSendTime, lastUsbSendTime);
                minUsbResponseTime = Math.Min(minUsbResponseTime, lastUsbResponseTime);
                maxUsbResponseTime = Math.Max(maxUsbResponseTime, lastUsbResponseTime);

                double avgSendTime = totalUsbSendTime / usbEchoPacketCount;
                double avgResponseTime = totalUsbResponseTime / usbEchoPacketCount;

                if (isUsbEchoRunning)
                {
                    UpdateStatus(
                        $"USB Echo: #{usbEchoPacketCount} | Avg: {avgSendTime:F1}/{avgResponseTime:F1} ms | Min: {minUsbSendTime:F1}/{minUsbResponseTime:F1} ms | Max: {maxUsbSendTime:F1}/{maxUsbResponseTime:F1} ms",
                        usbEchoLogColor); // Status bar color
                }


                if (usbEchoPacketCount % 50 == 0)
                {
                    LogMessage($"USB Echo Stats (#{usbEchoPacketCount}):", usbEchoLogColor);
                    LogMessage($"  Send - Avg: {avgSendTime:F3} ms, Min: {minUsbSendTime:F3} ms, Max: {maxUsbSendTime:F3} ms", usbEchoLogColor);
                    LogMessage($"  Recv - Avg: {avgResponseTime:F3} ms, Min: {minUsbResponseTime:F3} ms, Max: {maxUsbResponseTime:F3} ms", usbEchoLogColor);
                }
            }
            else
            {
                StopUsbEchoMode();
                UpdateStatus("USB Echo communication error! Stopped.", Color.Red);
            }
        }

        private void StartUsbEchoMode()
        {
            if (isUsbEchoRunning) return;
            if (myDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("USB device not connected for Echo!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isUsbEchoRunning = true;
            usbEchoPacketCount = 0;
            totalUsbSendTime = 0;
            totalUsbResponseTime = 0;
            minUsbSendTime = double.MaxValue;
            maxUsbSendTime = 0;
            minUsbResponseTime = double.MaxValue;
            maxUsbResponseTime = 0;

            var currentSelection = cmbCommands.SelectedItem as CommandItem;
            if (currentSelection != null && currentSelection.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                UpdateButtonText();
            }
            LogMessage("----- USB Echo mode started -----", usbEchoLogColor);
            string initialData = txtData.Text.Trim();
            if (string.IsNullOrEmpty(initialData)) initialData = "USB Echo Test";
            LogMessage($"USB Echo string: \"{initialData}\"", usbEchoLogColor);
            usbEchoTimer.Start();
        }

        private void StopUsbEchoMode()
        {
            if (!isUsbEchoRunning) return;

            usbEchoTimer.Stop();
            isUsbEchoRunning = false;

            var currentSelection = cmbCommands.SelectedItem as CommandItem;
            if (currentSelection != null && currentSelection.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                UpdateButtonText();
            }


            if (usbEchoPacketCount > 0)
            {
                double avgSendTime = totalUsbSendTime / usbEchoPacketCount;
                double avgResponseTime = totalUsbResponseTime / usbEchoPacketCount;

                LogMessage("----- USB Echo mode stopped -----", usbEchoLogColor);
                LogMessage($"Total USB packets: {usbEchoPacketCount}", usbEchoLogColor);
                LogMessage($"  Send - Avg: {avgSendTime:F3} ms, Min: {minUsbSendTime:F3} ms, Max: {maxUsbSendTime:F3} ms", usbEchoLogColor);
                LogMessage($"  Recv - Avg: {avgResponseTime:F3} ms, Min: {minUsbResponseTime:F3} ms, Max: {maxUsbResponseTime:F3} ms", usbEchoLogColor);
                LogMessage("------------------------------", usbEchoLogColor);
            }
            else
            {
                LogMessage("----- USB Echo mode stopped (no packets sent) -----", usbEchoLogColor);
            }
        }

        private void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            if (myDevice == null)
                FindDevice();
        }

        private void FindDevice()
        {
            try
            {
                usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);

                foreach (CyUSBDevice dev in usbDevices)
                {
                    if (dev.VendorID == USB_VID && dev.ProductID == USB_PID)
                    {
                        myDevice = dev;
                        outEndpoint = null;
                        inEndpoint = null;
                        foreach (CyUSBEndPoint ept in myDevice.EndPoints)
                        {
                            if (ept.Attributes == 2)
                            {
                                if (ept.bIn)
                                    inEndpoint = (CyBulkEndPoint)ept;
                                else
                                    outEndpoint = (CyBulkEndPoint)ept;
                            }
                        }

                        if (outEndpoint != null && inEndpoint != null)
                        {
                            UpdateStatus("USB Device connected!", Color.Green);
                            LogMessage($"USB Device found: {dev.FriendlyName}");
                            outEndpoint.TimeOut = 1000;
                            inEndpoint.TimeOut = 1000;
                            SendVersionQuery();
                            return;
                        }
                        myDevice = null;
                    }
                }
                UpdateStatus("USB Device not found!", Color.Red);
            }
            catch (Exception ex)
            {
                LogMessage($"Error finding USB device: {ex.Message}", errorLogColor);
                UpdateStatus("Error finding USB device!", Color.Red);
            }
        }

        private void SendVersionQuery()
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null) return;
            try
            {
                var packet = new UsbPacket { CommandId = UsbPacket.CMD_VERSION, DataLength = 0 };
                var response = SendUsbPacket(packet); // Will use default log color
                if (response != null)
                {
                    LogMessage("Version query sent (USB)");
                    LogMessage(response.ParseContent());
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Version query error (USB): {ex.Message}", errorLogColor);
            }
        }

        private UsbPacket SendUsbPacket(UsbPacket packet)
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null)
            {
                LogMessage("USB Send Error: Device not ready.", errorLogColor);
                return null;
            }

            byte[] outData = packet.ToByteArray();
            byte[] inData = new byte[inEndpoint.MaxPktSize];
            int outLen = outData.Length;
            int inLen = inData.Length;
            UsbPacket response = null;

            try
            {
                var swSend = Stopwatch.StartNew();
                bool okS = outEndpoint.XferData(ref outData, ref outLen);
                swSend.Stop();
                lastUsbSendTime = swSend.Elapsed.TotalMilliseconds;

                if (!okS)
                {
                    LogMessage("USB Error: Data send failed. Code: " + outEndpoint.LastError, errorLogColor);
                    return null;
                }

                double sendMbps = (lastUsbSendTime > 0) ? (outLen * 8.0 / 1_000_000.0) / (lastUsbSendTime / 1000.0) : 0;
                if (!isUsbEchoRunning || usbEchoPacketCount % 50 == 0 || usbEchoPacketCount == 1)
                {
                    LogMessage($"USB Sent: {outLen} bytes -> {lastUsbSendTime:F3} ms -> {sendMbps:F2} Mbps", isUsbEchoRunning ? (Color?)usbEchoLogColor : null);
                }

                var swR = Stopwatch.StartNew();
                bool okR = inEndpoint.XferData(ref inData, ref inLen);
                swR.Stop();
                lastUsbResponseTime = swR.Elapsed.TotalMilliseconds;

                if (!okR)
                {
                    LogMessage("USB Error: Response receive failed. Code: " + inEndpoint.LastError, errorLogColor);
                    return null;
                }
                if (inLen == 0 && packet.CommandId == UsbPacket.CMD_ECHO_STRING)
                {
                    LogMessage("USB Warning: Received 0 bytes for an Echo command.", isUsbEchoRunning ? (Color?)usbEchoLogColor : (Color?)Color.Orange);
                }


                double recvMbps = (lastUsbResponseTime > 0 && inLen > 0) ? (inLen * 8.0 / 1_000_000.0) / (lastUsbResponseTime / 1000.0) : 0;
                if (!isUsbEchoRunning || usbEchoPacketCount % 50 == 0 || usbEchoPacketCount == 1)
                {
                    LogMessage($"USB Received: {inLen} bytes -> {lastUsbResponseTime:F3} ms -> {recvMbps:F2} Mbps", isUsbEchoRunning ? (Color?)usbEchoLogColor : null);
                }

                byte[] actualInData = new byte[inLen];
                Array.Copy(inData, actualInData, inLen);
                response = UsbPacket.FromByteArray(actualInData);
            }
            catch (Exception ex)
            {
                LogMessage($"USB communication error: {ex.Message}", errorLogColor);
                if (ex.InnerException != null) LogMessage($"Inner Exception: {ex.InnerException.Message}", errorLogColor);
            }

            return response;
        }

        private void USBDeviceAttached(object sender, EventArgs e) => FindDevice();
        private void USBDeviceRemoved(object sender, EventArgs e)
        {
            bool deviceReallyRemoved = true;
            if (myDevice != null)
            {
                USBDeviceList currentDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
                deviceReallyRemoved = !currentDevices.Cast<CyUSBDevice>().Any(d => d.VendorID == myDevice.VendorID && d.ProductID == myDevice.ProductID && d.SerialNumber == myDevice.SerialNumber);
            }

            if (myDevice != null && deviceReallyRemoved)
            {
                myDevice = null;
                outEndpoint = null;
                inEndpoint = null;

                if (isUsbEchoRunning)
                {
                    StopUsbEchoMode();
                }

                UpdateStatus("USB Device removed!", Color.Red);
                LogMessage("USB Device removed");
            }
        }
        #endregion

        #region UART İletişimi
        private void SetupUart()
        {
            string[] portNames = SerialPort.GetPortNames();
            if (portNames.Length > 0)
            {
                selectedComPort = "COM9";

                if (!portNames.Contains(selectedComPort))
                {
                    LogMessage($"UART: Specified COM port {selectedComPort} not found. Available: {string.Join(", ", portNames)}", Color.Orange);
                    if (portNames.Length > 0) selectedComPort = portNames[0];
                    else
                    {
                        LogMessage("UART: No COM ports available to fall back to.", Color.Orange);
                        UpdateStatus("UART: No COM ports available.", Color.Orange);
                        return;
                    }
                    LogMessage($"UART: Falling back to {selectedComPort}.");
                }

                uartPort = new SerialPort(selectedComPort, UART_BAUD_RATE, Parity.None, 8, StopBits.One);
                uartPort.ReadTimeout = 200;
                uartPort.WriteTimeout = 200;
                LogMessage($"UART: Using {selectedComPort} at {UART_BAUD_RATE} baud.");
            }
            else
            {
                LogMessage("UART: No COM ports found.", Color.Orange);
                UpdateStatus("UART: No COM ports available.", Color.Orange);
            }
        }


        private void SetupUartEchoTimer()
        {
            uartEchoTimer = new Timer { Interval = 100 };
            uartEchoTimer.Tick += UartEchoTimer_Tick;
        }

        private void UartEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUartEchoRunning) return;
            SendUartEchoPacket();
        }

        private void SendUartEchoPacket()
        {
            if (uartPort == null || !uartPort.IsOpen)
            {
                if (isUartEchoRunning)
                {
                    LogMessage("UART Echo Error: Port not open. Stopping UART echo.", errorLogColor);
                    StopUartEchoMode();
                }
                return;
            }

            string textToSend = txtData.Text.Trim();
            if (string.IsNullOrEmpty(textToSend))
            {
                textToSend = "UART Echo Test";
            }

            try
            {
                Stopwatch swSend = Stopwatch.StartNew();
                uartPort.Write(textToSend);
                swSend.Stop();
                lastUartSendTime = swSend.Elapsed.TotalMilliseconds;
                double uartSentBytes = Encoding.ASCII.GetByteCount(textToSend);
                double uartSendMbps = (lastUartSendTime > 0) ? (uartSentBytes * 8.0 / 1_000_000.0) / (lastUartSendTime / 1000.0) : 0;

                if (uartEchoPacketCount % 50 == 0 || uartEchoPacketCount == 0) // İlk paket ve periyodik
                {
                    LogMessage($"UART Sent: {uartSentBytes} bytes -> {lastUartSendTime:F3} ms -> {uartSendMbps:F2} Mbps", uartEchoLogColor);
                }


                byte[] buffer = new byte[Encoding.ASCII.GetByteCount(textToSend)];
                int bytesRead = 0;
                Stopwatch swRecv = Stopwatch.StartNew();
                try
                {
                    int totalBytesRead = 0;
                    uartPort.ReadTimeout = 100;

                    while (totalBytesRead < buffer.Length && swRecv.ElapsedMilliseconds < 200)
                    {
                        if (uartPort.BytesToRead > 0)
                        {
                            int currentRead = uartPort.Read(buffer, totalBytesRead, Math.Min(uartPort.BytesToRead, buffer.Length - totalBytesRead));
                            totalBytesRead += currentRead;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(1);
                        }
                    }
                    bytesRead = totalBytesRead;
                }
                catch (TimeoutException)
                {
                    // Timeout logu aşağıda genel hata/uyuşmazlık kısmında ele alınabilir.
                }
                swRecv.Stop();
                lastUartResponseTime = swRecv.Elapsed.TotalMilliseconds;
                double uartReceivedBytes = bytesRead;
                double uartRecvMbps = (lastUartResponseTime > 0 && uartReceivedBytes > 0) ? (uartReceivedBytes * 8.0 / 1_000_000.0) / (lastUartResponseTime / 1000.0) : 0;


                string receivedText = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                if (bytesRead > 0 && textToSend.Equals(receivedText))
                {
                    uartEchoPacketCount++;
                    totalUartSendTime += lastUartSendTime;
                    totalUartResponseTime += lastUartResponseTime;

                    minUartSendTime = Math.Min(minUartSendTime, lastUartSendTime);
                    maxUartSendTime = Math.Max(maxUartSendTime, lastUartSendTime);
                    minUartResponseTime = Math.Min(minUartResponseTime, lastUartResponseTime);
                    maxUartResponseTime = Math.Max(maxUartResponseTime, lastUartResponseTime);

                    double avgSendTime = totalUartSendTime / uartEchoPacketCount;
                    double avgResponseTime = totalUartResponseTime / uartEchoPacketCount;

                    if (isUartEchoRunning)
                    {
                        UpdateStatus(
                         $"UART Echo: #{uartEchoPacketCount} | Avg: {avgSendTime:F1}/{avgResponseTime:F1} ms | Min: {minUartSendTime:F1}/{minUartResponseTime:F1} ms | Max: {maxUartSendTime:F1}/{maxUartResponseTime:F1} ms",
                         uartEchoLogColor); // Status bar color
                    }

                    if (uartEchoPacketCount % 50 == 0 || uartEchoPacketCount == 1) // İlk paket ve periyodik (başarılı alım için)
                    {
                        LogMessage($"UART Received: {uartReceivedBytes} bytes -> {lastUartResponseTime:F3} ms -> {uartRecvMbps:F2} Mbps (Match)", uartEchoLogColor);
                    }

                    if (uartEchoPacketCount % 50 == 0 && uartEchoPacketCount > 1) // Periyodik istatistik (ilk paket hariç)
                    {
                        LogMessage($"UART Echo Stats (#{uartEchoPacketCount}):", uartEchoLogColor);
                        LogMessage($"  Send - Avg: {avgSendTime:F3} ms, Min: {minUartSendTime:F3} ms, Max: {maxUartSendTime:F3} ms", uartEchoLogColor);
                        LogMessage($"  Recv - Avg: {avgResponseTime:F3} ms, Min: {minUartResponseTime:F3} ms, Max: {maxUartResponseTime:F3} ms", uartEchoLogColor);
                    }
                }
                else
                {
                    if (uartEchoPacketCount % 20 == 0 || uartEchoPacketCount == 0 || bytesRead < uartSentBytes) // Hata/uyuşmazlık durumunu daha sık logla
                    {
                        string errorMsg = $"UART Echo Mismatch/Timeout: Sent='{textToSend}' ({uartSentBytes}B), Recv='{receivedText}' ({uartReceivedBytes}B).";
                        errorMsg += $" Times: Send={lastUartSendTime:F1}ms, Recv={lastUartResponseTime:F1}ms.";
                        if (uartReceivedBytes > 0) errorMsg += $" Recv Speed: {uartRecvMbps:F2} Mbps.";
                        LogMessage(errorMsg, errorLogColor);
                    }
                }
            }
            catch (InvalidOperationException ioe)
            {
                LogMessage($"UART Echo Error (Invalid Operation): {ioe.Message}. Stopping UART echo.", errorLogColor);
                StopUartEchoMode();
            }
            catch (Exception ex)
            {
                LogMessage($"UART Echo Error: {ex.Message}. Stopping UART echo.", errorLogColor);
                StopUartEchoMode();
            }
        }


        private void StartUartEchoMode()
        {
            if (isUartEchoRunning) return;

            if (uartPort == null)
            {
                MessageBox.Show("No COM port configured for UART Echo!", "UART Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetupUart();
                if (uartPort == null) return;
            }

            try
            {
                if (!uartPort.IsOpen)
                {
                    uartPort.Open();
                    LogMessage($"UART Port {uartPort.PortName} opened for echo.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open COM port {uartPort.PortName}: {ex.Message}", "UART Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"UART Error: Could not open port {uartPort.PortName} - {ex.Message}", errorLogColor);
                return;
            }

            isUartEchoRunning = true;
            uartEchoPacketCount = 0; // Sıfırlama burada yapılmalı
            totalUartSendTime = 0;
            totalUartResponseTime = 0;
            minUartSendTime = double.MaxValue;
            maxUartSendTime = 0;
            minUartResponseTime = double.MaxValue;
            maxUartResponseTime = 0;
            uartReceiveBuffer.Clear();

            var currentSelection = cmbCommands.SelectedItem as CommandItem;
            if (currentSelection != null && currentSelection.CommandId == UsbPacket.CMD_UART_ECHO_STRING)
            {
                UpdateButtonText();
            }

            LogMessage("----- UART Echo mode started -----", uartEchoLogColor);
            string initialData = txtData.Text.Trim();
            if (string.IsNullOrEmpty(initialData)) initialData = "UART Echo Test";
            LogMessage($"UART Echo string: \"{initialData}\" on {uartPort.PortName}", uartEchoLogColor);
            uartEchoTimer.Start();
        }

        private void StopUartEchoMode()
        {
            if (!isUartEchoRunning) return;

            uartEchoTimer.Stop();
            isUartEchoRunning = false;

            var currentSelection = cmbCommands.SelectedItem as CommandItem;
            if (currentSelection != null && currentSelection.CommandId == UsbPacket.CMD_UART_ECHO_STRING)
            {
                UpdateButtonText();
            }

            if (uartEchoPacketCount > 0)
            {
                double avgSendTime = totalUartSendTime / uartEchoPacketCount;
                double avgResponseTime = totalUartResponseTime / uartEchoPacketCount;

                LogMessage("----- UART Echo mode stopped -----", uartEchoLogColor);
                LogMessage($"Total UART packets: {uartEchoPacketCount}", uartEchoLogColor);
                LogMessage($"  Send - Avg: {avgSendTime:F3} ms, Min: {minUartSendTime:F3} ms, Max: {maxUartSendTime:F3} ms", uartEchoLogColor);
                LogMessage($"  Recv - Avg: {avgResponseTime:F3} ms, Min: {minUartResponseTime:F3} ms, Max: {maxUartResponseTime:F3} ms", uartEchoLogColor);
                LogMessage("------------------------------", uartEchoLogColor);
            }
            else
            {
                LogMessage("----- UART Echo mode stopped (no packets sent) -----", uartEchoLogColor);
            }
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Application started");
            LogMessage("Searching for USB device...");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopUsbEchoMode();
            StopUartEchoMode();

            deviceCheckTimer?.Stop();
            deviceCheckTimer?.Dispose();
            usbEchoTimer?.Dispose();
            uartEchoTimer?.Dispose();

            usbDevices?.Dispose();

            if (uartPort != null)
            {
                if (uartPort.IsOpen)
                {
                    uartPort.Close();
                }
                uartPort.Dispose();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            var selected = cmbCommands.SelectedItem as CommandItem;
            if (selected == null) return;

            if (selected.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                if (isUsbEchoRunning) StopUsbEchoMode();
                else StartUsbEchoMode();
            }
            else if (selected.CommandId == UsbPacket.CMD_UART_ECHO_STRING)
            {
                if (isUartEchoRunning) StopUartEchoMode();
                else StartUartEchoMode();
            }
            else
            {
                if (isUsbEchoRunning) StopUsbEchoMode();
                if (isUartEchoRunning) StopUartEchoMode();

                if (myDevice == null || outEndpoint == null || inEndpoint == null)
                {
                    MessageBox.Show("USB device not connected for this command!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var packet = new UsbPacket { CommandId = selected.CommandId };
                string input = txtData.Text.Trim();

                if (selected.CommandId == UsbPacket.CMD_WRITE)
                {
                    if (!TryParseHex(input, out byte val)) return;
                    packet.Data[0] = val;
                    packet.DataLength = 1;
                }

                LogMessage($"----- USB Packet Sending ({selected.Name}) -----");
                var resp = SendUsbPacket(packet); // Uses default log color here
                if (resp != null)
                {
                    LogMessage(resp.ParseContent());
                    LogMessage($"USB Timings: Send={lastUsbSendTime:F3} ms, Response={lastUsbResponseTime:F3} ms");
                    LogMessage($"------------------------------------------------------------------------");
                    UpdateStatus($"USB Send: {lastUsbSendTime:F1} ms | USB Response: {lastUsbResponseTime:F1} ms", Color.Blue);
                }
                else
                {
                    UpdateStatus("USB Communication error!", Color.Red);
                }
            }
        }

        private bool TryParseHex(string input, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Hex input cannot be empty for Write command.", "Input Error",
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                input = input.Substring(2);
            if (byte.TryParse(input, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;
            MessageBox.Show("Invalid hex value! Example: 0xA5 or A5", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private void LogMessage(string message, Color? textColor = null)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() =>
                {
                    Color defaultLogColor = txtLog.ForeColor;
                    if (textColor.HasValue)
                    {
                        txtLog.SelectionColor = textColor.Value;
                    }
                    else
                    {
                        txtLog.SelectionColor = defaultLogColor;
                    }
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                    txtLog.SelectionColor = defaultLogColor; // Reset to default for the next log
                    txtLog.ScrollToCaret();
                }));
            }
            else
            {
                Color defaultLogColor = txtLog.ForeColor;
                if (textColor.HasValue)
                {
                    txtLog.SelectionColor = textColor.Value;
                }
                else
                {
                    txtLog.SelectionColor = defaultLogColor;
                }
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                txtLog.SelectionColor = defaultLogColor; // Reset to default for the next log
                txtLog.ScrollToCaret();
            }
        }


        private void UpdateStatus(string message, Color color)
        {
            if (statusStrip1.InvokeRequired)
                statusStrip1.Invoke(new Action<string, Color>(UpdateStatus), message, color);
            else
            {
                toolStripStatusLabel1.Text = message;
                toolStripStatusLabel1.ForeColor = color;
            }
        }
    }

    public class CommandItem
    {
        public string Name { get; set; }
        public byte CommandId { get; set; }
        public CommandItem(string name, byte commandId) { Name = name; CommandId = commandId; }
        public override string ToString() => Name;
    }
}