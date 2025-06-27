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

        // Custom Bulk transfer için Endpointler
        private CyBulkEndPoint customOutEndpoint; // EP1 (0x01)
        private CyBulkEndPoint customInEndpoint;  // EP2 (0x82)

        // CAN için Endpointler
        private CyBulkEndPoint canOutEndpoint;    // EP7 (0x07)
        private CyBulkEndPoint canInEndpoint;     // EP6 (0x86)

        // SendUsbPacket'in kullandığı genel endpointler (customBulkDevice'a ait olacak)
        private CyBulkEndPoint outEndpoint;
        private CyBulkEndPoint inEndpoint;

        private System.Windows.Forms.Timer deviceCheckTimer;

        private System.Windows.Forms.Timer usbEchoTimer;
        private bool isUsbEchoRunning = false;
        private double lastUsbSendTime, lastUsbResponseTime;
        private int usbEchoPacketCount;
        private double totalUsbSendTime, totalUsbResponseTime;
        private double minUsbSendTime = double.MaxValue, maxUsbSendTime = 0;
        private double minUsbResponseTime = double.MaxValue, maxUsbResponseTime = 0;

        private const int USB_VID = 0x04B4;
        private const int USB_PID = 0xF001;

        private const string CUSTOM_BULK_FRIENDLY_NAME_PART = "asa usb bulk"; 
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

        // MainForm sınıfının yapıcı metodudur.
        // Form başlatıldığında çağrılır, temel ayarları yapar ve olayları bağlar.
        public MainForm()
        {
            InitializeComponent(); // Windows Forms tasarımcısı tarafından oluşturulan bileşenleri başlatır.
            this.Load += MainForm_Load; // Form yüklendiğinde MainForm_Load metodunu çağırır.
            this.FormClosing += MainForm_FormClosing; // Form kapanırken MainForm_FormClosing metodunu çağırır.

            SetupCustomControls(); // Özel kullanıcı arayüzü kontrollerini ayarlar.
            SetupCanInterfaceLogic(); // CAN arayüzü mantığını ayarlar.
            SetupUsbMonitoring(); // USB cihaz izleme mekanizmasını ayarlar.
            SetupEchoTimers(); // USB ve UART echo testleri için zamanlayıcıları ayarlar.
            SetupUartCommunication(); // UART iletişimi için seri portu ayarlar.
        }

        // Özel kullanıcı arayüzü kontrollerini (komut listesi, butonlar, log alanı, CAN listeleri) ayarlar.
        private void SetupCustomControls()
        {
            // Eğer UsbPacket sınıfı mevcutsa, komutları cmbCommands kontrolüne ekler.
            if (typeof(MainForm).Assembly.GetType("usb_bulk_2.UsbPacket") != null)
            {
                cmbCommands.Items.Add(new CommandItem("Read", UsbPacket.CMD_READ));
                cmbCommands.Items.Add(new CommandItem("Write", UsbPacket.CMD_WRITE));
                cmbCommands.Items.Add(new CommandItem("Status", UsbPacket.CMD_STATUS));
                cmbCommands.Items.Add(new CommandItem("Reset", UsbPacket.CMD_RESET));
                cmbCommands.Items.Add(new CommandItem("Version", UsbPacket.CMD_VERSION));
                cmbCommands.Items.Add(new CommandItem("USB String Echo", UsbPacket.CMD_ECHO_STRING));
                cmbCommands.Items.Add(new CommandItem("UART String Echo", UsbPacket.CMD_UART_ECHO_STRING));
                if (cmbCommands.Items.Count > 0) cmbCommands.SelectedIndex = 0; // Eğer komut varsa, ilk komutu seçili hale getirir.
            }
            else
            {
                LogMessage("UsbPacket class cannot found. USB Control tab commands may not work.", errorLogColor); // UsbPacket sınıfı bulunamazsa hata mesajı loglar.
            }

            btnSend.Click += BtnSendCustomCommand_Click; // Gönder butonuna tıklandığında BtnSendCustomCommand_Click metodunu çağırır.
            txtLog.Font = new Font("Consolas", 9F); // Log alanının yazı tipini ayarlar.
            txtLog.ReadOnly = true; // Log alanını sadece okunabilir yapar.
            cmbCommands.SelectedIndexChanged += CmbCommands_SelectedIndexChanged; // Komut seçimi değiştiğinde CmbCommands_SelectedIndexChanged metodunu çağırır.

            listViewCanReceive.Font = new Font("Consolas", 8.5F); // Alınan CAN mesajları listesinin yazı tipini ayarlar.
            listViewCanTransmit.Font = new Font("Consolas", 8.5F); // Gönderilen CAN mesajları listesinin yazı tipini ayarlar.
            listViewCanReceive.DoubleClick += ListViewCan_DoubleClick; // Alınan CAN listesine çift tıklandığında ListViewCan_DoubleClick metodunu çağırır.
            listViewCanTransmit.DoubleClick += ListViewCan_DoubleClick; // Gönderilen CAN listesine çift tıklandığında ListViewCan_DoubleClick metodunu çağırır.

            // CAN listeleri için sağ tık menüsü oluşturur.
            ContextMenuStrip cmsCanList = new ContextMenuStrip();
            cmsCanList.Items.Add("Clear List", null, (s, ev) => { // "Listeyi Temizle" seçeneği
                if (cmsCanList.SourceControl == listViewCanReceive) listViewCanReceive.Items.Clear(); // Eğer kaynak alınanlar listesi ise onu temizler.
                else if (cmsCanList.SourceControl == listViewCanTransmit) listViewCanTransmit.Items.Clear(); // Eğer kaynak gönderilenler listesi ise onu temizler.
            });
            cmsCanList.Items.Add(new ToolStripSeparator()); // Ayırıcı ekler.
            var pauseUpdatesItem = cmsCanList.Items.Add("Pause Updates", null, (s, ev) => { // "Güncellemeleri Duraklat" seçeneği
                isCanUiPaused = !isCanUiPaused; // Duraklatma durumunu tersine çevirir.
                ((ToolStripMenuItem)s).Checked = isCanUiPaused; // Menü öğesinin işaretli durumunu günceller.
            });
            ((ToolStripMenuItem)pauseUpdatesItem).CheckOnClick = true; // Tıklandığında işaret durumunun otomatik değişmesini sağlar.

            listViewCanReceive.ContextMenuStrip = cmsCanList; // Alınan CAN listesine sağ tık menüsünü atar.
            listViewCanTransmit.ContextMenuStrip = cmsCanList; // Gönderilen CAN listesine sağ tık menüsünü atar.
        }

        // CAN mesaj listelerindeki bir öğeye çift tıklandığında çağrılır.
        // Seçilen CAN mesajının detaylarını bir mesaj kutusunda gösterir.
        private void ListViewCan_DoubleClick(object sender, EventArgs e)
        {
            ListView lv = sender as ListView; // Olayı tetikleyen ListView kontrolünü alır.
            if (lv != null && lv.SelectedItems.Count > 0) // Eğer ListView geçerli ve seçili bir öğe varsa
            {
                ListViewItem selectedItem = lv.SelectedItems[0]; // İlk seçili öğeyi alır.
                if (selectedItem.Tag is CanMessage canMsg) // Eğer öğenin Tag'i bir CanMessage nesnesi ise (daha zengin veri içerir)
                {
                    // CanMessage nesnesinden detayları formatlar.
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
                else // Eğer Tag bir CanMessage değilse (eski veya ham metin tabanlı olabilir)
                {
                    // ListViewItem'ın alt öğelerinden (SubItems) detayları formatlar.
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

        // USB ve UART echo testleri için kullanılan zamanlayıcıları ayarlar.
        private void SetupEchoTimers()
        {
            // USB echo zamanlayıcısını 100ms aralıkla ayarlar ve Tick olayına UsbEchoTimer_Tick metodunu bağlar.
            usbEchoTimer = new System.Windows.Forms.Timer { Interval = 100 };
            usbEchoTimer.Tick += UsbEchoTimer_Tick;

            // UART echo zamanlayıcısını 100ms aralıkla ayarlar ve Tick olayına UartEchoTimer_Tick metodunu bağlar.
            uartEchoTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uartEchoTimer.Tick += UartEchoTimer_Tick;
        }

        // cmbCommands kontrolündeki seçili komut değiştiğinde çağrılır.
        // Gönder butonunun metnini güncellemek için UpdateButtonText metodunu çağırır.
        private void CmbCommands_SelectedIndexChanged(object sender, EventArgs e) => UpdateButtonText();

        // Gönder (btnSend) butonunun metnini, seçili komuta göre günceller.
        // Özellikle echo modları için "Start/Stop Echo" şeklinde dinamik metin sağlar.
        private void UpdateButtonText()
        {
            if (cmbCommands.SelectedItem is CommandItem selected) // Seçili öğe bir CommandItem ise
            {
                // Seçili komut USB echo ise ve echo çalışıyorsa "Stop USB Echo", çalışmıyorsa "Start USB Echo" yazar.
                if (selected.CommandId == UsbPacket.CMD_ECHO_STRING) btnSend.Text = isUsbEchoRunning ? "Stop USB Echo" : "Start USB Echo";
                // Seçili komut UART echo ise ve echo çalışıyorsa "Stop UART Echo", çalışmıyorsa "Start UART Echo" yazar.
                else if (selected.CommandId == UsbPacket.CMD_UART_ECHO_STRING) btnSend.Text = isUartEchoRunning ? "Stop UART Echo" : "Start UART Echo";
                // Diğer komutlar için "Send Command" yazar.
                else btnSend.Text = "Send Command";
            }
            else btnSend.Text = "Send Command"; // Eğer bir komut seçili değilse varsayılan metni yazar.
        }

        // CAN arayüzü ile ilgili mantığı ve CanHandler nesnesini ayarlar.
        private void SetupCanInterfaceLogic()
        {
            _canHandler = new CanHandler(); // CanHandler nesnesini oluşturur.
            _canHandler.CanMessageReceived += HandleCanMessageFromCanHandler; // CanHandler'dan CAN mesajı alındığında HandleCanMessageFromCanHandler metodunu çağırır.
            _canHandler.LogMessageRequest += (msg, color) => LogMessage($"[CAN] {msg}", color); // CanHandler'dan log mesajı isteği geldiğinde ana log alanına yazar.
            btnSendCanMessage.Click += BtnSendCanMessageViaHandler_Click; // CAN mesajı gönderme butonuna tıklandığında BtnSendCanMessageViaHandler_Click metodunu çağırır.
        }


        #region USB Device Management (FriendlyName ile Güncellendi)
        // USB cihazlarını izlemek için gerekli CyUSB nesnelerini ve olayları ayarlar.
        // Periyodik cihaz kontrolü için bir zamanlayıcı başlatır.
        private void SetupUsbMonitoring()
        {
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); // CyUSB kütüphanesi ile kullanılabilir USB cihazlarının listesini alır.
            usbDevices.DeviceAttached += USBDeviceAttached; // Bir USB cihazı takıldığında USBDeviceAttached metodunu çağırır.
            usbDevices.DeviceRemoved += USBDeviceRemoved; // Bir USB cihazı çıkarıldığında USBDeviceRemoved metodunu çağırır.
            deviceCheckTimer = new System.Windows.Forms.Timer { Interval = 3000 }; // 3 saniyede bir çalışacak zamanlayıcı oluşturur.
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick; // Zamanlayıcı her tetiklendiğinde DeviceCheckTimer_Tick metodunu çağırır.
            deviceCheckTimer.Start(); // Zamanlayıcıyı başlatır.
            AttemptToFindAndConfigureDevice(); // Başlangıçta bağlı olan cihazları bulup yapılandırmaya çalışır.
        }

        // Periyodik olarak (deviceCheckTimer ile) çağrılır.
        // "Custom Bulk" ve "CAN" USB fonksiyonlarının hala bağlı ve çalışır durumda olup olmadığını kontrol eder.
        // Eğer bir veya daha fazlası hazır değilse, yeniden yapılandırma girişiminde bulunur.
        private void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            // customBulkDevice'ın ve canDevice'ın geçerli (null olmayan) ve bir handle'a sahip olup olmadığını kontrol eder.
            bool customBulkOk = customBulkDevice != null && customBulkDevice.DeviceHandle != IntPtr.Zero;
            bool canDevOk = canDevice != null && canDevice.DeviceHandle != IntPtr.Zero;

            // Eğer herhangi biri hazır değilse
            if (!customBulkOk || !canDevOk)
            {
                LogMessage("Device Check: One or more USB functions (Bulk/CAN) not ready. Attempting to reconfigure...", Color.Gray);
                AttemptToFindAndConfigureDevice(); // Cihazları bulup yapılandırmayı yeniden dener.
            }
        }

        // Bir USB cihazı sisteme takıldığında CyUSB tarafından tetiklenir.
        // Olayı loglar ve kısa bir gecikmeden sonra cihazları yeniden yapılandırmaya çalışır.
        private void USBDeviceAttached(object sender, EventArgs e)
        {
            var devEventArgs = e as CyUSB.USBEventArgs; // Olay argümanlarını CyUSB.USBEventArgs tipine dönüştürür.
            LogMessage($"USB Device Attached Event: FriendlyName='{devEventArgs?.FriendlyName}', Path='{devEventArgs?.Device?.Path}'", statusOkColor);
            // Cihazın tam olarak tanınması ve sürücülerinin yüklenmesi için kısa bir gecikme ekler.
            Task.Delay(750).ContinueWith(_ =>
            {
                // Form hala geçerliyse ve dispose edilmemişse, UI thread'inde yapılandırmayı dener.
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(AttemptToFindAndConfigureDevice));
                }
            });
        }

        // Bir USB cihazı sistemden çıkarıldığında CyUSB tarafından tetiklenir.
        // Olayı loglar, ilgili USB fonksiyonunu (Custom Bulk veya CAN) devre dışı bırakır,
        // kaynakları temizler ve durumu günceller.
        private void USBDeviceRemoved(object sender, EventArgs e)
        {
            var devEventArgs = e as CyUSB.USBEventArgs; // Olay argümanlarını CyUSB.USBEventArgs tipine dönüştürür.
            string removedFriendlyName = devEventArgs?.FriendlyName ?? "Unknown"; // Çıkarılan cihazın adını alır, yoksa "Unknown" der.
            LogMessage($"USB Device Removed Event: FriendlyName='{removedFriendlyName}'", statusErrorColor);

            bool customBulkWasActive = customBulkDevice != null; // Custom Bulk fonksiyonu daha önce aktif miydi?
            bool canWasActive = canDevice != null; // CAN fonksiyonu daha önce aktif miydi?

            // Çıkarılan cihazın "Custom Bulk" arayüzü olup olmadığını kontrol eder.
            // FriendlyName veya Device Path üzerinden eşleşme arar.
            if (customBulkDevice != null && (devEventArgs == null || customBulkDevice.Path == devEventArgs.Device?.Path || removedFriendlyName.IndexOf(CUSTOM_BULK_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (isUsbEchoRunning) StopUsbEchoMode(); // Eğer USB echo çalışıyorsa durdurur.
                customBulkDevice = null; // Cihaz referansını null yapar.
                customOutEndpoint = null; customInEndpoint = null; // Endpoint referanslarını null yapar.
                outEndpoint = null; inEndpoint = null; // Genel endpoint referanslarını null yapar.
                LogMessage($"Custom Bulk Function '{removedFriendlyName}' disconnected.", statusErrorColor);
            }

            // Çıkarılan cihazın "CAN" arayüzü olup olmadığını kontrol eder.
            // FriendlyName veya Device Path üzerinden eşleşme arar.
            if (canDevice != null && (devEventArgs == null || canDevice.Path == devEventArgs.Device?.Path || removedFriendlyName.IndexOf(CAN_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _canHandler?.StopListening(); // CAN dinlemeyi durdurur.
                _canHandler?.InitializeDevice(null, null, null); // CanHandler'daki cihaz referanslarını temizler.
                canDevice = null; // Cihaz referansını null yapar.
                canOutEndpoint = null; canInEndpoint = null; // Endpoint referanslarını null yapar.
                LogMessage($"CAN Function '{removedFriendlyName}' disconnected.", statusErrorColor);
            }

            // Durum çubuğunu günceller.
            if (customBulkDevice == null && canDevice == null && (customBulkWasActive || canWasActive)) // Her iki fonksiyon da koptuysa
            {
                UpdateStatus("All USB functions disconnected.", statusErrorColor);
            }
            else if (customBulkDevice == null && customBulkWasActive) // Sadece Custom Bulk koptuysa
            {
                UpdateStatus("Custom Bulk USB function disconnected.", statusErrorColor);
            }
            else if (canDevice == null && canWasActive) // Sadece CAN koptuysa
            {
                UpdateStatus("CAN USB function disconnected.", statusErrorColor);
            }
        }


        // Bağlı USB cihazlarını tarar ve belirtilen VID/PID ile FriendlyName'e uyan
        // "Custom Bulk" ve "CAN" arayüzlerini/cihazlarını bulup yapılandırmaya çalışır.
        // Her arayüz için doğru endpoint'leri (EP1/EP82 ve EP7/EP86) bulup atar.
        private void AttemptToFindAndConfigureDevice()
        {
            // Önce mevcut referansları temizler, özellikle DeviceHandle'ı sıfır olan (bağlantısı kopmuş) cihazları.
            if (customBulkDevice != null && customBulkDevice.DeviceHandle == IntPtr.Zero) customBulkDevice = null;
            if (canDevice != null && canDevice.DeviceHandle == IntPtr.Zero) canDevice = null;

            // Eğer cihaz referansları null ise, onlara ait endpointleri ve ilgili handler'ları da sıfırlar.
            if (customBulkDevice == null) { customOutEndpoint = null; customInEndpoint = null; outEndpoint = null; inEndpoint = null; }
            if (canDevice == null) { canOutEndpoint = null; canInEndpoint = null; _canHandler?.InitializeDevice(null, null, null); }


            bool customConfigured = false; // Custom Bulk arayüzünün başarıyla yapılandırılıp yapılandırılmadığını izler.
            bool canConfigured = false;    // CAN arayüzünün başarıyla yapılandırılıp yapılandırılmadığını izler.

            try
            {
                usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB); // USB cihaz listesini her zaman günceller.

                // Belirtilen VID ve PID'ye uyan tüm cihazları filtreler.
                List<CyUSBDevice> matchingDevices = usbDevices.Cast<CyUSBDevice>()
                                                    .Where(d => d.VendorID == USB_VID && d.ProductID == USB_PID)
                                                    .ToList();

                if (matchingDevices.Count == 0) // Eşleşen cihaz bulunamazsa
                {
                    UpdateStatus("USB Device (VID/PID match) not found.", statusErrorColor);
                    LogMessage("No PSoC devices found.", errorLogColor);
                    _canHandler?.StopListening(); // CAN dinlemesini durdurur (emin olmak için).
                    return;
                }

                LogMessage($"Found {matchingDevices.Count} device(s) with matching VID/PID.", Color.LightBlue);

                // Eşleşen her bir cihazı (veya arayüzü) kontrol eder.
                foreach (CyUSBDevice dev in matchingDevices)
                {
                    LogMessage($"Device: {dev.FriendlyName}", Color.Gray);
                    if (string.IsNullOrEmpty(dev.FriendlyName)) continue; // FriendlyName yoksa bu arayüzü atlar.

                    // CAN Arayüzünü/Cihazını Bulma ve Yapılandırma
                    // Eğer canDevice henüz atanmamışsa ve cihazın FriendlyName'i CAN_FRIENDLY_NAME_PART içeriyorsa
                    if (canDevice == null && dev.FriendlyName.IndexOf(CAN_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        canDevice = dev; // Bu cihazı canDevice olarak atar.
                        LogMessage($"CAN Device Candidate: {canDevice.FriendlyName}", canRxLogColor);
                        canOutEndpoint = null; canInEndpoint = null; // Endpoint'leri atamadan önce sıfırlar.

                        // Bu cihazın endpoint'lerini tarar.
                        for (int i = 0; i < canDevice.EndPointCount; i++)
                        {
                            // Endpoint bir CyBulkEndPoint ise ve tipi Bulk (Attributes == 2) ise
                            if (canDevice.EndPoints[i] is CyBulkEndPoint ep && ep.Attributes == 2)
                            {
                                // Gelen (IN) endpoint ve adresi 0x86 ise canInEndpoint'e atar.
                                if (ep.bIn && ep.Address == 0x86) canInEndpoint = ep;
                                // Giden (OUT) endpoint ve adresi 0x07 ise canOutEndpoint'e atar.
                                else if (!ep.bIn && ep.Address == 0x07) canOutEndpoint = ep;
                            }
                        }
                        // Eğer hem IN hem de OUT CAN endpoint'leri bulunduysa
                        if (canInEndpoint != null && canOutEndpoint != null)
                        {
                            LogMessage($"CAN Endpoints (EP:{canOutEndpoint.Address:X2} OUT / EP:{canInEndpoint.Address:X2} IN) on '{canDevice.FriendlyName}' configured.", canRxLogColor);
                            _canHandler.InitializeDevice(canDevice, canInEndpoint, canOutEndpoint); // CanHandler'ı bu cihaz ve endpoint'lerle başlatır.
                            _canHandler.StartListening(); // CAN mesajlarını dinlemeye başlar.
                            canConfigured = true; // CAN yapılandırması başarılı.
                        }
                        else
                        {
                            LogMessage($"CAN device '{canDevice.FriendlyName}' found, but required Endpoints (0x07/0x86) missing or not Bulk.", errorLogColor);
                            canDevice = null; // Başarısız olduysa canDevice'ı sıfırlar.
                        }
                    }
                    // Custom Bulk Arayüzünü/Cihazını Bulma ve Yapılandırma
                    // Eğer customBulkDevice henüz atanmamışsa ve cihazın FriendlyName'i CUSTOM_BULK_FRIENDLY_NAME_PART içeriyorsa
                    else if (customBulkDevice == null && dev.FriendlyName.IndexOf(CUSTOM_BULK_FRIENDLY_NAME_PART, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        customBulkDevice = dev; // Bu cihazı customBulkDevice olarak atar.
                        LogMessage($"Custom Bulk Device Candidate: {customBulkDevice.FriendlyName}", Color.DarkSlateBlue);
                        customOutEndpoint = null; customInEndpoint = null; // Endpoint'leri atamadan önce sıfırlar.

                        // Bu cihazın endpoint'lerini tarar.
                        for (int i = 0; i < customBulkDevice.EndPointCount; i++)
                        {
                            // Endpoint bir CyBulkEndPoint ise ve tipi Bulk (Attributes == 2) ise
                            if (customBulkDevice.EndPoints[i] is CyBulkEndPoint ep && ep.Attributes == 2)
                            {
                                // Gelen (IN) endpoint ve adresi 0x82 ise customInEndpoint'e atar.
                                if (ep.bIn && ep.Address == 0x82) customInEndpoint = ep;
                                // Giden (OUT) endpoint ve adresi 0x01 ise customOutEndpoint'e atar.
                                else if (!ep.bIn && ep.Address == 0x01) customOutEndpoint = ep;
                            }
                        }
                        // Eğer hem IN hem de OUT Custom Bulk endpoint'leri bulunduysa
                        if (customOutEndpoint != null && customInEndpoint != null)
                        {
                            outEndpoint = customOutEndpoint; // Genel amaçlı outEndpoint'e atar.
                            inEndpoint = customInEndpoint;   // Genel amaçlı inEndpoint'e atar.
                            outEndpoint.TimeOut = 1000; // Zaman aşımı süresini ayarlar.
                            inEndpoint.TimeOut = 1000;  // Zaman aşımı süresini ayarlar.
                            LogMessage($"Custom Bulk Endpoints (EP:{customOutEndpoint.Address:X2} OUT / EP:{customInEndpoint.Address:X2} IN) on '{customBulkDevice.FriendlyName}' configured.", Color.DarkSlateBlue);
                            // Eğer echo modları çalışmıyorsa, versiyon sorgusu gönderir.
                            if (!isUsbEchoRunning && !isUartEchoRunning) SendVersionQueryOverCustomEP();
                            customConfigured = true; // Custom Bulk yapılandırması başarılı.
                        }
                        else
                        {
                            LogMessage($"Custom Bulk device '{customBulkDevice.FriendlyName}' found, but required Endpoints (0x01/0x82) missing or not Bulk.", errorLogColor);
                            customBulkDevice = null; // Başarısız olduysa customBulkDevice'ı sıfırlar.
                        }
                    }
                } // End foreach dev

                // Yapılandırma sonuçlarına göre genel durumu günceller.
                if (customConfigured && canConfigured) UpdateStatus("All USB functions (Custom Bulk & CAN) connected.", statusOkColor);
                else if (customConfigured) UpdateStatus("Custom Bulk connected. CAN not found/configured.", statusWarnColor);
                else if (canConfigured) UpdateStatus("CAN connected. Custom Bulk not found/configured.", statusWarnColor);
                else // Hiçbiri yapılandırılamadıysa
                {
                    UpdateStatus("Required USB functions not found/configured.", statusErrorColor);
                    LogMessage("Could not configure either Custom Bulk or CAN interfaces from found devices.", errorLogColor);
                }

            }
            catch (Exception ex) // Cihaz arama/yapılandırma sırasında bir hata oluşursa
            {
                LogMessage($"Error during USB device search/configuration: {ex.Message}", errorLogColor);
                UpdateStatus("Error configuring USB devices.", statusErrorColor);
                // Hata durumunda tüm referansları null yapar ve işlemleri durdurur.
                customBulkDevice = null; canDevice = null;
                customOutEndpoint = null; customInEndpoint = null;
                canOutEndpoint = null; canInEndpoint = null;
                outEndpoint = null; inEndpoint = null;
                _canHandler.StopListening();
                _canHandler.InitializeDevice(null, null, null);
            }
        }
        #endregion

        #region USB Custom Packet Communication 
        // "Custom Bulk" arayüzü üzerinden PSoC cihazına versiyon sorgu komutu gönderir.
        // Bu fonksiyon, özellikle customBulkDevice ve ona ait endpoint'leri kullanır.
        private void SendVersionQueryOverCustomEP()
        {
            // Custom Bulk cihazı veya endpoint'leri hazır değilse işlem yapmaz.
            if (customBulkDevice == null || customOutEndpoint == null || customInEndpoint == null)
            {
                LogMessage("Cannot send version query: Custom Bulk USB device/endpoints not ready.", statusWarnColor);
                return;
            }
            try
            {
                // Versiyon komutu için bir UsbPacket oluşturur.
                var packet = new UsbPacket { CommandId = UsbPacket.CMD_VERSION, DataLength = 0 };
                // SendUsbPacket fonksiyonunu kullanarak paketi gönderir ve yanıtı alır.
                // Bu çağrıda SendUsbPacket, outEndpoint ve inEndpoint'i (customOutEndpoint/customInEndpoint'e eşitlenmiş olmalı) kullanacaktır.
                var response = SendUsbPacket(packet);
                if (response != null) // Yanıt başarıyla alındıysa
                {
                    LogMessage($"Version query successful (via Custom EP). Response: {response.ParseContent()}");
                }
                else // Yanıt alınamadıysa veya hata oluştuysa
                {
                    LogMessage("Version query (via Custom EP) failed or no response.", statusWarnColor);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Version query error (Custom EP): {ex.Message}", errorLogColor);
            }
        }

        // Genel bir UsbPacket'i, yapılandırılmış `outEndpoint` üzerinden gönderir ve `inEndpoint` üzerinden yanıtını alır.
        // Bu fonksiyon `customBulkDevice`'a ait olan `outEndpoint` ve `inEndpoint`'i kullanır.
        // Gönderme ve alma sürelerini, hızlarını loglar.
        private UsbPacket SendUsbPacket(UsbPacket packet)
        {
            // customBulkDevice (ve dolayısıyla outEndpoint/inEndpoint) hazır değilse null döner.
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                LogMessage("USB Send Error: Device not ready.", errorLogColor);
                return null;
            }

            byte[] outData = packet.ToByteArray(); // Gönderilecek paketi byte dizisine çevirir.
            byte[] inData = new byte[inEndpoint.MaxPktSize]; // Gelen veri için endpoint'in maksimum paket boyutunda bir buffer oluşturur.
            int outLen = outData.Length; // Gönderilecek veri uzunluğu.
            int inLen = inData.Length;   // Alınacak veri için buffer uzunluğu (XferData tarafından güncellenecek).
            UsbPacket response = null;   // Alınan yanıtı tutacak UsbPacket nesnesi.

            try
            {
                // Veri gönderme işlemini zamanlar.
                var swSend = Stopwatch.StartNew();
                bool okS = outEndpoint.XferData(ref outData, ref outLen); // Veriyi OUT endpoint'e gönderir.
                swSend.Stop();
                lastUsbSendTime = swSend.Elapsed.TotalMilliseconds; // Gönderme süresini kaydeder.

                if (!okS) // Gönderme başarısızsa
                {
                    LogMessage("USB Error: Data send failed. Code: " + outEndpoint.LastError, errorLogColor);
                    return null;
                }

                // Gönderme hızını Mbps cinsinden hesaplar.
                double sendMbps = (lastUsbSendTime > 0) ? (outLen * 8.0 / 1_000_000.0) / (lastUsbSendTime / 1000.0) : 0;
                // USB echo modu çalışmıyorsa veya belirli aralıklarla loglama yapar.
                if (!isUsbEchoRunning || usbEchoPacketCount % 50 == 0 || usbEchoPacketCount == 1)
                {
                    LogMessage($"USB Sent: {outLen} bytes -> {lastUsbSendTime:F3} ms -> {sendMbps:F2} Mbps", isUsbEchoRunning ? (Color?)usbEchoLogColor : null);
                }

                // Veri alma işlemini zamanlar.
                var swR = Stopwatch.StartNew();
                bool okR = inEndpoint.XferData(ref inData, ref inLen); // Veriyi IN endpoint'ten alır. inLen, alınan gerçek byte sayısıyla güncellenir.
                swR.Stop();
                lastUsbResponseTime = swR.Elapsed.TotalMilliseconds; // Alma süresini kaydeder.

                if (!okR) // Alma başarısızsa
                {
                    LogMessage("USB Error: Response receive failed. Code: " + inEndpoint.LastError, errorLogColor);
                    return null;
                }
                // Echo komutu için 0 byte alındıysa uyarı loglar (bazı cihazlar ACK için 0 byte dönebilir).
                if (inLen == 0 && packet.CommandId == UsbPacket.CMD_ECHO_STRING)
                {
                    LogMessage("USB Warning: Received 0 bytes for an Echo command.", isUsbEchoRunning ? (Color?)usbEchoLogColor : (Color?)Color.Orange);
                }

                // Alma hızını Mbps cinsinden hesaplar.
                double recvMbps = (lastUsbResponseTime > 0 && inLen > 0) ? (inLen * 8.0 / 1_000_000.0) / (lastUsbResponseTime / 1000.0) : 0;
                // USB echo modu çalışmıyorsa veya belirli aralıklarla loglama yapar.
                if (!isUsbEchoRunning || usbEchoPacketCount % 50 == 0 || usbEchoPacketCount == 1)
                {
                    LogMessage($"USB Received: {inLen} bytes -> {lastUsbResponseTime:F3} ms -> {recvMbps:F2} Mbps", isUsbEchoRunning ? (Color?)usbEchoLogColor : null);
                }

                // Alınan gerçek veriyi yeni bir byte dizisine kopyalar.
                byte[] actualInData = new byte[inLen];
                Array.Copy(inData, actualInData, inLen);
                response = UsbPacket.FromByteArray(actualInData); // Byte dizisini UsbPacket nesnesine çevirir.
            }
            catch (Exception ex) // İletişim sırasında bir hata oluşursa
            {
                LogMessage($"USB communication error: {ex.Message}", errorLogColor);
                if (ex.InnerException != null) LogMessage($"Inner Exception: {ex.InnerException.Message}", errorLogColor);
            }

            return response; // Alınan UsbPacket'i veya hata durumunda null'ı döner.
        }

        // "Send Command" (veya echo modlarında "Start/Stop Echo") butonuna tıklandığında çağrılır.
        // Seçili komuta göre işlem yapar: echo modunu başlatır/durdurur veya özel bir USB komutu gönderir.
        // Komut gönderimleri `customBulkDevice` üzerinden yapılır.
        private void BtnSendCustomCommand_Click(object sender, EventArgs e)
        {
            // cmbCommands'dan seçili öğeyi alır, eğer CommandItem değilse çıkar.
            if (!(cmbCommands.SelectedItem is CommandItem selectedCmdItem)) return;

            // Eğer seçili komut "USB String Echo" ise
            if (selectedCmdItem.CommandId == UsbPacket.CMD_ECHO_STRING)
            {
                if (isUsbEchoRunning) StopUsbEchoMode(); else StartUsbEchoMode(); // USB echo çalışıyorsa durdurur, değilse başlatır.
                return; // Diğer işlemlere devam etmez.
            }
            // Eğer seçili komut "UART String Echo" ise
            if (selectedCmdItem.CommandId == UsbPacket.CMD_UART_ECHO_STRING)
            {
                if (isUartEchoRunning) StopUartEchoMode(); else StartUartEchoMode(); // UART echo çalışıyorsa durdurur, değilse başlatır.
                return; // Diğer işlemlere devam etmez.
            }

            // Normal komut gönderme işlemleri için Custom Bulk USB cihazının hazır olup olmadığını kontrol eder.
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("Custom Bulk USB device not connected or endpoints not ready!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Eğer herhangi bir echo modu çalışıyorsa, yeni komut göndermeden önce durdurur.
            if (isUsbEchoRunning) StopUsbEchoMode();
            if (isUartEchoRunning) StopUartEchoMode();

            // Gönderilecek UsbPacket'i oluşturur, komut ID'sini atar.
            var packetToSend = new UsbPacket { CommandId = selectedCmdItem.CommandId };
            string dataInput = txtData.Text.Trim(); // Veri giriş alanındaki metni alır.

            // Eğer komut "Write" ise, girilen hex veriyi parse edip pakete ekler.
            if (selectedCmdItem.CommandId == UsbPacket.CMD_WRITE)
            {
                if (!TryParseHexByte(dataInput, out byte writeVal)) return; // Hex byte parse edilemezse çıkar.
                packetToSend.Data[0] = writeVal; // Parse edilen değeri paketin data alanına yazar.
                packetToSend.DataLength = 1;     // Veri uzunluğunu 1 olarak ayarlar.
            }

            LogMessage($"----- Sending USB Custom Command: {selectedCmdItem.Name} -----", Color.Indigo);
            UsbPacket response = SendUsbPacket(packetToSend); // Paketi gönderir ve yanıtı alır.

            if (response != null) // Yanıt alındıysa
            {
                LogMessage($"Response to {selectedCmdItem.Name}: {response.ParseContent()}"); // Yanıtı loglar.
                UpdateStatus($"Command '{selectedCmdItem.Name}' sent. Response received.", statusOkColor); // Durumu günceller.
            }
            else // Yanıt alınamadıysa veya hata oluştuysa
            {
                LogMessage($"No response or error for command {selectedCmdItem.Name}.", errorLogColor);
                UpdateStatus($"Error sending/receiving for '{selectedCmdItem.Name}'.", statusErrorColor);
            }
            LogMessage("----------------------------------------------------", Color.Indigo);
        }

        // Verilen string girdisini tek bir hexadecimal byte değerine dönüştürmeye çalışır.
        // Başarılı olursa true ve parse edilen değeri `value` ile döner, aksi halde false döner ve hata mesajı gösterir.
        private bool TryParseHexByte(string input, out byte value)
        {
            value = 0; // Başlangıç değeri.
            if (string.IsNullOrWhiteSpace(input)) // Girdi boşsa hata.
            {
                MessageBox.Show("Hex input cannot be empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            // Girdi "0x" ile başlıyorsa kaldırır.
            input = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input.Substring(2) : input;
            // Girdi 2 karakterden uzunsa veya byte'a parse edilemiyorsa hata.
            if (input.Length > 2 || !byte.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                MessageBox.Show("Invalid hex byte value! Example: A5 or 0xA5", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true; // Başarılı parse.
        }
        #endregion

        #region USB Echo Logic (Detaylı Loglama)
        // usbEchoTimer her tetiklendiğinde (Tick) çağrılır.
        // Eğer USB echo modu aktifse, SendUsbEchoPacket fonksiyonunu çağırarak bir echo paketi gönderir.
        private void UsbEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUsbEchoRunning) return; // USB echo modu aktif değilse bir şey yapmaz.
            SendUsbEchoPacket(); // USB echo paketi gönderir.
        }

        // USB echo test modunu başlatır.
        // İstatistikleri sıfırlar, buton metnini günceller ve echo zamanlayıcısını başlatır.
        // Bu mod, `customBulkDevice` ve ona ait endpoint'leri kullanır.
        private void StartUsbEchoMode()
        {
            if (isUsbEchoRunning) return; // Zaten çalışıyorsa bir şey yapmaz.
            // Custom Bulk USB cihazı hazır değilse hata mesajı gösterir ve modu başlatmaz.
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                MessageBox.Show("Custom Bulk USB device not ready for USB Echo!", "USB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            isUsbEchoRunning = true; // USB echo modunu aktif olarak işaretler.
            // İstatistikleri sıfırlar.
            usbEchoPacketCount = 0;
            totalUsbSendTime = 0; totalUsbResponseTime = 0;
            minUsbSendTime = double.MaxValue; maxUsbSendTime = 0;
            minUsbResponseTime = double.MaxValue; maxUsbResponseTime = 0;
            UpdateButtonText(); // Buton metnini "Stop USB Echo" olarak günceller.
            LogMessage("----- USB Echo Mode STARTED (Custom Bulk) -----", usbEchoLogColor);
            usbEchoTimer.Start(); // USB echo zamanlayıcısını başlatır.
        }

        // USB echo test modunu durdurur.
        // Zamanlayıcıyı durdurur, buton metnini günceller ve test istatistiklerini loglar.
        private void StopUsbEchoMode()
        {
            if (!isUsbEchoRunning) return; // Zaten durmuşsa bir şey yapmaz.
            usbEchoTimer.Stop(); // USB echo zamanlayıcısını durdurur.
            isUsbEchoRunning = false; // USB echo modunu pasif olarak işaretler.
            UpdateButtonText(); // Buton metnini "Start USB Echo" olarak günceller.
            LogMessage("----- USB Echo Mode STOPPED (Custom Bulk) -----", usbEchoLogColor);
            // Eğer en az bir paket gönderildiyse, istatistikleri loglar.
            if (usbEchoPacketCount > 0)
            {
                LogMessage($"Total USB Echo Packets: {usbEchoPacketCount}", usbEchoLogColor);
                LogMessage($"  Avg Send: {totalUsbSendTime / usbEchoPacketCount:F2}ms | Min: {minUsbSendTime:F2}ms | Max: {maxUsbSendTime:F2}ms", usbEchoLogColor);
                LogMessage($"  Avg Recv: {totalUsbResponseTime / usbEchoPacketCount:F2}ms | Min: {minUsbResponseTime:F2}ms | Max: {maxUsbResponseTime:F2}ms", usbEchoLogColor);
            }
        }

        // Bir USB echo paketi gönderir, yanıtı alır ve sonuçları (zamanlama, veri eşleşmesi) loglar.
        // Bu fonksiyon `customBulkDevice` ve ona ait endpoint'leri kullanır.
        // İstatistikleri günceller (min/max/ortalama süreler).
        private void SendUsbEchoPacket()
        {
            // Custom Bulk USB cihazı bağlantısı koptuysa modu durdurur.
            if (customBulkDevice == null || outEndpoint == null || inEndpoint == null)
            {
                StopUsbEchoMode();
                LogMessage("USB Echo: Custom Bulk Device lost. Stopping.", errorLogColor);
                return;
            }

            // Gönderilecek echo verisini txtData'dan alır, boşsa varsayılan bir metin kullanır.
            string echoDataStr = string.IsNullOrWhiteSpace(txtData.Text) ? "Default USB Echo Data" : txtData.Text.Trim();
            var packet = new UsbPacket { CommandId = UsbPacket.CMD_ECHO_STRING };
            packet.SetDataFromText(echoDataStr); // Metni paket verisine dönüştürür.

            UsbPacket resp = SendUsbPacket(packet); // USB paketini gönderir ve yanıtı alır.

            if (resp != null) // Yanıt başarıyla alındıysa
            {
                usbEchoPacketCount++; // Paket sayacını artırır.
                // Toplam ve min/max gönderme/alma sürelerini günceller.
                totalUsbSendTime += lastUsbSendTime;
                totalUsbResponseTime += lastUsbResponseTime;
                minUsbSendTime = Math.Min(minUsbSendTime, lastUsbSendTime);
                maxUsbSendTime = Math.Max(maxUsbSendTime, lastUsbSendTime);
                minUsbResponseTime = Math.Min(minUsbResponseTime, lastUsbResponseTime);
                maxUsbResponseTime = Math.Max(maxUsbResponseTime, lastUsbResponseTime);

                // Durum çubuğunu güncel echo bilgileriyle günceller.
                if (isUsbEchoRunning)
                    UpdateStatus($"USB Echo #{usbEchoPacketCount}: Send {lastUsbSendTime:F1}ms | Recv {lastUsbResponseTime:F1}ms", usbEchoLogColor);

                string receivedEchoStr = resp.GetDataAsText(); // Alınan paketteki veriyi metne çevirir.
                bool dataMatch = echoDataStr == receivedEchoStr; // Gönderilen ve alınan verinin eşleşip eşleşmediğini kontrol eder.

                if (!dataMatch) // Veri eşleşmiyorsa her zaman loglar.
                {
                    LogMessage($"USB BULK Echo Mismatch: Sent=\"{echoDataStr}\" | Recv=\"{receivedEchoStr}\"", errorLogColor);
                }
                // Veri eşleşiyorsa ve belirli aralıklarla (her 10 pakette bir veya ilk paket) detaylı loglama yapar.
                else if (usbEchoPacketCount % 10 == 0 || usbEchoPacketCount == 1)
                {
                    int dataBytesLen = packet.ToByteArray().Length; // Gönderilen paketin byte cinsinden uzunluğu.
                    double sendMbps = (lastUsbSendTime > 0) ? (dataBytesLen * 8.0 / (lastUsbSendTime / 1000.0) / 1000000.0) : 0; // Gönderme hızı.
                    LogMessage($"BULK Echo #{usbEchoPacketCount} [USB] TX: {dataBytesLen} bytes in {lastUsbSendTime:F2}ms ({sendMbps:F2} Mbps)", usbEchoLogColor);

                    int respBytesLen = resp.ToByteArray().Length; // Alınan paketin byte cinsinden uzunluğu.
                    double recvMbps = (lastUsbResponseTime > 0) ? (respBytesLen * 8.0 / (lastUsbResponseTime / 1000.0) / 1000000.0) : 0; // Alma hızı.
                    LogMessage($"BULK Echo #{usbEchoPacketCount} [USB] RX: {respBytesLen} bytes in {lastUsbResponseTime:F2}ms ({recvMbps:F2} Mbps) - {(dataMatch ? "Match" : "Mismatch")}", usbEchoLogColor);

                    // İstatistikleri loglar.
                    LogMessage($"BULK Echo #{usbEchoPacketCount} [USB] Stats:", usbEchoLogColor);
                    LogMessage($"  TX: Avg {totalUsbSendTime / usbEchoPacketCount:F2}ms | Min {minUsbSendTime:F2}ms | Max {maxUsbSendTime:F2}ms", usbEchoLogColor);
                    LogMessage($"  RX: Avg {totalUsbResponseTime / usbEchoPacketCount:F2}ms | Min {minUsbResponseTime:F2}ms | Max {maxUsbResponseTime:F2}ms", usbEchoLogColor);
                }
            }
            else // SendUsbPacket null döndürdüyse (hata oluştuysa)
            {
                LogMessage("USB Echo: SendUsbPacket returned null. Stopping.", errorLogColor);
                StopUsbEchoMode(); // USB echo modunu durdurur.
            }
        }
        #endregion

        #region UART Communication & Echo (Detaylı Loglama)
        // UART (seri port) iletişimini ayarlar.
        // Kullanılabilir COM portlarını bulur, seçili portu ve baud hızını yapılandırır.
        private void SetupUartCommunication()
        {
            try
            {
                string[] portNames = SerialPort.GetPortNames(); // Sistemdeki mevcut COM portlarını listeler.
                // Eğer varsayılan COM portu (selectedComPort) listede yoksa ve başka portlar varsa, ilk kullanılabilir portu seçer.
                if (!portNames.Contains(selectedComPort) && portNames.Length > 0)
                {
                    LogMessage($"UART: Default port {selectedComPort} not found. Available: {string.Join(", ", portNames)}. Using {portNames[0]}.", statusWarnColor);
                    selectedComPort = portNames[0];
                }
                else if (portNames.Length == 0) // Hiç COM portu yoksa
                {
                    LogMessage("UART: No COM ports available.", statusErrorColor);
                    UpdateStatus("UART: No COM ports.", statusErrorColor);
                    return;
                }

                // SerialPort nesnesini belirtilen port, baud hızı, parite, data bitleri ve stop bitleri ile oluşturur.
                uartPort = new SerialPort(selectedComPort, UART_BAUD_RATE, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200, // Okuma zaman aşımı 200ms.
                    WriteTimeout = 200 // Yazma zaman aşımı 200ms.
                };
                LogMessage($"UART: Configured for {selectedComPort} at {UART_BAUD_RATE} baud.", Color.CadetBlue);
            }
            catch (Exception ex) // UART ayarlanırken bir hata oluşursa
            {
                LogMessage($"UART Setup Error: {ex.Message}", errorLogColor);
            }
        }

        // uartEchoTimer her tetiklendiğinde (Tick) çağrılır.
        // Eğer UART echo modu aktifse, SendUartEchoPacket fonksiyonunu çağırarak bir echo paketi gönderir.
        private void UartEchoTimer_Tick(object sender, EventArgs e)
        {
            if (!isUartEchoRunning) return; // UART echo modu aktif değilse bir şey yapmaz.
            SendUartEchoPacket(); // UART echo paketi gönderir.
        }

        // UART echo test modunu başlatır.
        // Gerekirse seri portu ayarlar ve açar, istatistikleri sıfırlar, buton metnini günceller ve echo zamanlayıcısını başlatır.
        private void StartUartEchoMode()
        {
            if (isUartEchoRunning) return; // Zaten çalışıyorsa bir şey yapmaz.
            if (uartPort == null) { SetupUartCommunication(); } // uartPort null ise yeniden ayarlar.
            if (uartPort == null) { MessageBox.Show("UART port not available for Echo!", "UART Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; } // Hala null ise hata verir.

            try { if (!uartPort.IsOpen) uartPort.Open(); } // Port kapalıysa açmaya çalışır.
            catch (Exception ex) { LogMessage($"UART Echo: Error opening port {uartPort.PortName}: {ex.Message}", errorLogColor); return; } // Port açma hatası.

            isUartEchoRunning = true; // UART echo modunu aktif olarak işaretler.
            // İstatistikleri sıfırlar.
            uartEchoPacketCount = 0;
            totalUartSendTime = 0; totalUartResponseTime = 0;
            minUartSendTime = double.MaxValue; maxUartSendTime = 0;
            minUartResponseTime = double.MaxValue; maxUartResponseTime = 0;
            UpdateButtonText(); // Buton metnini "Stop UART Echo" olarak günceller.
            LogMessage($"----- UART Echo Mode STARTED on {uartPort.PortName} -----", uartEchoLogColor);
            uartEchoTimer.Start(); // UART echo zamanlayıcısını başlatır.
        }

        // UART echo test modunu durdurur.
        // Zamanlayıcıyı durdurur, buton metnini günceller ve test istatistiklerini loglar.
        private void StopUartEchoMode()
        {
            if (!isUartEchoRunning) return; // Zaten durmuşsa bir şey yapmaz.
            uartEchoTimer.Stop(); // UART echo zamanlayıcısını durdurur.
            isUartEchoRunning = false; // UART echo modunu pasif olarak işaretler.
            UpdateButtonText(); // Buton metnini "Start UART Echo" olarak günceller.
            LogMessage($"----- UART Echo Mode STOPPED on {uartPort.PortName} -----", uartEchoLogColor);
            // Eğer en az bir paket gönderildiyse, istatistikleri loglar.
            if (uartEchoPacketCount > 0)
            {
                LogMessage($"Total UART Echo Packets: {uartEchoPacketCount}", uartEchoLogColor);
                LogMessage($"  Avg Send: {totalUartSendTime / uartEchoPacketCount:F2}ms | Min: {minUartSendTime:F2}ms | Max: {maxUartSendTime:F2}ms", uartEchoLogColor);
                LogMessage($"  Avg Recv: {totalUartResponseTime / uartEchoPacketCount:F2}ms | Min: {minUartResponseTime:F2}ms | Max: {maxUartResponseTime:F2}ms", uartEchoLogColor);
            }
        }

        // Bir UART echo paketi gönderir, seri porttan yanıtı okur ve sonuçları (zamanlama, veri eşleşmesi) loglar.
        // İstatistikleri günceller (min/max/ortalama süreler).
        private void SendUartEchoPacket()
        {
            // UART portu null veya kapalıysa modu durdurur.
            if (uartPort == null || !uartPort.IsOpen)
            {
                StopUartEchoMode();
                LogMessage("UART Echo: Port not open. Stopping.", errorLogColor);
                return;
            }

            // Gönderilecek echo verisini txtData'dan alır, boşsa varsayılan bir metin kullanır.
            string dataToSendStr = string.IsNullOrWhiteSpace(txtData.Text) ? "Default UART Echo Data" : txtData.Text.Trim();
            byte[] bytesToSend = Encoding.ASCII.GetBytes(dataToSendStr); // Metni ASCII byte dizisine çevirir.
            byte[] receivedBuffer = new byte[bytesToSend.Length]; // Alınan veriler için gönderilenle aynı boyutta buffer.
            int bytesActuallyRead = 0; // Gerçekte okunan byte sayısı.

            try
            {
                // Veri gönderme işlemini zamanlar.
                var swSend = Stopwatch.StartNew();
                uartPort.Write(bytesToSend, 0, bytesToSend.Length); // Veriyi seri porta yazar.
                swSend.Stop();
                lastUartSendTime = swSend.Elapsed.TotalMilliseconds; // Gönderme süresini kaydeder.
                double uartSendMbps = (lastUartSendTime > 0) ? (bytesToSend.Length * 8.0 / (lastUartSendTime / 1000.0) / 1000000.0) : 0; // Gönderme hızı.

                // Veri alma işlemini zamanlar.
                var swRecv = Stopwatch.StartNew();
                try
                {
                    int totalRead = 0;
                    long startTimeMs = swRecv.ElapsedMilliseconds; // Okumaya başlama zamanı (zaman aşımı kontrolü için).
                    // Beklenen tüm byte'lar okunana kadar veya zaman aşımına uğrayana kadar döngü.
                    while (totalRead < receivedBuffer.Length && (swRecv.ElapsedMilliseconds - startTimeMs) < uartPort.ReadTimeout)
                    {
                        if (uartPort.BytesToRead > 0) // Okunacak byte varsa
                        {
                            int readNow = uartPort.Read(receivedBuffer, totalRead, receivedBuffer.Length - totalRead); // Buffer'a okur.
                            if (readNow == 0) break; // Hiç byte okunmadıysa (beklenmedik durum) döngüden çıkar.
                            totalRead += readNow; // Toplam okunan byte sayısını günceller.
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(5); // Kısa bir süre bekler (CPU'yu meşgul etmemek için).
                        }
                    }
                    bytesActuallyRead = totalRead; // Gerçekte okunan byte sayısını atar.
                }
                catch (TimeoutException) { /* Zaman aşımı oluşursa, bytesActuallyRead değeri zaten doğru olacaktır (ya da 0). */ }

                swRecv.Stop();
                lastUartResponseTime = swRecv.Elapsed.TotalMilliseconds; // Alma süresini kaydeder.
                string receivedStr = Encoding.ASCII.GetString(receivedBuffer, 0, bytesActuallyRead); // Okunan byte'ları ASCII metne çevirir.
                double uartRecvMbps = (lastUartResponseTime > 0 && bytesActuallyRead > 0) ? (bytesActuallyRead * 8.0 / (lastUartResponseTime / 1000.0) / 1000000.0) : 0; // Alma hızı.

                // Gönderilen veri ile alınan verinin eşleşip eşleşmediğini kontrol eder.
                bool dataMatch = bytesActuallyRead == bytesToSend.Length && dataToSendStr.Equals(receivedStr);

                if (dataMatch) // Veri eşleşiyorsa
                {
                    uartEchoPacketCount++; // Paket sayacını artırır.
                    // Toplam ve min/max gönderme/alma sürelerini günceller.
                    totalUartSendTime += lastUartSendTime;
                    totalUartResponseTime += lastUartResponseTime;
                    minUartSendTime = Math.Min(minUartSendTime, lastUartSendTime);
                    maxUartSendTime = Math.Max(maxUartSendTime, lastUartSendTime);
                    minUartResponseTime = Math.Min(minUartResponseTime, lastUartResponseTime);
                    maxUartResponseTime = Math.Max(maxUartResponseTime, lastUartResponseTime);

                    // Durum çubuğunu güncel echo bilgileriyle günceller.
                    if (isUartEchoRunning)
                        UpdateStatus($"UART Echo #{uartEchoPacketCount}: Send {lastUartSendTime:F1}ms | Recv {lastUartResponseTime:F1}ms", uartEchoLogColor);

                    // Belirli aralıklarla (her 10 pakette bir veya ilk paket) detaylı loglama yapar.
                    if (uartEchoPacketCount % 10 == 0 || uartEchoPacketCount == 1)
                    {
                        LogMessage($"UART Echo #{uartEchoPacketCount} [UART] TX: {bytesToSend.Length} bytes in {lastUartSendTime:F2}ms ({uartSendMbps:F2} Mbps)", uartEchoLogColor);
                        LogMessage($"UART Echo #{uartEchoPacketCount} [UART] RX: {bytesActuallyRead} bytes in {lastUartResponseTime:F2}ms ({uartRecvMbps:F2} Mbps) - Match", uartEchoLogColor);

                        // İstatistikleri loglar.
                        LogMessage($"UART Echo #{uartEchoPacketCount} [UART] Stats:", uartEchoLogColor);
                        LogMessage($"  TX: Avg {totalUartSendTime / uartEchoPacketCount:F2}ms | Min {minUartSendTime:F2}ms | Max {maxUartSendTime:F2}ms", uartEchoLogColor);
                        LogMessage($"  RX: Avg {totalUartResponseTime / uartEchoPacketCount:F2}ms | Min {minUartResponseTime:F2}ms | Max {maxUartResponseTime:F2}ms", uartEchoLogColor);
                    }
                }
                else // Veri eşleşmiyorsa veya eksik/fazla okunduysa
                {
                    LogMessage($"Echo [UART] Mismatch: Sent=\"{dataToSendStr}\" ({bytesToSend.Length}B) | Recv=\"{receivedStr}\" ({bytesActuallyRead}B)", errorLogColor);
                    if (isUartEchoRunning)
                        UpdateStatus($"UART Echo #{uartEchoPacketCount}: Mismatch/Timeout", errorLogColor);
                }
            }
            catch (Exception ex) // UART iletişimi sırasında bir hata oluşursa
            {
                LogMessage($"UART Echo Error: {ex.Message}. Stopping.", errorLogColor);
                StopUartEchoMode(); // UART echo modunu durdurur.
            }
        }
        #endregion

        #region CAN Interface Logic
        // CanHandler'dan bir CAN mesajı (alınan veya gönderilen) geldiğinde çağrılır.
        // Mesajı UI thread'inde ilgili ListView'a (listViewCanReceive veya listViewCanTransmit) ekler.
        private void HandleCanMessageFromCanHandler(CanMessage message)
        {
            // Formun handle'ı oluşturulmuş ve dispose edilmemişse devam et. (Thread güvenliği için)
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                // UI güncellemelerini UI thread'inde yapmak için BeginInvoke kullanılır.
                this.BeginInvoke(new Action(() =>
                {
                    // Mesajın yönüne (Rx veya Tx) göre hedef ListView'ı ve rengi belirler.
                    ListView targetListView = (message.Direction == "Rx") ? listViewCanReceive : listViewCanTransmit;
                    Color itemColor = (message.Direction == "Rx") ? canRxLogColor : canTxLogColor;
                    AddCanMessageToUiListView(targetListView, message, itemColor); // Mesajı ListView'a ekler.
                }));
            }
        }

        // Verilen CanMessage'ı belirtilen ListView'a ve renkte bir öğe olarak ekler.
        // ListView'daki öğe sayısını sınırlar ve yeni öğenin görünür olmasını sağlar (UI duraklatılmamışsa).
        private void AddCanMessageToUiListView(ListView listView, CanMessage message, Color itemColor)
        {
            // Yeni bir ListViewItem oluşturur, ilk sütun sıra numarasıdır.
            var lvi = new ListViewItem(message.SequenceNumber.ToString());
            // Diğer sütunları (zaman damgası, ID, DLC, veri vb.) ekler.
            lvi.SubItems.Add(message.UiTimestamp.ToString("HH:mm:ss.fff"));
            lvi.SubItems.Add(message.Id.ToString(message.Id > 0x7FF ? "X8" : "X3")); // ID'yi standart veya extended formatında gösterir.
            lvi.SubItems.Add(message.Length.ToString());
            lvi.SubItems.Add(message.DataToHexString());
            lvi.SubItems.Add(message.PSoCTimestamp.ToString());
            lvi.SubItems.Add(message.Properties.ToString("X2"));
            lvi.ForeColor = itemColor; // Öğenin metin rengini ayarlar.
            lvi.Tag = message; // CanMessage nesnesini Tag özelliğine atar (detayları göstermek için).

            listView.Items.Add(lvi); // Öğeyi ListView'a ekler.
            // ListView'daki öğe sayısı 1500'ü geçerse en eski öğeyi siler.
            if (listView.Items.Count > 1500)
            {
                listView.Items.RemoveAt(0);
            }
            // Eğer CAN UI güncellemeleri duraklatılmamışsa, yeni eklenen öğenin görünür olmasını sağlar.
            if (!isCanUiPaused)
            {
                lvi.EnsureVisible();
            }
        }

        // "Send CAN Message" butonuna tıklandığında çağrılır.
        // Kullanıcı arayüzünden CAN ID, DLC ve veri bilgilerini alır, parse eder,
        // bir CanMessage nesnesi oluşturur ve _canHandler aracılığıyla gönderir.
        // Bu fonksiyon, _canHandler'ın `canDevice` ve ona ait endpoint'leri kullanmasını bekler.
        private void BtnSendCanMessageViaHandler_Click(object sender, EventArgs e)
        {
            // _canHandler null ise veya cihazı (canDevice ve endpointleri) hazır değilse hata mesajı gösterir.
            if (_canHandler == null || !_canHandler.IsDeviceReady)
            {
                MessageBox.Show("CAN USB function not ready for sending!", "CAN Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // CAN ID'sini metin kutusundan alır, küçük harfe çevirir ve "0x", "h" gibi ön/son ekleri temizler.
            string idStr = txtCanTransmitId.Text.Trim().ToLower();
            if (idStr.StartsWith("0x")) idStr = idStr.Substring(2);
            if (idStr.EndsWith("h")) idStr = idStr.Substring(0, idStr.Length - 1);

            // Temizlenmiş ID string'ini hexadecimal uint değerine parse etmeye çalışır.
            if (!uint.TryParse(idStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId))
            {
                MessageBox.Show("Invalid CAN ID. Please enter a hex value (e.g., 1A0 or 1234567).", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool isExtended = chkCanExtendedId.Checked; // Extended ID seçili mi?
            // Standart ID (11-bit) 0x7FF'i geçemez. Geçiyorsa kullanıcıyı uyarır ve Extended ID'yi işaretler.
            if (!isExtended && canId > 0x7FF)
            {
                MessageBox.Show("Standard CAN ID cannot exceed 0x7FF. For larger IDs, check 'Extended'.", "Input Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                chkCanExtendedId.Checked = true; // Otomatik olarak Extended ID'ye geçer.
                // isExtended = true; // Bu satır gereksiz, çünkü chkCanExtendedId.Checked zaten true oldu.
            }
            // Extended ID (29-bit) 0x1FFFFFFF'i geçemez.
            else if (isExtended && canId > 0x1FFFFFFF)
            {
                MessageBox.Show("Extended CAN ID cannot exceed 0x1FFFFFFF.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte dlc = (byte)numCanTransmitDlc.Value; // DLC değerini NumericUpDown kontrolünden alır.
            // Veri metnini (txtCanTransmitData) parse edip byte dizisine dönüştürmeye çalışır.
            if (!CanHandler.TryParseHexData(txtCanTransmitData.Text, dlc, out byte[] dataBytes))
            {
                MessageBox.Show($"Invalid data format for DLC {dlc}. Enter {dlc} hex bytes separated by spaces (e.g., 00 AA 11).", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Gönderilecek CanMessage nesnesini oluşturur ve alanlarını doldurur.
            var msgToSend = new CanMessage
            {
                Id = canId,
                Length = dlc,
                Properties = 0, // Gönderirken PSoC'un dolduracağı alanlar sıfır olabilir.
                PSoCTimestamp = 0
            };
            Array.Copy(dataBytes, msgToSend.Data, dataBytes.Length); // Parse edilen veriyi mesajın Data alanına kopyalar.

            // Mesajı CanHandler aracılığıyla gönderir.
            if (!_canHandler.SendCanMessage(msgToSend))
            {
                LogMessage("Failed to send CAN message. Check CAN logs.", errorLogColor); // Gönderme başarısız olursa loglar.
            }
        }
        #endregion

        #region MainForm Load & Closing
        // Form yüklendiğinde (Load olayı) çağrılır.
        // Uygulamanın başladığını ve versiyonunu loglar, başlangıç durumunu ayarlar.
        // USB cihazlarını bulma ve yapılandırma işlemi SetupUsbMonitoring içinde zaten çağrılır.
        private void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Application_Started. Version: " + Application.ProductVersion);
            UpdateStatus("Initializing...", Color.Gray);
            // AttemptToFindAndConfigureDevice() SetupUsbMonitoring tarafından çağrılır, burada tekrar çağırmaya gerek yok.
        }

        // Form kapanırken (FormClosing olayı) çağrılır.
        // Aktif işlemleri (echo modları, CAN dinleme) durdurur ve kullanılan kaynakları (zamanlayıcılar, portlar, USB cihaz listesi) serbest bırakır.
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogMessage("Application_Closing...", Color.Gray);
            if (isUsbEchoRunning) StopUsbEchoMode(); // USB echo çalışıyorsa durdurur.
            if (isUartEchoRunning) StopUartEchoMode(); // UART echo çalışıyorsa durdurur.
            _canHandler?.StopListening(); // CAN dinlemeyi durdurur.

            // Zamanlayıcıları durdurur ve dispose eder.
            deviceCheckTimer?.Stop(); deviceCheckTimer?.Dispose();
            usbEchoTimer?.Dispose();
            uartEchoTimer?.Dispose();
            _canHandler?.Dispose(); // CanHandler'ı dispose eder.
            uartPort?.Close(); uartPort?.Dispose(); // UART portunu kapatır ve dispose eder.
            usbDevices?.Dispose(); // USB cihaz listesini dispose eder.
            // Cihaz referanslarını null yapar.
            customBulkDevice = null;
            canDevice = null;

            LogMessage("Application_Closed.");
        }
        #endregion

        #region Logging & Status Updates
        // txtLog RichTextBox kontrolüne mesajları zaman damgasıyla ve isteğe bağlı renkle loglar.
        // Eğer farklı bir thread'den çağrılırsa, BeginInvoke ile UI thread'inde çalışmasını sağlar.
        private void LogMessage(string message, Color? textColor = null)
        {
            if (txtLog.InvokeRequired) // Eğer bu metod UI thread'i dışından çağrıldıysa
            {
                // LogInternal metodunu UI thread'inde çalıştırmak için BeginInvoke kullanır.
                txtLog.BeginInvoke(new Action(() => LogInternal(message, textColor)));
            }
            else // UI thread'inden çağrıldıysa doğrudan LogInternal'ı çağırır.
            {
                LogInternal(message, textColor);
            }
        }

        // LogMessage tarafından çağrılan, loglama işlemini fiilen yapan iç metot.
        // Mesajı txtLog'a ekler, rengini ayarlar, log boyutunu yönetir ve en sona kaydırır.
        private void LogInternal(string message, Color? textColor)
        {
            if (txtLog.IsDisposed) return; // txtLog dispose edilmişse bir şey yapmaz.
            Color originalColor = txtLog.SelectionColor; // Mevcut seçim rengini saklar.
            txtLog.SelectionColor = textColor ?? txtLog.ForeColor; // Belirtilen rengi veya varsayılan metin rengini ayarlar.
            // Mesajı zaman damgasıyla birlikte txtLog'a ekler.
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            txtLog.SelectionColor = originalColor; // Seçim rengini eski haline getirir.
            // Log alanındaki satır sayısı 1000'i geçerse, en eski satırları (800 satır kalacak şekilde) siler.
            if (txtLog.Lines.Length > 1000)
            {
                txtLog.Select(0, txtLog.GetFirstCharIndexFromLine(txtLog.Lines.Length - 800));
                txtLog.SelectedText = "";
            }
            txtLog.ScrollToCaret(); // Log alanını en son eklenen satıra kaydırır.
        }

        // Durum çubuğundaki (statusStrip1) toolStripStatusLabel1'in metnini ve rengini günceller.
        // Eğer farklı bir thread'den çağrılırsa, BeginInvoke ile UI thread'inde çalışmasını sağlar.
        private void UpdateStatus(string message, Color color)
        {
            if (statusStrip1.InvokeRequired) // Eğer bu metod UI thread'i dışından çağrıldıysa
            {
                // UpdateStatusInternal metodunu UI thread'inde çalıştırmak için BeginInvoke kullanır.
                statusStrip1.BeginInvoke(new Action(() => UpdateStatusInternal(message, color)));
            }
            else // UI thread'inden çağrıldıysa doğrudan UpdateStatusInternal'ı çağırır.
            {
                UpdateStatusInternal(message, color);
            }
        }

        // UpdateStatus tarafından çağrılan, durum etiketini fiilen güncelleyen iç metot.
        private void UpdateStatusInternal(string message, Color color)
        {
            if (toolStripStatusLabel1.IsDisposed) return; // Etiket dispose edilmişse bir şey yapmaz.
            toolStripStatusLabel1.Text = message; // Etiketin metnini ayarlar.
            toolStripStatusLabel1.ForeColor = color; // Etiketin rengini ayarlar.
        }
        #endregion
    }

    // CommandItem ve UsbPacket sınıfları
    // cmbCommands ComboBox'ında gösterilecek komut öğelerini temsil eden bir sınıftır.
    // Her öğe bir komut adı (Name) ve bir komut ID'si (CommandId) içerir.
    public class CommandItem
    {
        public string Name { get; set; } // Komutun kullanıcı arayüzünde gösterilecek adı.
        public byte CommandId { get; set; } // Komutun sayısal ID'si.

        // CommandItem sınıfının yapıcı metodu.
        // Yeni bir CommandItem nesnesi oluşturur ve Name ile CommandId özelliklerini ayarlar.
        public CommandItem(string name, byte commandId) { Name = name; CommandId = commandId; }

        // ToString metodunu override ederek, ComboBox'ta öğenin Name özelliğinin gösterilmesini sağlar.
        public override string ToString() => Name;
    }


}