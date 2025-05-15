/*main.c*/
#include <project.h>
#include "UsbPacket.h" 

/* Buffer boyutları */
#define CUSTOM_BULK_BUFFER_LEN 64
uint8 custom_outBuffer[CUSTOM_BULK_BUFFER_LEN]; /* Host'tan (Custom Bulk) alınacak veri */
uint8 custom_inBuffer[CUSTOM_BULK_BUFFER_LEN];  /* Host'a (Custom Bulk) gönderilecek veri */

/* USBUART (CDC) için Buffer Boyutu ve Buffer */
#define UART_BUFFER_SIZE 64 // CDC transferleri genellikle 64 byte paketler kullanır
uint8 uart_rx_buffer[UART_BUFFER_SIZE];

/*(Bulk için) */
UsbPacket rxPacket;
UsbPacket txPacket;

/* Cihaz versiyonu Bulk için */
const uint8 DEVICE_VERSION[3] = {1, 0, 0}; 

/* Durum değişkenleri (Custom Bulk için) */
uint8 deviceStatus = 0x00;
uint32 packetCounter = 0;


// Custom Bulk Fonksiyonları -----
void ProcessPacket() {
    uint8 result = ValidatePacket(&rxPacket);
    uint8 responseData[MAX_DATA_SIZE];
    uint8 responseLen = 0;
    
    if (result != RESULT_OK) {
        responseData[0] = result;
        PrepareResponsePacket(&txPacket, &rxPacket, 0xFF, responseData, 1);
        return;
    }
    
    switch(rxPacket.commandId) {
        case CMD_READ:
            responseData[0] = RESULT_OK;
            responseData[1] = deviceStatus;
            responseLen = 2;
            break;
        case CMD_WRITE:
            if (rxPacket.dataLength > 0) {
                deviceStatus = rxPacket.data[0];
                responseData[0] = RESULT_OK;
                responseLen = 1;
            } else {
                responseData[0] = RESULT_ERROR;
                responseLen = 1;
            }
            break;
        case CMD_STATUS:
            responseData[0] = RESULT_OK;
            responseData[1] = deviceStatus;
            responseData[2] = (uint8)(packetCounter & 0xFF);
            responseData[3] = (uint8)((packetCounter >> 8) & 0xFF);
            responseData[4] = (uint8)((packetCounter >> 16) & 0xFF);
            responseData[5] = (uint8)((packetCounter >> 24) & 0xFF);
            responseLen = 6;
            break;
        case CMD_RESET:
            deviceStatus = 0;
            packetCounter = 0;
            responseData[0] = RESULT_OK;
            responseLen = 1;
            break;
        case CMD_VERSION:
            responseData[0] = RESULT_OK;
            responseData[1] = DEVICE_VERSION[0];
            responseData[2] = DEVICE_VERSION[1];
            responseData[3] = DEVICE_VERSION[2];
            responseLen = 4;
            break;
        case CMD_ECHO_STRING:
            if (rxPacket.dataLength > 0) {
                responseData[0] = RESULT_OK;
                uint8 i;
                for (i = 0; i < rxPacket.dataLength; i++) {
                    responseData[i + 1] = rxPacket.data[i];
                }
                responseLen = rxPacket.dataLength + 1;
            } else {
                responseData[0] = RESULT_ERROR;
                responseLen = 1;
            }
            break;
        default:
            responseData[0] = RESULT_INVALID_CMD;
            responseLen = 1;
            break;
    }
    PrepareResponsePacket(&txPacket, &rxPacket, rxPacket.commandId, responseData, responseLen);
    packetCounter++;
}

void PacketToBytes(UsbPacket* packet, uint8* bytes) {
    uint8 i;
    bytes[0] = packet->header[0];
    bytes[1] = packet->header[1];
    bytes[2] = packet->commandId;
    bytes[3] = packet->dataLength;
    for (i = 0; i < packet->dataLength; i++) {
        bytes[4 + i] = packet->data[i];
    }
    bytes[4 + packet->dataLength] = (uint8)(packet->checksum & 0xFF);
    bytes[4 + packet->dataLength + 1] = (uint8)((packet->checksum >> 8) & 0xFF);
}

void BytesToPacket(uint8* bytes, UsbPacket* packet) {
    uint8 i;
    packet->header[0] = bytes[0];
    packet->header[1] = bytes[1];
    packet->commandId = bytes[2];
    packet->dataLength = bytes[3];
    for (i = 0; i < packet->dataLength; i++) {
        packet->data[i] = bytes[4 + i];
    }
    packet->checksum = (uint16)bytes[4 + packet->dataLength] | 
                     ((uint16)bytes[4 + packet->dataLength + 1] << 8);
}



int main() {
    uint16 uart_count; // USBUART'tan okunan byte sayısı

    CyGlobalIntEnable;
    
    
    USB_Start(0, USB_5V_OPERATION); 
    
    /* USB cihazı host tarafından konfigüre edilene kadar bekle */
    while (!USB_GetConfiguration());
    
    USB_CDC_Init();
    
    /* OUT EP Host'tan PSoC'ye Bulk için */
    USB_EnableOutEP(1);
    
    /* Paketleri başlat Bulk */
    InitPacket(&rxPacket);
    InitPacket(&txPacket);
    
    

    for(;;) {
        /* Bulk Transfer */
        if (USB_GetEPState(1) == USB_OUT_BUFFER_FULL) { // EP1'den (Bulk OUT) veri geldiyse
            uint16 custom_bulk_len; // Okunan byte sayısı
            custom_bulk_len = USB_ReadOutEP(1, custom_outBuffer, CUSTOM_BULK_BUFFER_LEN);
            
            BytesToPacket(custom_outBuffer, &rxPacket);
            ProcessPacket();
            PacketToBytes(&txPacket, custom_inBuffer);
            
            /* Veriyi geri gönder - EP2 bulk IN endpoint */
            USB_LoadInEP(2, custom_inBuffer, CUSTOM_BULK_BUFFER_LEN); 

            USB_EnableOutEP(1);
        }

        /* USB UART haberleşme */
        
        if (USB_IsConfigurationChanged()) {
            
            USB_CDC_Init();
            
            if (USB_GetConfiguration()) {
                 USB_EnableOutEP(1); // Custom Bulk OUT EP
            }
        }
        
        if (USB_DataIsReady() != 0u) { // PC'den PSoC'a veri var mı
            
            uart_count = USB_GetAll(uart_rx_buffer); // Gelen tüm veriyi al (EP5'ten oku)
            
            if (uart_count > 0) {
                /* Gelen veriyi PC'ye geri gönder (Echo) (EP4 Data IN endpoint) */
                /* Önce gönderim buffer'ının hazır olmasını bekle */
                
                while (USB_CDCIsReady() == 0u); // Gönderim buffer'ı boşalana kadar bekle
                
                USB_PutData(uart_rx_buffer, uart_count); // Veriyi PC'ye gönder (EP4'e yazar)
                
                
                if (UART_BUFFER_SIZE == uart_count)
                    {
                        while (0u == USB_CDCIsReady());
                        USB_PutData(NULL, 0u);
                    }
            }
        }
        
    }
    return 0;
}
