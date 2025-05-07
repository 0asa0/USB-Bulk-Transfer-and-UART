using System;
using System.Text;

namespace usb_bulk_2
{
    public class UsbPacket
    {
        // Sabitler
        public const byte PACKET_HEADER1 = 0xAA;
        public const byte PACKET_HEADER2 = 0x55;
        public const int MAX_DATA_SIZE = 60;

        // Komut kodları
        public const byte CMD_READ = 0x01;
        public const byte CMD_WRITE = 0x02;
        public const byte CMD_STATUS = 0x03;
        public const byte CMD_RESET = 0x04;
        public const byte CMD_VERSION = 0x05;
        public const byte CMD_ECHO_STRING = 0x06; 

        // Sonuç kodları
        public const byte RESULT_OK = 0x00;
        public const byte RESULT_ERROR = 0x01;
        public const byte RESULT_INVALID_CMD = 0x02;
        public const byte RESULT_CRC_ERROR = 0x03;

        // Paket özellikleri
        public byte[] Header { get; set; } = new byte[2] { PACKET_HEADER1, PACKET_HEADER2 };
        public byte CommandId { get; set; }
        public byte DataLength { get; set; }
        public byte[] Data { get; set; } = new byte[MAX_DATA_SIZE];
        public ushort Checksum { get; set; }

        // Yapıcı
        public UsbPacket()
        {
            InitPacket();
        }

        // Paketi başlat
        public void InitPacket()
        {
            Header[0] = PACKET_HEADER1;
            Header[1] = PACKET_HEADER2;
            CommandId = 0;
            DataLength = 0;
            Data = new byte[MAX_DATA_SIZE];
            Checksum = 0;
        }

        // Paketi byte dizisine dönüştür
        public byte[] ToByteArray()
        {
            byte[] packet = new byte[64]; // 64-byte paket

            // Header
            packet[0] = Header[0];
            packet[1] = Header[1];

            // Command ID ve Data Length
            packet[2] = CommandId;
            packet[3] = DataLength;

            // Data
            if (Data != null && DataLength > 0)
                Array.Copy(Data, 0, packet, 4, DataLength);

            // Checksum hesapla
            Checksum = CalculateCRC16(packet, 4 + DataLength);

            // Checksum'ı paketin sonuna ekle
            packet[4 + DataLength] = (byte)(Checksum & 0xFF);
            packet[4 + DataLength + 1] = (byte)((Checksum >> 8) & 0xFF);

            return packet;
        }

        // Byte dizisinden paket oluştur
        public static UsbPacket FromByteArray(byte[] packet)
        {
            UsbPacket result = new UsbPacket();

            // Paket doğrulaması yap
            if (packet[0] != PACKET_HEADER1 || packet[1] != PACKET_HEADER2)
                throw new Exception("Geçersiz paket başlığı!");

            result.CommandId = packet[2];
            result.DataLength = packet[3];

            // Data'yı ayıkla
            result.Data = new byte[MAX_DATA_SIZE];
            Array.Copy(packet, 4, result.Data, 0, result.DataLength);

            // Checksum'ı ayıkla
            result.Checksum = (ushort)((packet[4 + result.DataLength + 1] << 8) | packet[4 + result.DataLength]);

            // Checksum doğrula
            ushort calculatedChecksum = CalculateCRC16(packet, 4 + result.DataLength);

            if (result.Checksum != calculatedChecksum)
                throw new Exception("Checksum hatası!");

            return result;
        }

        // CRC16-CCITT hesaplama
        private static ushort CalculateCRC16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(data[i] << 8);

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) > 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }

            return crc;
        }

        // Yanıt paketindeki sonuç kodunu al
        public byte GetResultCode()
        {
            if (DataLength > 0)
                return Data[0];

            return RESULT_ERROR;
        }

        // Komut adını al
        // Komut adını al
        public static string GetCommandName(byte commandId)
        {
            switch (commandId)
            {
                case CMD_READ: return "Read";
                case CMD_WRITE: return "Write";
                case CMD_STATUS: return "Status";
                case CMD_RESET: return "Reset";
                case CMD_VERSION: return "Version";
                case CMD_ECHO_STRING: return "String Echo";
                default: return $"Bilinmeyen (0x{commandId:X2})";
            }
        }

        // Sonuç kodunun adını al
        public static string GetResultName(byte resultCode)
        {
            switch (resultCode)
            {
                case RESULT_OK: return "Başarılı";
                case RESULT_ERROR: return "Genel Hata";
                case RESULT_INVALID_CMD: return "Geçersiz Komut";
                case RESULT_CRC_ERROR: return "CRC Hatası";
                default: return $"Bilinmeyen (0x{resultCode:X2})";
            }
        }

        // Paket içeriğini oku ve açıkla
        // Paket içeriğini oku ve açıkla
        public string ParseContent()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Komut: {GetCommandName(CommandId)}");
            sb.AppendLine($"Veri Uzunluğu: {DataLength} byte");

            byte resultCode = GetResultCode();
            sb.AppendLine($"Sonuç: {GetResultName(resultCode)}");

            if (resultCode == RESULT_OK)
            {
                switch (CommandId)
                {
                    case CMD_READ:
                        if (DataLength > 1)
                            sb.AppendLine($"Durum: 0x{Data[1]:X2}");
                        break;

                    case CMD_WRITE:
                        // Sadece başarı durumu
                        break;

                    case CMD_STATUS:
                        if (DataLength > 5)
                        {
                            sb.AppendLine($"Durum: 0x{Data[1]:X2}");
                            uint packetCount = (uint)(Data[2] | (Data[3] << 8) | (Data[4] << 16) | (Data[5] << 24));
                            sb.AppendLine($"Paket Sayacı: {packetCount:N0}");
                        }
                        break;

                    case CMD_RESET:
                        // Sadece başarı durumu
                        break;

                    case CMD_VERSION:
                        if (DataLength > 3)
                        {
                            sb.AppendLine($"Versiyon: v{Data[1]}.{Data[2]}.{Data[3]}");
                        }
                        break;

                    case CMD_ECHO_STRING:
                        if (DataLength > 1)
                        {
                            byte[] stringData = new byte[DataLength - 1];
                            Array.Copy(Data, 1, stringData, 0, DataLength - 1);
                            string text = Encoding.ASCII.GetString(stringData);
                            sb.AppendLine($"Echo: \"{text}\"");
                            sb.AppendLine($"Hex: {BitConverter.ToString(stringData).Replace("-", " ")}");
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        // UsbPacket.cs sınıfına eklenecek metotlar
        public void SetDataFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                DataLength = 0;
                return;
            }

            byte[] textBytes = Encoding.ASCII.GetBytes(text);
            DataLength = (byte)Math.Min(textBytes.Length, MAX_DATA_SIZE);
            Array.Copy(textBytes, Data, DataLength);
        }

        public string GetDataAsText(int startIndex = 0)
        {
            if (DataLength <= startIndex)
                return string.Empty;

            int textLength = DataLength - startIndex;
            return Encoding.ASCII.GetString(Data, startIndex, textLength);
        }

        public string GetDataAsHexString()
        {
            if (DataLength == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < DataLength; i++)
            {
                sb.Append(Data[i].ToString("X2"));
                if (i < DataLength - 1)
                    sb.Append(" ");
            }

            return sb.ToString();
        }


    }
}