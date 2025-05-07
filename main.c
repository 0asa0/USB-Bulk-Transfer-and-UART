#include <project.h>
#include "UsbPacket.h"

#define BUFFER_LEN 64
uint8 outBuffer[BUFFER_LEN];  /* data received from host */
uint8 inBuffer[BUFFER_LEN];   /* data that send to host */

UsbPacket rxPacket;
UsbPacket txPacket;

const uint8 DEVICE_VERSION[3] = {1, 0, 0}; /* v1.0.0 */

uint8 deviceStatus = 0x00;
uint32 packetCounter = 0;

void ProcessPacket() {
    uint8 result = ValidatePacket(&rxPacket);
    uint8 responseData[MAX_DATA_SIZE];
    uint8 responseLen = 0;
    char echoStr[MAX_DATA_SIZE + 1]; /* +1 for NULL terminator */
    
    /* package validation */
    if (result != RESULT_OK) {
        responseData[0] = result; /* error code */
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
            responseData[1] = DEVICE_VERSION[0]; /* Major */
            responseData[2] = DEVICE_VERSION[1]; /* Minor */
            responseData[3] = DEVICE_VERSION[2]; /* Patch */
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
    CyGlobalIntEnable;
    
    USB_Start(0, USB_5V_OPERATION);
    
    while (!USB_GetConfiguration());
    
    USB_EnableOutEP(1);
    
    InitPacket(&rxPacket);
    InitPacket(&txPacket);
    
    for(;;) {
        if (USB_GetEPState(1) == USB_OUT_BUFFER_FULL) {
            USB_ReadOutEP(1, outBuffer, BUFFER_LEN);
            
            BytesToPacket(outBuffer, &rxPacket);
            
            ProcessPacket();
            
            PacketToBytes(&txPacket, inBuffer);
            
            USB_LoadInEP(2, inBuffer, BUFFER_LEN);
            
            USB_EnableOutEP(1);
        }
    }
    return 0;
}
