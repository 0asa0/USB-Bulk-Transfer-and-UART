// MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CyUSB;
using System.IO.Ports;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace usb_bulk_2
{
    public partial class MainForm : Form
    {
        private USBDeviceList usbDevices;

        // Ayrı cihazlar veya arayüzler için CyUSBDevice nesneleri
        private CyUSBDevice customBulkDevice; // "asa usb bulk" için
        private CyUSBDevice canDevice;        // "asa usb CAN" için

        // Custom Bulk için Endpointler
        private CyBulkEndPoint customOutEndpoint; // EP1 (0x01)
        private CyBulkEndPoint customInEndpoint;  // EP2 (0x82)

        // CAN için Endpointler
        private CyBulkEndPoint canOutEndpoint;    // EP7 (0x07)
        private CyBulkEndPoint canInEndpoint;     // EP6 (0x86)

        // SendUsbPacket'in kullandığı genel endpointler (customBulkDevice'a ait olacak)
        private CyBulkEndPoint outEndpoint;
        private CyBulkEndPoint inEndpoint;

        private System.Windows.Forms.Timer deviceCheckTimer;

        // ... (Diğer değişken tanımlamaları aynı kalabilir: USB Echo, UART Echo, CanHandler, Log Colors vb.) ...
        private System.Windows.Forms.Timer usbEchoTimer;
        private bool isUsbEchoRunning = false;
        private double lastUsbSendTime, lastUsbResponseTime;
        private int usbEchoPacketCount;
        private double totalUsbSendTime, totalUsbResponseTime;
        private double minUsbSendTime = double.MaxValue, maxUsbSendTime = 0;
        private double minUsbResponseTime = double.MaxValue, maxUsbResponseTime = 0;

        private const int USB_VID = 0x04B4;
        private const int USB_PID = 0xF001;

        private const string CUSTOM_BULK_FRIENDLY_NAME_PART = "asa usb bulk"; // PSoC'taki isme göre ayarlayın
        private const string CAN_FRIENDLY_NAME_PART = "asa usb CAN";


        // UART Echo
        private SerialPort uartPort;
        private System.Windows.Forms.Timer uartEchoTimer;
        private bool isUartEchoRunning = false;
        private string selectedComPort = "COM9";
        private const int UART_BAUD_RATE = 115200;
        private double lastUartSendTime, lastUartResponseTime;
        private int uartEchoPacketCount;
        private double totalUartSendTime, totalUartResponseTime;
        private double minUartSendTime = double.MaxValue, maxUartSendTime = 0;
        private double minUartResponseTime = double.MaxValue, maxUartResponseTime = 0;

        // CAN Interface
        private CanHandler _canHandler;
        private bool isCanUiPaused = false;

        // Log Colors
        private readonly Color usbEchoLogColor = Color.DarkCyan;
        private readonly Color uartEchoLogColor = Color.DarkMagenta;
        private readonly Color canRxLogColor = Color.DarkGreen;
        private readonly Color canTxLogColor = Color.DarkBlue;
        private readonly Color errorLogColor = Color.OrangeRed;
        private readonly Color statusOkColor = Color.Green;
        private readonly Color statusWarnColor = Color.DarkOrange;
        private readonly Color statusErrorColor = Color.Red;


        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;

            SetupCustomControls();
            SetupCanInterfaceLogic();
            SetupUsbMonitoring();
            SetupEchoTimers();
            SetupUartCommunication();
        }

        // ... (SetupCustomControls, ListViewCan_DoubleClick, SetupEchoTimers, UpdateButtonText, SetupCanInterfaceLogic aynı kalabilir) ...
        // Sadece SetupCustomControls içindeki örnek CommandItem'ların UsbPacket.cs dosyanızda tanımlı olduğundan emin olun.
        private void SetupCustomControls()
        {
            if (typeof(MainForm).Assembly.GetType("usb_bulk_2.UsbPacket") != null)
            {
                cmbCommands.Items.Add(new CommandItem("Read", UsbPacket.CMD_READ));
                cmbCommands.Items.Add(new CommandItem("Write", UsbPacket.CMD_WRITE));
                cmbCommands.Items.Add(new CommandItem("Status", UsbPacket.CMD_STATUS));
                cmbCommands.Items.Add(new CommandItem("Reset", UsbPacket.CMD_RESET));
                cmbCommands.Items.Add(new CommandItem("Version", UsbPacket.CMD_VERSION));
                cmbCommands.Items.Add(new CommandItem("USB String Echo", UsbPacket.CMD_ECHO_STRING));
                cmbCommands.Items.Add(new CommandItem("UART String Echo", UsbPacket.CMD_UART_ECHO_STRING));
                if (cmbCommands.Items.Count > 0) cmbCommands.SelectedIndex = 0;
            }
            else
            {
                LogMessage("UsbPacket sınıfı bulunamadı. USB Kontrol sekmesi komutları çalışmayabilir.", errorLogColor);
            }

            btnSend.Click += BtnSendCustomCommand_Click;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ReadOnly = true;
            cmbCommands.SelectedIndexChanged += CmbCommands_SelectedIndexChanged;

            listViewCanReceive.Font = new Font("Consolas", 8.5F);
            listViewCanTransmit.Font = new Font("Consolas", 8.5F);
            listViewCanReceive.DoubleClick += ListViewCan_DoubleClick;
            listViewCanTransmit.DoubleClick += ListViewCan_DoubleClick;

            ContextMenuStrip cmsCanList = new ContextMenuStrip();
            cmsCanList.Items.Add("Clear List", null, (s, ev) => {
                if (cmsCanList.SourceControl == listViewCanReceive) listViewCanReceive.Items.Clear();
                else if (cmsCanList.SourceControl == listViewCanTransmit) listViewCanTransmit.Items.Clear();
            });
            cmsCanList.Items.Add(new ToolStripSeparator());
            var pauseUpdatesItem = cmsCanList.Items.Add("Pause Updates", null, (s, ev) => {
                isCanUiPaused = !isCanUiPaused;
                ((ToolStripMenuItem)s).Checked = isCanUiPaused;
            });
            ((ToolStripMenuItem)pauseUpdatesItem).CheckOnClick = true;

            listViewCanReceive.ContextMenuStrip = cmsCanList;
            listViewCanTransmit.ContextMenuStrip = cmsCanList;
        }
        private void ListViewCan_DoubleClick(object sender, EventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv != null && lv.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = lv.SelectedItems[0];
                if (selectedItem.Tag is CanMessage canMsg)
                {
                    string details = $"Seq: {canMsg.SequenceNumber}\n" +
                                    $"Dir: {canMsg.Direction}\n" +
                                    $"UI Time: {canMsg.UiTimestamp:HH:mm:ss.fff}\n" +
                                    $"PSoC TS: {canMsg.PSoCTimestamp}\n" +
                                    $"ID: {canMsg.IdToHexString()} (0x{canMsg.Id:X})\n" +
                                    $"DLC: {canMsg.Length}\n" +
                                    $"Data: {canMsg.DataToHexString()}\n" +
                                    $"Props: 0x{canMsg.Properties:X2}";
                    MessageBox.Show(details, $"CAN Message Details ({canMsg.Direction})", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    string details = $"Seq: {selectedItem.SubItems[0].Text}\n" +
                                    $"Time: {selectedItem.SubItems[1].Text}\n" +
                                    $"ID: {selectedItem.SubItems[2].Text}\n" +
                                    $"DLC: {selectedItem.SubItems[3].Text}\n" +
                                    $"Data: {selectedItem.SubItems[4].Text}\n" +
                                    $"PSoC TS: {selectedItem.SubItems[5].Text}\n" +
                                    $"Props: {selectedItem.SubItems[6].Text}";
                    MessageBox.Show(details, "CAN Message Details (Raw Text)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SetupEchoTimers()
        {
            usbEchoTimer = new System.Windows.Forms.Timer { Interval = 100 };
            usbEchoTimer.Tick += UsbEchoTimer_Tick;

            uartEchoTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uartEchoTimer.Tick += UartEchoTimer_Tick;
        }

        private void CmbCommands_SelectedIndexChanged(object sender, EventArgs e) => UpdateButtonText();

        private void UpdateButtonText()
        {
            if (cmbCommands.SelectedItem is CommandItem selected)
            {
                if (selected.CommandId == UsbPacket.CMD_ECHO_STRING) btnSend.Text = isUsbEchoRunning ? "Stop USB Echo" : "Start USB Echo";
                else if (selected.CommandId == UsbPacket.CMD_UART_ECHO_STRING) btnSend.Text = isUartEchoRunning ? "Stop UART Echo" : "Start UART Echo";
                else btnSend.Text = "Send Command";
            }
            else btnSend.Text = "Send Command";
        }

        private void SetupCanInterfaceLogic()
        {
            _canHandler = new CanHandler();
            _canHandler.CanMessageReceived += HandleCanMessageFromCanHandler;
            _canHandler.LogMessageRequest += (msg, color) => LogMessage($"[CAN] {msg}", color);
            btnSendCanMessage.Click += BtnSendCanMessageViaHandler_Click;
        }


        #region USB Device Management (FriendlyName ile Güncellendi)
        private void SetupUsbMonitoring()
        {
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            usbDevices.DeviceAttached += USBDeviceAttached;
            usbDevices.DeviceRemoved += USBDeviceRemoved; // Bu event her iki "cihaz" için de tetiklenebilir
            deviceCheckTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick;
            deviceCheckTimer.Start();
            AttemptToFindAndConfigureDevice();
        }

        private void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            // Her iki cihazın da bağlı olup olmadığını kontrol et
            bool customBulkOk = customBulkDevice != null && customBulkDevice.DeviceHandle != IntPtr.Zero;
            bool canDevOk = canDevice != null && canDevice.DeviceHandle != IntPtr.Zero;

            if (!customBulkOk || !canDevOk)
            {
                LogMessage("Device Check: One or more USB functions (Bulk/CAN) not ready. Attempting to reconfigure...", Color.Gray);
                AttemptToFindAndConfigureDevice(); // Tam yeniden yapılandırma
            }
        }

        private void USBDeviceAttached(object sender, EventArgs e)
        {
            var devEventArgs = e as CyUSB.USBEventArgs;
            LogMessage($"USB Device Attached Event: FriendlyName='{devEventArgs?.FriendlyName}', Path='{devEventArgs?.Device?.Path}'", statusOkColor);
            Task.Delay(750).ContinueWith(_ =>
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(AttemptToFindAndConfigureDevice));
                }
            });
        }

        private void USBDeviceRemoved(object sender, EventArgs e)
        {
            var devEventArgs = e as CyUSB.USBEventArgs;
            string removedFriendlyName = devEventArgs?.FriendlyName ?? "Unknown";
            LogMessage($"USB Device Removed Event: FriendlyName='{removedFriendlyName}'", statusErrorColor);

            bool customBulkWasActive = customBulkDevice != null;
            bool canWasActive = canDevice != null;

            // Kaldırılan cihaza göre ilgili referansları ve durumu sıfırla
            if (customBulkDevice != null && (devEventArgs == null || customBulkDevice.Path == devEventArgs.Device?.Path || removedFriendlyName.IndexOf(CUSTOM_BULK_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (isUsbEchoRunning) StopUsbEchoMode();
                customBulkDevice = null; // myDevice.Dispose() burada değil!
                customOutEndpoint = null; customInEndpoint = null;
                outEndpoint = null; inEndpoint = null;
                LogMessage($"Custom Bulk Function '{removedFriendlyName}' disconnected.", statusErrorColor);
            }

            if (canDevice != null && (devEventArgs == null || canDevice.Path == devEventArgs.Device?.Path || removedFriendlyName.IndexOf(CAN_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _canHandler?.StopListening();
                _canHandler?.InitializeDevice(null, null, null);
                canDevice = null;
                canOutEndpoint = null; canInEndpoint = null;
                LogMessage($"CAN Function '{removedFriendlyName}' disconnected.", statusErrorColor);
            }

            // Genel durum güncellemesi
            if (customBulkDevice == null && canDevice == null && (customBulkWasActive || canWasActive))
            {
                UpdateStatus("All USB functions disconnected.", statusErrorColor);
            }
            else if (customBulkDevice == null && customBulkWasActive)
            {
                UpdateStatus("Custom Bulk USB function disconnected.", statusErrorColor);
            }
            else if (canDevice == null && canWasActive)
            {
                UpdateStatus("CAN USB function disconnected.", statusErrorColor);
            }
            // Gerekirse kalanları tekrar bulmaya çalışabiliriz, ama DeviceCheckTimer bunu yapacak.
        }


        private void AttemptToFindAndConfigureDevice()
        {
            // Önce mevcut referansları temizleyelim (eğer varsa ve bağlı değillerse)
            if (customBulkDevice != null && customBulkDevice.DeviceHandle == IntPtr.Zero) customBulkDevice = null;
            if (canDevice != null && canDevice.DeviceHandle == IntPtr.Zero) canDevice = null;

            if (customBulkDevice == null) { customOutEndpoint = null; customInEndpoint = null; outEndpoint = null; inEndpoint = null; }
            if (canDevice == null) { canOutEndpoint = null; canInEndpoint = null; _canHandler?.InitializeDevice(null, null, null); }


            bool customConfigured = false;
            bool canConfigured = false;

            try
            {
                usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); // Her zaman listeyi yenile

                // VID/PID'ye uyan tüm cihazları al
                List<CyUSBDevice> matchingDevices = usbDevices.Cast<CyUSBDevice>()
                                                    .Where(d => d.VendorID == USB_VID && d.ProductID == USB_PID)
                                                    .ToList();

                if (matchingDevices.Count == 0)
                {
                    UpdateStatus("USB Device (VID/PID match) not found.", statusErrorColor);
                    LogMessage("No PSoC devices found.", errorLogColor);
                    _canHandler?.StopListening(); // Emin olmak için
                    return;
                }

                LogMessage($"Found {matchingDevices.Count} device(s) with matching VID/PID.", Color.LightBlue);

                foreach (CyUSBDevice dev in matchingDevices)
                {
                    LogMessage($"Device: {dev.FriendlyName}", Color.Gray);
                    if (string.IsNullOrEmpty(dev.FriendlyName)) continue;

                    // CAN Arayüzünü/Cihazını Bul
                    if (canDevice == null && dev.FriendlyName.IndexOf(CAN_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        canDevice = dev;
                        LogMessage($"CAN Device Candidate: {canDevice.FriendlyName}", canRxLogColor);
                        canOutEndpoint = null; canInEndpoint = null; // Reset before trying to assign
                        // Bu cihazın endpointlerini tara
                        for (int i = 0; i < canDevice.EndPointCount; i++)
                        {
                            if (canDevice.EndPoints[i] is CyBulkEndPoint ep && ep.Attributes == 2)
                            {
                                if (ep.bIn && ep.Address == 0x86) canInEndpoint = ep;
                                else if (!ep.bIn && ep.Address == 0x07) canOutEndpoint = ep;
                            }
                        }
                        if (canInEndpoint != null && canOutEndpoint != null)
                        {
                            LogMessage($"CAN Endpoints (EP:{canOutEndpoint.Address:X2} OUT / EP:{canInEndpoint.Address:X2} IN) on '{canDevice.FriendlyName}' configured.", canRxLogColor);
                            _canHandler.InitializeDevice(canDevice, canInEndpoint, canOutEndpoint); // Sadece canDevice'ı ver
                            _canHandler.StartListening();
                            canConfigured = true;
                        }
                        else
                        {
                            LogMessage($"CAN device '{canDevice.FriendlyName}' found, but required Endpoints (0x07/0x86) missing or not Bulk.", errorLogColor);
                            canDevice = null; // Başarısız olduysa sıfırla
                        }
                    }
                    // Custom Bulk Arayüzünü/Cihazını Bul
                    else if (customBulkDevice == null && dev.FriendlyName.IndexOf(CUSTOM_BULK_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        customBulkDevice = dev;
                        LogMessage($"Custom Bulk Device Candidate: {customBulkDevice.FriendlyName}", Color.DarkSlateBlue);
                        customOutEndpoint = null; customInEndpoint = null; // Reset
                        for (int i = 0; i < customBulkDevice.EndPointCount; i++)
                        {
                            if (customBulkDevice.EndPoints[i] is CyBulkEndPoint ep && ep.Attributes == 2)
                            {
                                if (ep.bIn && ep.Address == 0x82) customInEndpoint = ep;
                                else if (!ep.bIn && ep.Address == 0x01) customOutEndpoint = ep;
                            }
                        }
                        if (customOutEndpoint != null && customInEndpoint != null)
                        {
                            outEndpoint = customOutEndpoint; // Genel olanları ata
                            inEndpoint = customInEndpoint;
                            outEndpoint.TimeOut = 1000;
                            inEndpoint.TimeOut = 1000;
                            LogMessage($"Custom Bulk Endpoints (EP:{customOutEndpoint.Address:X2} OUT / EP:{customInEndpoint.Address:X2} IN) on '{customBulkDevice.FriendlyName}' configured.", Color.DarkSlateBlue);
                            if (!isUsbEchoRunning && !isUartEchoRunning) SendVersionQueryOverCustomEP();
                            customConfigured = true;
                        }
                        else
                        {
                            LogMessage($"Custom Bulk device '{customBulkDevice.FriendlyName}' found, but required Endpoints (0x01/0x82) missing or not Bulk.", errorLogColor);
                            customBulkDevice = null; // Sıfırla
                        }
                    }
                } // End foreach dev

                // Genel Durum Güncellemesi
                if (customConfigured && canConfigured) UpdateStatus("All USB functions (Custom Bulk & CAN) connected.", statusOkColor);
                else if (customConfigured) UpdateStatus("Custom Bulk connected. CAN not found/configured.", statusWarnColor);
                else if (canConfigured) UpdateStatus("CAN connected. Custom Bulk not found/configured.", statusWarnColor);
                else
                {
                    UpdateStatus("Required USB functions not found/configured.", statusErrorColor);
                    LogMessage("Could not configure either Custom Bulk or CAN interfaces from found devices.", errorLogColor);
                }

            }
            catch (Exception ex)
            {
                LogMessage($"Error during USB device search/configuration: {ex.Message}", errorLogColor);
                UpdateStatus("Error configuring USB devices.", statusErrorColor);
                // Hata durumunda tüm referansları null yap
                customBulkDevice = null; canDevice = null;
                customOutEndpoint = null; customInEndpoint = null;
                canOutEndpoint = null; canInEndpoint = null;
                outEndpoint = null; inEndpoint = null;
                _canHandler.StopListening();
                _canHandler.InitializeDevice(null, null, null);
            }
        }
        #endregion

        #region USB Custom Packet Communication (LastError_Message düzeltildi)
        // ... (SendVersionQueryOverCustomEP aynı) ...
        // SendUsbPacket içindeki LastError_Message'ı düzelt
        private void SendVersionQueryOverCustomEP()
        {
            // customBulkDevice ve onun endpointlerini kullanmalı
            if (customBulkDevice == null || customOutEndpoint == null || customInEndpoint == null)
            {
                LogMessage("Cannot send version query: Custom Bulk USB device/endpoints not ready.", statusWarnColor);
                return;
            }
            try
            {
                var packet = new UsbPacket { CommandId = UsbPacket.CMD_VERSION, DataLength = 0 };
                // SendUsbPacket'e hangi endpointleri kullanacağını belirtmemiz gerekebilir,
                // ya da SendUsbPacket'in her zaman custom endpointleri kullandığını varsayalım.
                // Şu anki yapıda outEndpoint/inEndpoint custom'a ayarlı.
                var response = SendUsbPacket(packet);
                if (response != null)
                {
                    LogMessage($"Version query successful (via Custom EP). Response: {response.ParseContent()}");
                }
                else
                {
                    LogMessage("Version query (via Custom EP) failed or no response.", statusWarnColor);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Version query error (Custom EP): {ex.Message}", errorLogColor);
            }
        }

        private UsbPacket SendUsbPacket(UsbPacket packet)
        {
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
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
        private void BtnSendCustomCommand_Click(object sender, EventArgs e)
        {
            if (!(cmbCommands.SelectedItem is CommandItem selectedCmdItem)) return;

            if (selectedCmdItem.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                if (isUsbEchoRunning) StopUsbEchoMode(); else StartUsbEchoMode();
                return;
            }
            if (selectedCmdItem.CommandId == UsbPacket.CMD_UART_ECHO_STRING)
            {
                if (isUartEchoRunning) StopUartEchoMode(); else StartUartEchoMode();
                return;
            }

            // customBulkDevice ve onun endpointlerini (outEndpoint/inEndpoint üzerinden) kullan
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("Custom Bulk USB device not connected or endpoints not ready!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (isUsbEchoRunning) StopUsbEchoMode();
            if (isUartEchoRunning) StopUartEchoMode();

            var packetToSend = new UsbPacket { CommandId = selectedCmdItem.CommandId };
            string dataInput = txtData.Text.Trim();

            if (selectedCmdItem.CommandId == UsbPacket.CMD_WRITE)
            {
                if (!TryParseHexByte(dataInput, out byte writeVal)) return;
                packetToSend.Data[0] = writeVal;
                packetToSend.DataLength = 1;
            }

            LogMessage($"----- Sending USB Custom Command: {selectedCmdItem.Name} -----", Color.Indigo);
            UsbPacket response = SendUsbPacket(packetToSend);

            if (response != null)
            {
                LogMessage($"Response to {selectedCmdItem.Name}: {response.ParseContent()}");
                UpdateStatus($"Command '{selectedCmdItem.Name}' sent. Response received.", statusOkColor);
            }
            else
            {
                LogMessage($"No response or error for command {selectedCmdItem.Name}.", errorLogColor);
                UpdateStatus($"Error sending/receiving for '{selectedCmdItem.Name}'.", statusErrorColor);
            }
            LogMessage("----------------------------------------------------", Color.Indigo);
        }

        private bool TryParseHexByte(string input, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Hex input cannot be empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            input = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input.Substring(2) : input;
            if (input.Length > 2 || !byte.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                MessageBox.Show("Invalid hex byte value! Example: A5 or 0xA5", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
        #endregion

        #region USB Echo Logic (Detaylı Loglama)
        private void UsbEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUsbEchoRunning) return;
            SendUsbEchoPacket();
        }
        private void StartUsbEchoMode()
        {
            // customBulkDevice ve onun endpointlerini kullan
            if (isUsbEchoRunning) return;
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("Custom Bulk USB device not ready for USB Echo!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            isUsbEchoRunning = true;
            usbEchoPacketCount = 0;
            totalUsbSendTime = 0; totalUsbResponseTime = 0;
            minUsbSendTime = double.MaxValue; maxUsbSendTime = 0;
            minUsbResponseTime = double.MaxValue; maxUsbResponseTime = 0;
            UpdateButtonText();
            LogMessage("----- USB Echo Mode STARTED (Custom Bulk) -----", usbEchoLogColor);
            usbEchoTimer.Start();
        }
        private void StopUsbEchoMode()
        {
            if (!isUsbEchoRunning) return;
            usbEchoTimer.Stop();
            isUsbEchoRunning = false;
            UpdateButtonText();
            LogMessage("----- USB Echo Mode STOPPED (Custom Bulk) -----", usbEchoLogColor);
            if (usbEchoPacketCount > 0)
            {
                LogMessage($"Total USB Echo Packets: {usbEchoPacketCount}", usbEchoLogColor);
                LogMessage($"  Avg Send: {totalUsbSendTime / usbEchoPacketCount:F2}ms | Min: {minUsbSendTime:F2}ms | Max: {maxUsbSendTime:F2}ms", usbEchoLogColor);
                LogMessage($"  Avg Recv: {totalUsbResponseTime / usbEchoPacketCount:F2}ms | Min: {minUsbResponseTime:F2}ms | Max: {maxUsbResponseTime:F2}ms", usbEchoLogColor);
            }
        }
        private void SendUsbEchoPacket()
        {
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                StopUsbEchoMode();
                LogMessage("USB Echo: Custom Bulk Device lost. Stopping.", errorLogColor);
                return;
            }

            string echoDataStr = string.IsNullOrWhiteSpace(txtData.Text) ? "Default USB Echo Data" : txtData.Text.Trim();
            var packet = new UsbPacket { CommandId = UsbPacket.CMD_ECHO_STRING };
            packet.SetDataFromText(echoDataStr);

            UsbPacket resp = SendUsbPacket(packet);

            if (resp != null)
            {
                usbEchoPacketCount++;
                totalUsbSendTime += lastUsbSendTime;
                totalUsbResponseTime += lastUsbResponseTime;
                minUsbSendTime = Math.Min(minUsbSendTime, lastUsbSendTime);
                maxUsbSendTime = Math.Max(maxUsbSendTime, lastUsbSendTime);
                minUsbResponseTime = Math.Min(minUsbResponseTime, lastUsbResponseTime);
                maxUsbResponseTime = Math.Max(maxUsbResponseTime, lastUsbResponseTime);

                if (isUsbEchoRunning)
                    UpdateStatus($"USB Echo #{usbEchoPacketCount}: Send {lastUsbSendTime:F1}ms | Recv {lastUsbResponseTime:F1}ms", usbEchoLogColor);

                string receivedEchoStr = resp.GetDataAsText();
                bool dataMatch = echoDataStr == receivedEchoStr;

                if (!dataMatch)
                {
                    // Hata durumunda her zaman log basılmalı
                    LogMessage($"USB Echo Mismatch: Sent=\"{echoDataStr}\" | Recv=\"{receivedEchoStr}\"", errorLogColor);
                }
                else if (usbEchoPacketCount % 10 == 0 || usbEchoPacketCount == 1)
                {
                    // Standartlaştırılmış log formatı
                    int dataBytesLen = packet.ToByteArray().Length;
                    double sendMbps = dataBytesLen * 8.0 / (lastUsbSendTime / 1000.0) / 1000000.0;

                    LogMessage($"Echo #{usbEchoPacketCount} [USB] TX: {dataBytesLen} bytes in {lastUsbSendTime:F2}ms ({sendMbps:F2} Mbps)", usbEchoLogColor);

                    int respBytesLen = resp != null ? resp.ToByteArray().Length : 0;
                    double recvMbps = respBytesLen * 8.0 / (lastUsbResponseTime / 1000.0) / 1000000.0;

                    LogMessage($"Echo #{usbEchoPacketCount} [USB] RX: {respBytesLen} bytes in {lastUsbResponseTime:F2}ms ({recvMbps:F2} Mbps) - {(dataMatch ? "Match" : "Mismatch")}", usbEchoLogColor);

                    // İstatistikler
                    LogMessage($"Echo #{usbEchoPacketCount} [USB] Stats:", usbEchoLogColor);
                    LogMessage($"  TX: Avg {totalUsbSendTime / usbEchoPacketCount:F2}ms | Min {minUsbSendTime:F2}ms | Max {maxUsbSendTime:F2}ms", usbEchoLogColor);
                    LogMessage($"  RX: Avg {totalUsbResponseTime / usbEchoPacketCount:F2}ms | Min {minUsbResponseTime:F2}ms | Max {maxUsbResponseTime:F2}ms", usbEchoLogColor);
                }
            }
            else
            {
                LogMessage("USB Echo: SendUsbPacket returned null. Stopping.", errorLogColor);
                StopUsbEchoMode();
            }
        }
        #endregion

        #region UART Communication & Echo (Detaylı Loglama)
        private void SetupUartCommunication()
        {
            try
            {
                string[] portNames = SerialPort.GetPortNames();
                if (!portNames.Contains(selectedComPort) && portNames.Length > 0)
                {
                    LogMessage($"UART: Default port {selectedComPort} not found. Available: {string.Join(", ", portNames)}. Using {portNames[0]}.", statusWarnColor);
                    selectedComPort = portNames[0];
                }
                else if (portNames.Length == 0)
                {
                    LogMessage("UART: No COM ports available.", statusErrorColor);
                    UpdateStatus("UART: No COM ports.", statusErrorColor);
                    return;
                }

                uartPort = new SerialPort(selectedComPort, UART_BAUD_RATE, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200,
                    WriteTimeout = 200
                };
                LogMessage($"UART: Configured for {selectedComPort} at {UART_BAUD_RATE} baud.", Color.CadetBlue);
            }
            catch (Exception ex)
            {
                LogMessage($"UART Setup Error: {ex.Message}", errorLogColor);
            }
        }
        private void UartEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUartEchoRunning) return;
            SendUartEchoPacket();
        }
        private void StartUartEchoMode()
        {
            if (isUartEchoRunning) return;
            if (uartPort == null) { SetupUartCommunication(); }
            if (uartPort == null) { MessageBox.Show("UART port not available for Echo!", "UART Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            try { if (!uartPort.IsOpen) uartPort.Open(); }
            catch (Exception ex) { LogMessage($"UART Echo: Error opening port {uartPort.PortName}: {ex.Message}", errorLogColor); return; }

            isUartEchoRunning = true;
            uartEchoPacketCount = 0;
            totalUartSendTime = 0; totalUartResponseTime = 0;
            minUartSendTime = double.MaxValue; maxUartSendTime = 0;
            minUartResponseTime = double.MaxValue; maxUartResponseTime = 0;
            UpdateButtonText();
            LogMessage($"----- UART Echo Mode STARTED on {uartPort.PortName} -----", uartEchoLogColor);
            uartEchoTimer.Start();
        }
        private void StopUartEchoMode()
        {
            if (!isUartEchoRunning) return;
            uartEchoTimer.Stop();
            isUartEchoRunning = false;
            UpdateButtonText();
            LogMessage($"----- UART Echo Mode STOPPED on {uartPort.PortName} -----", uartEchoLogColor);
            if (uartEchoPacketCount > 0)
            {
                LogMessage($"Total UART Echo Packets: {uartEchoPacketCount}", uartEchoLogColor);
                LogMessage($"  Avg Send: {totalUartSendTime / uartEchoPacketCount:F2}ms | Min: {minUartSendTime:F2}ms | Max: {maxUartSendTime:F2}ms", uartEchoLogColor);
                LogMessage($"  Avg Recv: {totalUartResponseTime / uartEchoPacketCount:F2}ms | Min: {minUartResponseTime:F2}ms | Max: {maxUartResponseTime:F2}ms", uartEchoLogColor);
            }
        }
        private void SendUartEchoPacket()
        {
            if (uartPort == null || !uartPort.IsOpen)
            {
                StopUartEchoMode();
                LogMessage("UART Echo: Port not open. Stopping.", errorLogColor);
                return;
            }

            string dataToSendStr = string.IsNullOrWhiteSpace(txtData.Text) ? "Default UART Echo Data" : txtData.Text.Trim();
            byte[] bytesToSend = Encoding.ASCII.GetBytes(dataToSendStr);
            byte[] receivedBuffer = new byte[bytesToSend.Length];
            int bytesActuallyRead = 0;

            try
            {
                var swSend = Stopwatch.StartNew();
                uartPort.Write(bytesToSend, 0, bytesToSend.Length);
                swSend.Stop();
                lastUartSendTime = swSend.Elapsed.TotalMilliseconds;
                double uartSendMbps = bytesToSend.Length * 8.0 / (lastUartSendTime / 1000.0) / 1000000.0;

                var swRecv = Stopwatch.StartNew();
                try
                {
                    int totalRead = 0;
                    long startTimeMs = swRecv.ElapsedMilliseconds;
                    while (totalRead < receivedBuffer.Length && (swRecv.ElapsedMilliseconds - startTimeMs) < uartPort.ReadTimeout)
                    {
                        if (uartPort.BytesToRead > 0)
                        {
                            int readNow = uartPort.Read(receivedBuffer, totalRead, receivedBuffer.Length - totalRead);
                            if (readNow == 0) break;
                            totalRead += readNow;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(5);
                        }
                    }
                    bytesActuallyRead = totalRead;
                }
                catch (TimeoutException) { /* Handled by checking bytesActuallyRead */ }

                swRecv.Stop();
                lastUartResponseTime = swRecv.Elapsed.TotalMilliseconds;
                string receivedStr = Encoding.ASCII.GetString(receivedBuffer, 0, bytesActuallyRead);
                double uartRecvMbps = bytesActuallyRead * 8.0 / (lastUartResponseTime / 1000.0) / 1000000.0;

                bool dataMatch = bytesActuallyRead == bytesToSend.Length && dataToSendStr.Equals(receivedStr);

                if (dataMatch)
                {
                    uartEchoPacketCount++;
                    totalUartSendTime += lastUartSendTime;
                    totalUartResponseTime += lastUartResponseTime;
                    minUartSendTime = Math.Min(minUartSendTime, lastUartSendTime);
                    maxUartSendTime = Math.Max(maxUartSendTime, lastUartSendTime);
                    minUartResponseTime = Math.Min(minUartResponseTime, lastUartResponseTime);
                    maxUartResponseTime = Math.Max(maxUartResponseTime, lastUartResponseTime);

                    if (isUartEchoRunning)
                        UpdateStatus($"UART Echo #{uartEchoPacketCount}: Send {lastUartSendTime:F1}ms | Recv {lastUartResponseTime:F1}ms", uartEchoLogColor);

                    if (uartEchoPacketCount % 10 == 0 || uartEchoPacketCount == 1)
                    {
                        // Standartlaştırılmış log formatı - USB ile aynı format
                        LogMessage($"Echo #{uartEchoPacketCount} [UART] TX: {bytesToSend.Length} bytes in {lastUartSendTime:F2}ms ({uartSendMbps:F2} Mbps)", uartEchoLogColor);
                        LogMessage($"Echo #{uartEchoPacketCount} [UART] RX: {bytesActuallyRead} bytes in {lastUartResponseTime:F2}ms ({uartRecvMbps:F2} Mbps) - Match", uartEchoLogColor);

                        // İstatistikler
                        LogMessage($"Echo #{uartEchoPacketCount} [UART] Stats:", uartEchoLogColor);
                        LogMessage($"  TX: Avg {totalUartSendTime / uartEchoPacketCount:F2}ms | Min {minUartSendTime:F2}ms | Max {maxUartSendTime:F2}ms", uartEchoLogColor);
                        LogMessage($"  RX: Avg {totalUartResponseTime / uartEchoPacketCount:F2}ms | Min {minUartResponseTime:F2}ms | Max {maxUartResponseTime:F2}ms", uartEchoLogColor);
                    }
                }
                else
                {
                    // Hata durumunda her zaman log basılmalı
                    LogMessage($"Echo [UART] Mismatch: Sent=\"{dataToSendStr}\" ({bytesToSend.Length}B) | Recv=\"{receivedStr}\" ({bytesActuallyRead}B)", errorLogColor);
                    if (isUartEchoRunning)
                        UpdateStatus($"UART Echo #{uartEchoPacketCount}: Mismatch/Timeout", errorLogColor);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"UART Echo Error: {ex.Message}. Stopping.", errorLogColor);
                StopUartEchoMode();
            }
        }
        #endregion

        #region CAN Interface Logic
        private void HandleCanMessageFromCanHandler(CanMessage message)
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke(new Action(() =>
                {
                    ListView targetListView = (message.Direction == "Rx") ? listViewCanReceive : listViewCanTransmit;
                    Color itemColor = (message.Direction == "Rx") ? canRxLogColor : canTxLogColor;
                    AddCanMessageToUiListView(targetListView, message, itemColor);
                }));
            }
        }

        private void AddCanMessageToUiListView(ListView listView, CanMessage message, Color itemColor)
        {
            var lvi = new ListViewItem(message.SequenceNumber.ToString());
            lvi.SubItems.Add(message.UiTimestamp.ToString("HH:mm:ss.fff"));
            lvi.SubItems.Add(message.Id.ToString(message.Id > 0x7FF ? "X8" : "X3"));
            lvi.SubItems.Add(message.Length.ToString());
            lvi.SubItems.Add(message.DataToHexString());
            lvi.SubItems.Add(message.PSoCTimestamp.ToString());
            lvi.SubItems.Add(message.Properties.ToString("X2"));
            lvi.ForeColor = itemColor;
            lvi.Tag = message;

            listView.Items.Add(lvi);
            if (listView.Items.Count > 1500)
            {
                listView.Items.RemoveAt(0);
            }
            if (!isCanUiPaused)
            {
                lvi.EnsureVisible();
            }
        }

        private void BtnSendCanMessageViaHandler_Click(object sender, EventArgs e)
        {
            // Artık _canHandler'ın IsDeviceReady'si canDevice ve onun endpointlerini kontrol eder
            if (_canHandler == null || !_canHandler.IsDeviceReady)
            {
                MessageBox.Show("CAN USB function not ready for sending!", "CAN Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string idStr = txtCanTransmitId.Text.Trim().ToLower();
            if (idStr.StartsWith("0x")) idStr = idStr.Substring(2);
            if (idStr.EndsWith("h")) idStr = idStr.Substring(0, idStr.Length - 1);

            if (!uint.TryParse(idStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId))
            {
                MessageBox.Show("Invalid CAN ID. Please enter a hex value (e.g., 1A0 or 1234567).", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool isExtended = chkCanExtendedId.Checked;
            if (!isExtended && canId > 0x7FF)
            {
                MessageBox.Show("Standard CAN ID cannot exceed 0x7FF. For larger IDs, check 'Extended'.", "Input Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                chkCanExtendedId.Checked = true;
            }
            else if (isExtended && canId > 0x1FFFFFFF)
            {
                MessageBox.Show("Extended CAN ID cannot exceed 0x1FFFFFFF.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte dlc = (byte)numCanTransmitDlc.Value;
            if (!CanHandler.TryParseHexData(txtCanTransmitData.Text, dlc, out byte[] dataBytes))
            {
                MessageBox.Show($"Invalid data format for DLC {dlc}. Enter {dlc} hex bytes separated by spaces (e.g., 00 AA 11).", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var msgToSend = new CanMessage
            {
                Id = canId,
                Length = dlc,
                Properties = 0,
                PSoCTimestamp = 0
            };
            Array.Copy(dataBytes, msgToSend.Data, 8);

            if (!_canHandler.SendCanMessage(msgToSend))
            {
                LogMessage("Failed to send CAN message. Check CAN logs.", errorLogColor);
            }
        }
        #endregion

        #region MainForm Load & Closing
        private void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Application_Started. Version: " + Application.ProductVersion);
            UpdateStatus("Initializing...", Color.Gray);
            // AttemptToFindAndConfigureDevice() is called by SetupUsbMonitoring
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogMessage("Application_Closing...", Color.Gray);
            if (isUsbEchoRunning) StopUsbEchoMode();
            if (isUartEchoRunning) StopUartEchoMode();
            _canHandler?.StopListening();

            deviceCheckTimer?.Stop(); deviceCheckTimer?.Dispose();
            usbEchoTimer?.Dispose();
            uartEchoTimer?.Dispose();
            _canHandler?.Dispose();
            uartPort?.Close(); uartPort?.Dispose();

            // usbDevices.Dispose() çağrıldığında içindeki tüm CyUSBDevice nesneleri de (customBulkDevice, canDevice)
            // (eğer aynı fiziksel cihazın farklı "görünümleri" değillerse ve usbDevices listesinden geldilerse)
            // dispose edilir. Eğer farklı fiziksel cihazlarsa, her birini ayrı yönetmek gerekebilir.
            // Şimdilik usbDevices.Dispose()'un yeterli olduğunu varsayıyoruz.
            usbDevices?.Dispose();
            customBulkDevice = null;
            canDevice = null;

            LogMessage("Application_Closed.");
        }
        #endregion

        #region Logging & Status Updates
        private void LogMessage(string message, Color? textColor = null)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(() => LogInternal(message, textColor)));
            }
            else
            {
                LogInternal(message, textColor);
            }
        }
        private void LogInternal(string message, Color? textColor)
        {
            if (txtLog.IsDisposed) return;
            Color originalColor = txtLog.SelectionColor;
            txtLog.SelectionColor = textColor ?? txtLog.ForeColor;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            txtLog.SelectionColor = originalColor;
            if (txtLog.Lines.Length > 1000)
            {
                txtLog.Select(0, txtLog.GetFirstCharIndexFromLine(txtLog.Lines.Length - 800));
                txtLog.SelectedText = "";
            }
            txtLog.ScrollToCaret();
        }

        private void UpdateStatus(string message, Color color)
        {
            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.BeginInvoke(new Action(() => UpdateStatusInternal(message, color)));
            }
            else
            {
                UpdateStatusInternal(message, color);
            }
        }
        private void UpdateStatusInternal(string message, Color color)
        {
            if (toolStripStatusLabel1.IsDisposed) return;
            toolStripStatusLabel1.Text = message;
            toolStripStatusLabel1.ForeColor = color;
        }
        #endregion
    }

    // CommandItem ve UsbPacket sınıfları
    public class CommandItem
    {
        public string Name { get; set; }
        public byte CommandId { get; set; }
        public CommandItem(string name, byte commandId) { Name = name; CommandId = commandId; }
        public override string ToString() => Name;
    }


}