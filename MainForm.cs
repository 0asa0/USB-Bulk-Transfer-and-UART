using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CyUSB;

namespace usb_bulk_2
{
    public partial class MainForm : Form
    {
        private USBDeviceList usbDevices;
        private CyUSBDevice myDevice;
        private CyBulkEndPoint outEndpoint;
        private CyBulkEndPoint inEndpoint;
        private Timer deviceCheckTimer;

        // Daha hassas ölçüm için double kullanıyoruz
        private double lastSendTime = 0;
        private double lastResponseTime = 0;

        // USB VID/PID
        private const int USB_VID = 0x04B4;  // Cypress VID
        private const int USB_PID = 0x00F0;  // PSoC için seçtiğiniz PID

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            SetupControls();
            SetupUsbMonitoring();
        }

        private void SetupControls()
        {
            cmbCommands.Items.Add(new CommandItem("Read", UsbPacket.CMD_READ));
            cmbCommands.Items.Add(new CommandItem("Write", UsbPacket.CMD_WRITE));
            cmbCommands.Items.Add(new CommandItem("Status", UsbPacket.CMD_STATUS));
            cmbCommands.Items.Add(new CommandItem("Reset", UsbPacket.CMD_RESET));
            cmbCommands.Items.Add(new CommandItem("Version", UsbPacket.CMD_VERSION));
            cmbCommands.Items.Add(new CommandItem("String Echo", UsbPacket.CMD_ECHO_STRING));
            cmbCommands.SelectedIndex = 0;

            btnSend.Click += BtnSend_Click;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ReadOnly = true;
        }

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

        private void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            if (myDevice == null)
                FindDevice();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Uygulama başlatıldı");
            LogMessage("USB cihaz aranıyor...");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            deviceCheckTimer?.Stop();
            deviceCheckTimer?.Dispose();
            usbDevices?.Dispose();
        }

        private void FindDevice()
        {
            try
            {
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
                            UpdateStatus("Cihaz bağlandı!", Color.Green);
                            LogMessage($"Cihaz bulundu: {dev.FriendlyName}");
                            SendVersionQuery();
                            return;
                        }
                        myDevice = null;
                    }
                }
                UpdateStatus("Cihaz bulunamadı!", Color.Red);
            }
            catch (Exception ex)
            {
                LogMessage($"Hata: {ex.Message}");
                UpdateStatus("Hata oluştu!", Color.Red);
            }
        }

        private void SendVersionQuery()
        {
            try
            {
                var packet = new UsbPacket { CommandId = UsbPacket.CMD_VERSION, DataLength = 0 };
                var response = SendPacket(packet);
                if (response != null)
                {
                    LogMessage("Versiyon sorgusu gönderildi");
                    LogMessage(response.ParseContent());
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Versiyon sorgusu hatası: {ex.Message}");
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("Cihaz bağlı değil!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = cmbCommands.SelectedItem as CommandItem;
            if (selected == null) return;

            var packet = new UsbPacket { CommandId = selected.CommandId };
            string input = txtData.Text.Trim();
            if (selected.CommandId == UsbPacket.CMD_WRITE)
            {
                if (!TryParseHex(input, out byte val)) return;
                packet.Data[0] = val;
                packet.DataLength = 1;
            }
            else if (selected.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                if (string.IsNullOrEmpty(input))
                {
                    MessageBox.Show("String girişi gerekli!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                packet.SetDataFromText(input);
                LogMessage($"Gönderilecek string: \"{input}\"");
                LogMessage($"Hex: {packet.GetDataAsHexString()}");
            }

            LogMessage($"----- Paket Gönderiliyor ({selected.Name}) -----");
            var resp = SendPacket(packet);
            if (resp != null)
            {
                LogMessage(resp.ParseContent());
                LogMessage($"Süreler: Gönderim={lastSendTime:F3} ms, Yanıt={lastResponseTime:F3} ms");
                LogMessage($"------------------------------------------------------------------------");
                LogMessage($"------------------------------------------------------------------------");
                UpdateStatus($"Gönderim: {lastSendTime:F1} ms | Yanıt: {lastResponseTime:F1} ms", Color.Blue);
            }
            else
            {
                UpdateStatus("İletişim hatası!", Color.Red);
            }
        }

        private UsbPacket SendPacket(UsbPacket packet)
        {
            byte[] outData = packet.ToByteArray();
            byte[] inData = new byte[64];
            int outLen = outData.Length;
            int inLen = inData.Length;
            UsbPacket response = null;

            try
            {
                var swSend = System.Diagnostics.Stopwatch.StartNew();
                bool okS = outEndpoint.XferData(ref outData, ref outLen);
                swSend.Stop();
                lastSendTime = swSend.Elapsed.TotalMilliseconds;

                if (!okS)
                {
                    LogMessage("Hata: Veri gönderilemedi!");
                    return null;
                }

                double sendMbps = (outLen * 8.0 / 1_000_000.0) / (lastSendTime / 1000.0);
                LogMessage($"Gönderim: {outLen} byte -> {lastSendTime:F3} ms -> {sendMbps:F2} Mb/s");

                var swR = System.Diagnostics.Stopwatch.StartNew();
                bool okR = inEndpoint.XferData(ref inData, ref inLen);
                swR.Stop();
                lastResponseTime = swR.Elapsed.TotalMilliseconds;

                if (!okR)
                {
                    LogMessage("Hata: Yanıt alınamadı!");
                    return null;
                }

                double recvMbps = (inLen * 8.0 / 1_000_000.0) / (lastResponseTime / 1000.0);
                LogMessage($"Alım: {inLen} byte -> {lastResponseTime:F3} ms -> {recvMbps:F2} Mb/s");

                response = UsbPacket.FromByteArray(inData);
            }
            catch (Exception ex)
            {
                LogMessage($"USB iletişim hatası: {ex.Message}");
            }

            return response;
        }

        private bool TryParseHex(string input, out byte value)
        {
            value = 0;
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                input = input.Substring(2);
            if (byte.TryParse(input, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;
            MessageBox.Show("Geçersiz hex değeri! Örnek: 0xA5 veya A5", "Hata",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private void LogMessage(string message)
        {
            if (txtLog.InvokeRequired)
                txtLog.Invoke(new Action<string>(LogMessage), message);
            else
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
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

        private void USBDeviceAttached(object sender, EventArgs e) => FindDevice();
        private void USBDeviceRemoved(object sender, EventArgs e)
        {
            if (myDevice != null && !DeviceExistsInList(myDevice))
            {
                myDevice = null;
                outEndpoint = inEndpoint = null;
                UpdateStatus("Cihaz çıkarıldı!", Color.Red);
                LogMessage("Cihaz çıkarıldı");
            }
        }

        private bool DeviceExistsInList(CyUSBDevice device)
        {
            foreach (var d in usbDevices)
                if (d == device)
                    return true;
            return false;
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
