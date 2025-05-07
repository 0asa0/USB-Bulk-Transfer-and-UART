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
            // Komutlar için ComboBox'ı doldur
            cmbCommands.Items.Add(new CommandItem("Read", UsbPacket.CMD_READ));
            cmbCommands.Items.Add(new CommandItem("Write", UsbPacket.CMD_WRITE));
            cmbCommands.Items.Add(new CommandItem("Status", UsbPacket.CMD_STATUS));
            cmbCommands.Items.Add(new CommandItem("Reset", UsbPacket.CMD_RESET));
            cmbCommands.Items.Add(new CommandItem("Version", UsbPacket.CMD_VERSION));
            cmbCommands.SelectedIndex = 0;

            // Buton olayları
            btnSend.Click += BtnSend_Click;

            // Log görüntüleme ayarları
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ReadOnly = true;
        }

        private void SetupUsbMonitoring()
        {
            // USB cihaz listesi oluştur
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);

            // USB olay işleyicileri
            usbDevices.DeviceAttached += new EventHandler(USBDeviceAttached);
            usbDevices.DeviceRemoved += new EventHandler(USBDeviceRemoved);

            // Periyodik cihaz kontrolü için zamanlayıcı
            deviceCheckTimer = new Timer();
            deviceCheckTimer.Interval = 2000; // 2 saniye
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick;
            deviceCheckTimer.Start();

            // İlk cihaz kontrolü
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
                // Seçilen komutu al
                if (cmbCommands.SelectedItem is CommandItem selectedCommand)
                {
                    UsbPacket packet = new UsbPacket();
                    packet.CommandId = selectedCommand.CommandId;

                    // Komuta göre veri hazırla
                    switch (selectedCommand.CommandId)
                    {
                        case UsbPacket.CMD_WRITE:
                            // TextBox'tan metni al
                            string inputText = txtData.Text;
                            if (!string.IsNullOrEmpty(inputText))
                            {
                                // Metni byte dizisine dönüştür
                                byte[] textBytes = Encoding.ASCII.GetBytes(inputText);

                                // Veri uzunluğunu kontrol et
                                if (textBytes.Length > UsbPacket.MAX_DATA_SIZE)
                                {
                                    LogMessage($"Uyarı: Veri çok uzun! Max {UsbPacket.MAX_DATA_SIZE} byte gönderebilirsiniz.");
                                    Array.Resize(ref textBytes, UsbPacket.MAX_DATA_SIZE);
                                }

                                // Veriyi pakete ekle
                                packet.DataLength = (byte)textBytes.Length;
                                Array.Copy(textBytes, packet.Data, textBytes.Length);

                                LogMessage($"Gönderilen metin: {inputText}");
                                LogMessage($"Hex olarak: {BitConverter.ToString(textBytes).Replace("-", " ")}");
                            }
                            else
                            {
                                // Veri yoksa varsayılan durum değeri (0x01) gönder
                                packet.DataLength = 1;
                                packet.Data[0] = 0x01;
                            }
                            break;

                        case UsbPacket.CMD_READ:
                        case UsbPacket.CMD_STATUS:
                        case UsbPacket.CMD_RESET:
                        case UsbPacket.CMD_VERSION:
                            // Bu komutlar için ek veri gerekmez
                            packet.DataLength = 0;
                            break;

                        default:
                            // Diğer özel komutlar için
                            if (!string.IsNullOrEmpty(txtData.Text))
                            {
                                // Metni doğrudan byte'a dönüştür
                                byte[] customData = Encoding.ASCII.GetBytes(txtData.Text);
                                packet.DataLength = (byte)Math.Min(customData.Length, UsbPacket.MAX_DATA_SIZE);
                                Array.Copy(customData, packet.Data, packet.DataLength);
                            }
                            break;
                    }

                    // Paketi gönder ve cevap al
                    UsbPacket response = SendPacket(packet);

                    if (response != null)
                    {
                        LogMessage("Komut başarıyla gönderildi");
                        LogMessage(response.ParseContent());

                        // Eğer yanıt metin içeriyorsa göster
                        if (response.DataLength > 1 && response.GetResultCode() == UsbPacket.RESULT_OK)
                        {
                            try
                            {
                                // İlk byte sonuç kodu olduğundan onu atlayıp geri kalanını metin olarak göster
                                byte[] responseTextBytes = new byte[response.DataLength - 1];
                                Array.Copy(response.Data, 1, responseTextBytes, 0, response.DataLength - 1);

                                string responseText = Encoding.ASCII.GetString(responseTextBytes);
                                LogMessage($"Yanıt metni: {responseText}");
                            }
                            catch { /* Metin dönüşümü başarısızsa bu kısmı atla */ }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Hata: {ex.Message}");
            }
        }

        private UsbPacket SendPacket(UsbPacket packet)
        {
            if (myDevice == null || outEndpoint == null || inEndpoint == null)
                return null;

            try
            {
                // Paketi byte dizisine dönüştür
                byte[] outData = packet.ToByteArray();
                byte[] inData = new byte[64]; // Alınacak veri buffer'ı

                int outLength = outData.Length;
                int inLength = 64;

                // Veriyi gönder
                if (!outEndpoint.XferData(ref outData, ref outLength))
                {
                    LogMessage("Gönderim hatası: Veri gönderilemedi!");
                    return null;
                }

                // Yanıtı bekle (kısa bir süre)
                System.Threading.Thread.Sleep(10);

                // Cevabı al
                if (!inEndpoint.XferData(ref inData, ref inLength))
                {
                    LogMessage("Alım hatası: Yanıt alınamadı!");
                    return null;
                }

                try
                {
                    // Alınan veriyi pakete dönüştür
                    return UsbPacket.FromByteArray(inData);
                }
                catch (Exception ex)
                {
                    LogMessage($"Paket çözme hatası: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Transfer hatası: {ex.Message}");
                return null;
            }
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