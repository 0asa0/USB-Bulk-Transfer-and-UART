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

        private System.Diagnostics.Stopwatch transferStopwatch = new System.Diagnostics.Stopwatch();
        private long lastSendTime = 0;
        private long lastResponseTime = 0;
        // USB VID/PID
        private const int USB_VID = 0x04B4;  // Cypress VID
        private const int USB_PID = 0xF0;  // PSoC için seçtiğiniz PID

        public MainForm()
        {
            InitializeComponent();

            // Form yükleme işlemi
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;

            // Kontrolleri düzenle
            SetupControls();

            // USB cihazları izleme
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

            usbDevices.DeviceAttached += new EventHandler(USBDeviceAttached);
            usbDevices.DeviceRemoved += new EventHandler(USBDeviceRemoved);

            deviceCheckTimer = new Timer();
            deviceCheckTimer.Interval = 2000; // 2 saniye
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick;
            deviceCheckTimer.Start();

            FindDevice();
        }

        private void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            // Cihaz durumunu periyodik olarak kontrol et
            if (myDevice == null)
            {
                FindDevice();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Uygulama başlatıldı");
            LogMessage("USB cihaz aranıyor...");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Zamanlayıcıyı durdur
            if (deviceCheckTimer != null)
            {
                deviceCheckTimer.Stop();
                deviceCheckTimer.Dispose();
            }

            // USB cihaz listesini temizle
            if (usbDevices != null)
            {
                usbDevices.Dispose();
            }
        }

        private void FindDevice()
        {
            try
            {
                foreach (CyUSBDevice dev in usbDevices)
                {
                    if ((dev.VendorID == USB_VID) && (dev.ProductID == USB_PID))
                    {
                        myDevice = dev;

                        // Bulk endpointleri bul
                        outEndpoint = null;
                        inEndpoint = null;

                        foreach (CyUSBEndPoint ept in myDevice.EndPoints)
                        {
                            if (ept.Attributes == 2) // Bulk transfer type
                            {
                                if (ept.bIn) // Check if the endpoint is IN
                                    inEndpoint = (CyBulkEndPoint)ept;
                                else
                                    outEndpoint = (CyBulkEndPoint)ept;
                            }
                        }

                        if (outEndpoint != null && inEndpoint != null)
                        {
                            UpdateStatus("Cihaz bağlandı!", Color.Green);
                            LogMessage($"Cihaz bulundu: {dev.FriendlyName}");
                            LogMessage($"Üretici: {dev.Manufacturer}");
                            LogMessage($"Ürün: {dev.Product}");
                            LogMessage($"Seri No: {dev.SerialNumber}");

                            // Cihaz versiyonunu sorgula
                            SendVersionQuery();

                            return;
                        }
                        else
                        {
                            LogMessage("HATA: Bulk endpointler bulunamadı!");
                            myDevice = null;
                        }
                    }
                }

                if (myDevice == null)
                {
                    UpdateStatus("Cihaz bulunamadı!", Color.Red);
                }
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
                // Versiyon sorgusu paketi oluştur
                UsbPacket packet = new UsbPacket();
                packet.CommandId = UsbPacket.CMD_VERSION;
                packet.DataLength = 0;

                UsbPacket response = SendPacket(packet);
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
                LogMessage("Uyarı: Cihaz bağlı değil!");
                MessageBox.Show("Cihaz bağlı değil!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (cmbCommands.SelectedItem is CommandItem selectedCommand)
                {
                    UsbPacket packet = new UsbPacket();
                    packet.CommandId = selectedCommand.CommandId;

                    string inputData = txtData.Text.Trim();

                    switch (selectedCommand.CommandId)
                    {
                        case UsbPacket.CMD_WRITE:
                            if (!string.IsNullOrEmpty(inputData))
                            {
                                if (inputData.StartsWith("0x"))
                                    inputData = inputData.Substring(2);

                                byte value;
                                if (byte.TryParse(inputData, System.Globalization.NumberStyles.HexNumber,
                                                 System.Globalization.CultureInfo.InvariantCulture, out value))
                                {
                                    packet.Data[0] = value;
                                    packet.DataLength = 1;
                                }
                                else
                                {
                                    MessageBox.Show("Geçersiz hex değeri! Örnek: 0xA5 veya A5", "Hata",
                                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Veri girişi gerekli!", "Uyarı",
                                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            break;

                        case UsbPacket.CMD_ECHO_STRING:
                            if (!string.IsNullOrEmpty(inputData))
                            {
                                // String verisini ascii olarak işle
                                byte[] stringBytes = Encoding.ASCII.GetBytes(inputData);

                                // Buffer boyutunu aşmamalı
                                int copyLength = Math.Min(stringBytes.Length, UsbPacket.MAX_DATA_SIZE);

                                Array.Copy(stringBytes, 0, packet.Data, 0, copyLength);
                                packet.DataLength = (byte)copyLength;

                                LogMessage($"Gönderilecek string: \"{inputData}\"");
                                LogMessage($"Hex karşılığı: {BitConverter.ToString(stringBytes, 0, copyLength).Replace("-", " ")}");
                            }
                            else
                            {
                                MessageBox.Show("String girişi gerekli!", "Uyarı",
                                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            break;
                    }

                    LogMessage($"----- Paket Gönderiliyor ({selectedCommand.Name}) -----");

                    UsbPacket response = SendPacket(packet);

                    if (response != null)
                    {
                        LogMessage(response.ParseContent());
                        LogMessage($"İletişim Süresi: Gönderim={lastSendTime}ms, Toplam={lastResponseTime}ms");

                        // Süreleri durum çubuğunda da göster
                        UpdateStatus($"Gönderim: {lastSendTime}ms | Yanıt: {lastResponseTime}ms", Color.Blue);
                    }
                    else
                    {
                        LogMessage("Yanıt alınamadı!");
                        UpdateStatus("İletişim hatası!", Color.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Hata: {ex.Message}");
                UpdateStatus("Hata oluştu!", Color.Red);
            }
        }

        private UsbPacket SendPacket(UsbPacket packet)
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null)
                throw new Exception("USB cihazı bağlı değil");

            byte[] outData = packet.ToByteArray();
            byte[] inData = new byte[64];

            int outLength = outData.Length;
            int inLength = inData.Length;

            UsbPacket response = null;

            try
            {
                transferStopwatch.Restart();

                if (outEndpoint.XferData(ref outData, ref outLength) == true)
                {
                    lastSendTime = transferStopwatch.ElapsedMilliseconds;

                    if (inEndpoint.XferData(ref inData, ref inLength) == true)
                    {
                        lastResponseTime = transferStopwatch.ElapsedMilliseconds;

                        response = UsbPacket.FromByteArray(inData);
                    }
                    else
                    {
                        LogMessage("Hata: Yanıt alınamadı!");
                    }
                }
                else
                {
                    LogMessage("Hata: Veri gönderilemedi!");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"USB iletişim hatası: {ex.Message}");
            }
            finally
            {
                transferStopwatch.Stop();
            }

            return response;
        }

        private void LogMessage(string message)
        {
            // Eğer farklı thread'den çağrılırsa Invoke kullan
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(LogMessage), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            txtLog.AppendText($"[{timestamp}] {message}\r\n");
            txtLog.ScrollToCaret();
        }

        private void UpdateStatus(string message, Color color)
        {
            // Eğer farklı thread'den çağrılırsa Invoke kullan
            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.Invoke(new Action<string, Color>(UpdateStatus), message, color);
                return;
            }

            toolStripStatusLabel1.Text = message;
            toolStripStatusLabel1.ForeColor = color;
        }

        private void USBDeviceAttached(object sender, EventArgs e)
        {
            FindDevice();
        }

        private void USBDeviceRemoved(object sender, EventArgs e)
        {
            if (myDevice != null && !DeviceExistsInList(myDevice))
            {
                myDevice = null;
                inEndpoint = null;
                outEndpoint = null;
                UpdateStatus("Cihaz çıkarıldı!", Color.Red);
                LogMessage("Cihaz çıkarıldı");
            }
        }

        private bool DeviceExistsInList(CyUSBDevice device)
        {
            foreach (CyUSBDevice dev in usbDevices)
            {
                if (dev == device)
                {
                    return true;
                }
            }
            return false;
        }

    }

    // Komut öğelerini tutmak için yardımcı sınıf
    public class CommandItem
    {
        public string Name { get; set; }
        public byte CommandId { get; set; }

        public CommandItem(string name, byte commandId)
        {
            Name = name;
            CommandId = commandId;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}