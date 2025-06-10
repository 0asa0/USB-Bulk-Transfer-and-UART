// CanMessage.cs
using System;
using System.Linq; // For Enumerable.Repeat
using System.Text; // For StringBuilder

namespace usb_bulk_2
{
    public class CanMessage
    {
        public DateTime UiTimestamp { get; set; }     // C# tarafında mesajın işlendiği zaman
        public uint PSoCTimestamp { get; set; }       // PSoC'tan gelen ham timestamp değeri
        public uint Id { get; set; }
        public byte[] Data { get; private set; } = new byte[8]; // Her zaman 8 byte
        public byte Length { get; set; }              // DLC (0-8)
        public byte Properties { get; set; }          // PSoC'tan gelen properties
        public string Direction { get; set; }         // "Rx" veya "Tx"
        public ulong SequenceNumber { get; set; }      // UI tarafında atanan sıra numarası

        public CanMessage()
        {
            // Data dizisini başlat
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = 0x00; // veya başka bir varsayılan değer
            }
        }

        // PSoC'tan gelen 18 byte'lık ham veriden CanMessage oluştur
        public static CanMessage FromPSoCByteArray(byte[] rawData, ulong sequenceNumber, string direction)
        {
            if (rawData == null || rawData.Length < 18)
                throw new ArgumentException("Raw data must be at least 18 bytes long for CAN message.");

            var msg = new CanMessage
            {
                UiTimestamp = DateTime.Now,
                SequenceNumber = sequenceNumber,
                Direction = direction
            };

            msg.PSoCTimestamp = BitConverter.ToUInt32(rawData, 0);
            msg.Id = BitConverter.ToUInt32(rawData, 4);
            Array.Copy(rawData, 8, msg.Data, 0, 8); // Her zaman 8 byte kopyala
            msg.Length = rawData[16];
            if (msg.Length > 8) msg.Length = 8; // Güvenlik önlemi
            msg.Properties = rawData[17];

            return msg;
        }

        // CanMessage'ı PSoC'a gönderilecek 18 byte'lık ham veriye dönüştürür
        public byte[] ToPSoCByteArray()
        {
            byte[] rawData = new byte[18];
            BitConverter.GetBytes(PSoCTimestamp).CopyTo(rawData, 0); // Ya da C# tarafında atanacak bir değer
            BitConverter.GetBytes(Id).CopyTo(rawData, 4);
            Data.CopyTo(rawData, 8); // Data dizisi zaten 8 byte
            rawData[16] = Length;
            rawData[17] = Properties;
            return rawData;
        }

        public string DataToHexString()
        {
            if (Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(Length * 3);
            for (int i = 0; i < Length; i++)
            {
                sb.AppendFormat("{0:X2}", Data[i]);
                if (i < Length - 1)
                    sb.Append(" ");
            }
            return sb.ToString();
        }

        public string IdToHexString()
        {
            // PSoC kodunuzdaki ID ayarlama mantığına göre (standart/extended)
            // burada gösterimi ayarlayabilirsiniz. Şimdilik basit hex.
            return $"{Id:X}{(Id > 0x7FF ? "" : " (STD)")}"; // ID > 0x7FF ise Extended kabul edelim
        }
    }
}