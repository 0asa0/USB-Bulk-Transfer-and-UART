#include "can_help.h"
#include "CAN.h" 

void CAN_TXmailBox_Change_DataLength(uint8 mailBox, uint8 DLC)
{
    uint32 CAN_TX_CFG_temp = CY_GET_REG32(CAN_TX_CMD_PTR(mailBox)) & CAN_TX_READ_BACK_MASK;
    CAN_TX_CFG_temp &= 0xFFF0FFFF;
    CAN_TX_CFG_temp |= DLC << 16; 
    CY_SET_REG32(CAN_TX_CMD_PTR(mailBox), (CAN_TX_CFG_temp | CAN_TX_WPN_SET));
}

/* Range of message ID is from 0x000 to 0x7EF for standard 11-bit ID and to 0x1FBFFFFF for an extended 29-bit ID. */
/* [31:3] – Identifier (ID[31:21] – identifier when IDE = 0, ID[31:3] - identifier when IDE = 1)                  */
void CAN_TXmailBox_Change_MsgID(uint8 txmailbox, uint32 MsgID) /* also updates the IDE bit */
{
    uint32 txid = (MsgID <= 0x7FF) ? (MsgID << 21) : (MsgID << 3) ;
    CY_SET_REG32(CAN_TX_ID_PTR(txmailbox), txid);
    /* IDE - ID Extention bit update */   
    uint32 txcmd_temp = (CY_GET_REG32(CAN_TX_CMD_PTR(txmailbox)) & CAN_TX_READ_BACK_MASK);
           txcmd_temp = (MsgID <= 0x7FF) ?  (txcmd_temp & ~CAN_TX_IDE_MASK) : (txcmd_temp | CAN_TX_IDE_MASK) ;
    CY_SET_REG32(CAN_TX_CMD_PTR(txmailbox), (txcmd_temp | CAN_TX_WPN_SET));
}

void CAN_RXmailBox_Change_MsgID(uint8 rxmailbox, uint32 MsgID) /* also updates the IDE bit */
{
    /* IDE bit in the rxcmd register not modifed. maybe required, need to test */
    uint32 MsgID_temp;
    uint32 MsgID_mask; /* bitvalue 1 means don't care, 0 means take it into account */
    if(MsgID <= 0x7FF) /* standard 11-bit ID */
    {
        MsgID_temp = (MsgID << 21);
        MsgID_mask = 0x001FFFF9u ;
    }
    else /* extended 29-bit ID */
    {
        MsgID_temp = (MsgID << 3) | 0x00000004; /* 0x4 is IDE bit                */
        MsgID_mask = 0x00000001u;               /* all the extended ID's enabled */
    }    
    CY_SET_REG32((reg32 *) (&CAN_RX[rxmailbox].rxamr), MsgID_mask); /* Write RX (AMR) Acceptance Mask Register */    
    CY_SET_REG32((reg32 *) (&CAN_RX[rxmailbox].rxacr), MsgID_temp); /* Write RX (ACR) Acceptance Clearance Register */
}

uint32 CAN_TXmailBox_Get_MsgID(uint8 txmailbox)
{
    uint8  txcmd_IDE_bit = (0 != (CY_GET_REG32(CAN_TX_CMD_PTR(txmailbox)) & CAN_TX_IDE_MASK) );
    uint32 txid          = CY_GET_REG32(CAN_TX_ID_PTR(txmailbox));
    txid = txcmd_IDE_bit ? (txid >> 3) : (txid >> 21) ;
    return txid;
}

uint32 CAN_RXmailBox_Get_MsgID(uint8 rxmailbox)
{
    uint32 rxid = CY_GET_REG32((reg32 *)(&CAN_RX[rxmailbox].rxacr));
    uint8  rxcmd_IDE_bit = (0 != (rxid & 0x00000004) );
    rxid = rxcmd_IDE_bit ? (rxid >> 3) : (rxid >> 21) ;
    return rxid;
}


void CAN_Send_Message(CAN_Message_t* msg)
{
    uint8 i;
    
    /* Set message ID for mailbox 0 */
    CAN_TXmailBox_Change_MsgID(0, msg->id);
    
    /* Set data length */
    CAN_TXmailBox_Change_DataLength(0, msg->length);
    
    /* Set data bytes */
    for (i = 0; i < msg->length && i < 8; i++) {
        switch(i) {
            case 0: CAN_TX_DATA_BYTE1(0) = msg->data[0]; break;
            case 1: CAN_TX_DATA_BYTE2(0) = msg->data[1]; break;
            case 2: CAN_TX_DATA_BYTE3(0) = msg->data[2]; break;
            case 3: CAN_TX_DATA_BYTE4(0) = msg->data[3]; break;
            case 4: CAN_TX_DATA_BYTE5(0) = msg->data[4]; break;
            case 5: CAN_TX_DATA_BYTE6(0) = msg->data[5]; break;
            case 6: CAN_TX_DATA_BYTE7(0) = msg->data[6]; break;
            case 7: CAN_TX_DATA_BYTE8(0) = msg->data[7]; break;
        }
    }
    
    /* Send message */
    CAN_SendMsg(0);
}

