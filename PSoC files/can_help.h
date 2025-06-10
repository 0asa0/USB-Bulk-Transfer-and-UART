#ifndef CAN_HELP_H
#define CAN_HELP_H

#include <project.h>

typedef struct {
    uint32 timestamp;    
    uint32 id;           
    uint8 data[8];      
    uint8 length;    
    uint8 properties;    /* Message properties bitfield */
} CAN_Message_t;

void CAN_TXmailBox_Change_DataLength(uint8 mailBox, uint8 DLC);
void CAN_TXmailBox_Change_MsgID(uint8 txmailbox, uint32 MsgID);
void CAN_RXmailBox_Change_MsgID(uint8 rxmailbox, uint32 MsgID);
uint32 CAN_TXmailBox_Get_MsgID(uint8 txmailbox);
uint32 CAN_RXmailBox_Get_MsgID(uint8 rxmailbox);

void CAN_Send_Message(CAN_Message_t* msg);
uint8 CAN_Receive_Message(CAN_Message_t* msg);
void CAN_Process_USB_Message(uint8* usb_data, uint16 length);
uint16 CAN_Prepare_USB_Message(CAN_Message_t* can_msg, uint8* usb_data);

CY_ISR_PROTO(CAN_ISR_Handler);

extern volatile uint8 can_msg_received;
extern volatile CAN_Message_t can_received_msg;

#endif /* CAN_HELP_H */
