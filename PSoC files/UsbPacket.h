#ifndef USB_PACKET_H
#define USB_PACKET_H
    
#include <cytypes.h>
#include <stdint.h>
    
#define PACKET_HEADER1     0xAA
#define PACKET_HEADER2     0x55
#define MAX_DATA_SIZE      60
#define CMD_READ           0x01
#define CMD_WRITE          0x02
#define CMD_STATUS         0x03
#define CMD_RESET          0x04
#define CMD_VERSION        0x05
#define CMD_ECHO_STRING    0x06

#define RESULT_OK          0x00
#define RESULT_ERROR       0x01
#define RESULT_INVALID_CMD 0x02
#define RESULT_CRC_ERROR   0x03

    
typedef struct {
    uint8 header[2];     /* 0xAA, 0x55 sabit başlık */
    uint8 commandId;     /* Komut ID */
    uint8 dataLength;    /* Veri alanı uzunluğu */
    uint8 data[MAX_DATA_SIZE]; /* Veri alanı */
    uint16 checksum;     /* CRC16 kontrol değeri */
} UsbPacket;

/* Fonksiyon prototipleri */

uint8 PrepareStringData(char* stringData, uint8* outData);
uint16 CalculateCRC16(uint8* data, uint16 length);
void InitPacket(UsbPacket* packet);
uint8 ValidatePacket(UsbPacket* packet);
void PrepareResponsePacket(UsbPacket* txPacket, UsbPacket* rxPacket, 
                          uint8 commandId, uint8* data, uint8 dataLength);

#endif /* USB_PACKET_H */