/* CAN */
volatile uint8 can_msg_received = 0;
volatile CAN_Message_t can_received_msg;

CY_ISR(CAN_ISR_Handler)
{
    /* Check for received message using direct register access */
    if (CAN_INT_SR_REG.byte[1] & CAN_RX_MESSAGE_MASK) {

        /* Message received, store it */
        can_received_msg.id = CAN_RXmailBox_Get_MsgID(0);
        can_received_msg.length = CAN_GET_DLC(0);

        /* Get data bytes */
        if (can_received_msg.length > 0) can_received_msg.data[0] = CAN_RX_DATA_BYTE1(0);
        if (can_received_msg.length > 1) can_received_msg.data[1] = CAN_RX_DATA_BYTE2(0);
        if (can_received_msg.length > 2) can_received_msg.data[2] = CAN_RX_DATA_BYTE3(0);
        if (can_received_msg.length > 3) can_received_msg.data[3] = CAN_RX_DATA_BYTE4(0);
        if (can_received_msg.length > 4) can_received_msg.data[4] = CAN_RX_DATA_BYTE5(0);
        if (can_received_msg.length > 5) can_received_msg.data[5] = CAN_RX_DATA_BYTE6(0);
        if (can_received_msg.length > 6) can_received_msg.data[6] = CAN_RX_DATA_BYTE7(0);
        if (can_received_msg.length > 7) can_received_msg.data[7] = CAN_RX_DATA_BYTE8(0);
        

        can_received_msg.properties = 0;
        if (CAN_GET_RX_IDE(0)) { 
                can_received_msg.properties |= 0x01; // Bit 0: IDE (1 = Extended)
            }
        
        static uint32 timestamp_counter = 0;
        can_received_msg.timestamp = timestamp_counter++;
        

        can_msg_received = 1;

        CAN_INT_SR_REG.byte[1] = CAN_RX_MESSAGE_MASK;
        CAN_RX_ACK_MESSAGE(0);
    }
    
}

uint8 CAN_Receive_Message(CAN_Message_t* msg)
{
    if (can_msg_received) {
        /* Copy the received message */
        msg->timestamp = can_received_msg.timestamp;
        msg->id = can_received_msg.id;
        msg->length = can_received_msg.length;
        msg->properties = can_received_msg.properties;
        
        uint8 i;
        for (i = 0; i < 8; i++) {
            msg->data[i] = can_received_msg.data[i];
        }
        
        can_msg_received = 0; /* Clear flag */
        return 1; /* Message received */
    }
    
    return 0; /* No message */
}

void CAN_Process_USB_Message(uint8* usb_data, uint16 length)
{
    CAN_Message_t can_msg;
    
    if (length >= 18) { 
        /* Parse USB data to CAN message structure */
        can_msg.timestamp = (uint32)usb_data[0] | 
                           ((uint32)usb_data[1] << 8) | 
                           ((uint32)usb_data[2] << 16) | 
                           ((uint32)usb_data[3] << 24);
        
        can_msg.id = (uint32)usb_data[4] | 
                     ((uint32)usb_data[5] << 8) | 
                     ((uint32)usb_data[6] << 16) | 
                     ((uint32)usb_data[7] << 24);
        
        /* Copy data bytes */
        uint8 i;
        for (i = 0; i < 8; i++) {
            can_msg.data[i] = usb_data[8 + i];
        }
        
        can_msg.length = usb_data[16];
        can_msg.properties = usb_data[17];
        
        /* Send CAN message */
        CAN_Send_Message(&can_msg);
    }
}

uint16 CAN_Prepare_USB_Message(CAN_Message_t* can_msg, uint8* usb_data)
{
    uint8 i;
    
    /* Pack CAN message to USB format */
    usb_data[0] = (uint8)(can_msg->timestamp & 0xFF);
    usb_data[1] = (uint8)((can_msg->timestamp >> 8) & 0xFF);
    usb_data[2] = (uint8)((can_msg->timestamp >> 16) & 0xFF);
    usb_data[3] = (uint8)((can_msg->timestamp >> 24) & 0xFF);
    
    usb_data[4] = (uint8)(can_msg->id & 0xFF);
    usb_data[5] = (uint8)((can_msg->id >> 8) & 0xFF);
    usb_data[6] = (uint8)((can_msg->id >> 16) & 0xFF);
    usb_data[7] = (uint8)((can_msg->id >> 24) & 0xFF);
    
    /* Copy data bytes */
    for (i = 0; i < 8; i++) {
        usb_data[8 + i] = can_msg->data[i];
    }
    
    usb_data[16] = can_msg->length;
    usb_data[17] = can_msg->properties;
    
    return 18; /* Total message length */
}
