#include "UsbPacket.h"

/* CRC-16-CCITT hesaplama algoritması */
uint16 CalculateCRC16(uint8* data, uint16 length) {
    uint16 crc = 0xFFFF;
    uint16 i, j;
    
    for (i = 0; i < length; i++) {
        crc ^= data[i] << 8;
        for (j = 0; j < 8; j++) {
            if (crc & 0x8000)
                crc = (crc << 1) ^ 0x1021;
            else
                crc <<= 1;
        }
    }
    
    return crc;
}

/* Paket başlatma */
void InitPacket(UsbPacket* packet) {
    packet->header[0] = PACKET_HEADER1;
    packet->header[1] = PACKET_HEADER2;
    packet->commandId = 0;
    packet->dataLength = 0;
    packet->checksum = 0;
    
    /* Veri alanını temizle */
    uint8 i;
    for (i = 0; i < MAX_DATA_SIZE; i++) {
        packet->data[i] = 0;
    }
}

/* Paketin geçerliliğini kontrol et */
uint8 ValidatePacket(UsbPacket* packet) {
    /* Başlık kontrolü */
    if (packet->header[0] != PACKET_HEADER1 || packet->header[1] != PACKET_HEADER2) {
        return RESULT_ERROR;
    }
    
    /* Veri uzunluğunun sınırlarını kontrol et */
    if (packet->dataLength > MAX_DATA_SIZE) {
        return RESULT_ERROR;
    }
    
    /* CRC kontrolü için paketin byte dizisine dönüştürülmesi */
    uint8 packetBytes[64]; /* 64-byte paket */
    uint8 i;
    
    packetBytes[0] = packet->header[0];
    packetBytes[1] = packet->header[1];
    packetBytes[2] = packet->commandId;
    packetBytes[3] = packet->dataLength;
    
    for (i = 0; i < packet->dataLength; i++) {
        packetBytes[4 + i] = packet->data[i];
    }
    
    /* Gelen CRC değeri */
    uint16 receivedCRC = packet->checksum;
    
    /* CRC hesapla */
    uint16 calculatedCRC = CalculateCRC16(packetBytes, 4 + packet->dataLength);
    
    /* CRC kontrolü */
    if (receivedCRC != calculatedCRC) {
        return RESULT_CRC_ERROR;
    }
    
    return RESULT_OK;
}

/* String verisini USB paketi için hazırla */
uint8 PrepareStringData(char* stringData, uint8* outData) {
    uint8 i = 0;
    uint8 len = 0;
    
    /* String uzunluğunu bul (NULL terminatör hariç) */
    while(stringData[len] != '\0' && len < MAX_DATA_SIZE) {
        len++;
    }
    
    /* String verisini çıkış buffer'ına kopyala */
    for(i = 0; i < len; i++) {
        outData[i] = (uint8)stringData[i];
    }
    
    return len;
}

/* Yanıt paketi hazırlama */
void PrepareResponsePacket(UsbPacket* txPacket, UsbPacket* rxPacket, 
                          uint8 commandId, uint8* data, uint8 dataLength) {
    uint8 i;
    
    /* Başlık ve komut bilgilerini ayarla */
    txPacket->header[0] = PACKET_HEADER1;
    txPacket->header[1] = PACKET_HEADER2;
    txPacket->commandId = commandId;
    txPacket->dataLength = dataLength;
    
    /* Veri kopyala */
    for (i = 0; i < dataLength && i < MAX_DATA_SIZE; i++) {
        txPacket->data[i] = data[i];
    }
    
    /* CRC hesaplama için paketin byte dizisine dönüştürülmesi */
    uint8 packetBytes[64]; /* 64-byte paket */
    
    packetBytes[0] = txPacket->header[0];
    packetBytes[1] = txPacket->header[1];
    packetBytes[2] = txPacket->commandId;
    packetBytes[3] = txPacket->dataLength;
    
    for (i = 0; i < txPacket->dataLength; i++) {
        packetBytes[4 + i] = txPacket->data[i];
    }
    
    /* CRC hesapla */
    txPacket->checksum = CalculateCRC16(packetBytes, 4 + txPacket->dataLength);
}
