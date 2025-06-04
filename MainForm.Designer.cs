// MainForm.Designer.cs
namespace usb_bulk_2
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageUsbControl = new System.Windows.Forms.TabPage();
            this.panelUsbControl = new System.Windows.Forms.Panel();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtData = new System.Windows.Forms.TextBox();
            this.labelData = new System.Windows.Forms.Label();
            this.cmbCommands = new System.Windows.Forms.ComboBox();
            this.labelCommand = new System.Windows.Forms.Label();
            this.tabPageCanInterface = new System.Windows.Forms.TabPage();
            this.splitContainerCan = new System.Windows.Forms.SplitContainer();
            this.groupBoxCanReceive = new System.Windows.Forms.GroupBox();
            this.listViewCanReceive = new System.Windows.Forms.ListView();
            this.colRxSeq = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxDlc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxData = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxPSoCTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRxProps = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.groupBoxCanTransmit = new System.Windows.Forms.GroupBox();
            this.btnClearCanTransmitFields = new System.Windows.Forms.Button();
            this.btnAddCanTransmitJob = new System.Windows.Forms.Button();
            this.txtCanTransmitData = new System.Windows.Forms.TextBox();
            this.labelCanTransmitData = new System.Windows.Forms.Label();
            this.numCanTransmitDlc = new System.Windows.Forms.NumericUpDown();
            this.labelCanTransmitDlc = new System.Windows.Forms.Label();
            this.txtCanTransmitId = new System.Windows.Forms.TextBox();
            this.labelCanTransmitId = new System.Windows.Forms.Label();
            this.listViewCanTransmit = new System.Windows.Forms.ListView();
            this.colTxSeq = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTxTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTxId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTxDlc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTxData = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnSendCanMessage = new System.Windows.Forms.Button();
            this.chkCanExtendedId = new System.Windows.Forms.CheckBox();
            this.panelLog = new System.Windows.Forms.Panel();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.statusStrip1.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabPageUsbControl.SuspendLayout();
            this.panelUsbControl.SuspendLayout();
            this.tabPageCanInterface.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCan)).BeginInit();
            this.splitContainerCan.Panel1.SuspendLayout();
            this.splitContainerCan.Panel2.SuspendLayout();
            this.splitContainerCan.SuspendLayout();
            this.groupBoxCanReceive.SuspendLayout();
            this.groupBoxCanTransmit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCanTransmitDlc)).BeginInit();
            this.panelLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 539); // Adjusted
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(784,22); // Adjusted
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(129, 17);
            this.toolStripStatusLabel1.Text = "Uygulama başlatılıyor...";
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageUsbControl);
            this.tabControlMain.Controls.Add(this.tabPageCanInterface);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(784, 350); // Adjusted
            this.tabControlMain.TabIndex = 1;
            // 
            // tabPageUsbControl
            // 
            this.tabPageUsbControl.Controls.Add(this.panelUsbControl);
            this.tabPageUsbControl.Location = new System.Drawing.Point(4, 22);
            this.tabPageUsbControl.Name = "tabPageUsbControl";
            this.tabPageUsbControl.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageUsbControl.Size = new System.Drawing.Size(776, 324); // Adjusted
            this.tabPageUsbControl.TabIndex = 0;
            this.tabPageUsbControl.Text = "USB Kontrol";
            this.tabPageUsbControl.UseVisualStyleBackColor = true;
            // 
            // panelUsbControl
            // 
            this.panelUsbControl.Controls.Add(this.btnSend);
            this.panelUsbControl.Controls.Add(this.txtData);
            this.panelUsbControl.Controls.Add(this.labelData);
            this.panelUsbControl.Controls.Add(this.cmbCommands);
            this.panelUsbControl.Controls.Add(this.labelCommand);
            this.panelUsbControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelUsbControl.Location = new System.Drawing.Point(3, 3);
            this.panelUsbControl.Name = "panelUsbControl";
            this.panelUsbControl.Size = new System.Drawing.Size(770, 318); // Adjusted
            this.panelUsbControl.TabIndex = 0;
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Location = new System.Drawing.Point(680, 15); // Adjusted
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 58);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "Gönder";
            this.btnSend.UseVisualStyleBackColor = true;
            // 
            // txtData
            // 
            this.txtData.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtData.Location = new System.Drawing.Point(115, 52);
            this.txtData.Name = "txtData";
            this.txtData.Size = new System.Drawing.Size(550, 20); // Adjusted
            this.txtData.TabIndex = 3;
            // 
            // labelData
            // 
            this.labelData.AutoSize = true;
            this.labelData.Location = new System.Drawing.Point(12, 55);
            this.labelData.Name = "labelData";
            this.labelData.Size = new System.Drawing.Size(29, 13);
            this.labelData.TabIndex = 2;
            this.labelData.Text = "Veri:";
            // 
            // cmbCommands
            // 
            this.cmbCommands.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbCommands.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCommands.FormattingEnabled = true;
            this.cmbCommands.Location = new System.Drawing.Point(115, 15);
            this.cmbCommands.Name = "cmbCommands";
            this.cmbCommands.Size = new System.Drawing.Size(550, 21); // Adjusted
            this.cmbCommands.TabIndex = 1;
            // 
            // labelCommand
            // 
            this.labelCommand.AutoSize = true;
            this.labelCommand.Location = new System.Drawing.Point(12, 18);
            this.labelCommand.Name = "labelCommand";
            this.labelCommand.Size = new System.Drawing.Size(43, 13);
            this.labelCommand.TabIndex = 0;
            this.labelCommand.Text = "Komut:";
            // 
            // tabPageCanInterface
            // 
            this.tabPageCanInterface.Controls.Add(this.splitContainerCan);
            this.tabPageCanInterface.Location = new System.Drawing.Point(4, 22);
            this.tabPageCanInterface.Name = "tabPageCanInterface";
            this.tabPageCanInterface.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageCanInterface.Size = new System.Drawing.Size(776, 324); // Adjusted
            this.tabPageCanInterface.TabIndex = 1;
            this.tabPageCanInterface.Text = "CAN Arayüzü";
            this.tabPageCanInterface.UseVisualStyleBackColor = true;
            // 
            // splitContainerCan
            // 
            this.splitContainerCan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerCan.Location = new System.Drawing.Point(3, 3);
            this.splitContainerCan.Name = "splitContainerCan";
            this.splitContainerCan.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerCan.Panel1 (Receive)
            // 
            this.splitContainerCan.Panel1.Controls.Add(this.groupBoxCanReceive);
            // 
            // splitContainerCan.Panel2 (Transmit)
            // 
            this.splitContainerCan.Panel2.Controls.Add(this.groupBoxCanTransmit);
            this.splitContainerCan.Size = new System.Drawing.Size(770, 318); // Adjusted
            this.splitContainerCan.SplitterDistance = 150; // Adjusted
            this.splitContainerCan.TabIndex = 0;
            // 
            // groupBoxCanReceive
            // 
            this.groupBoxCanReceive.Controls.Add(this.listViewCanReceive);
            this.groupBoxCanReceive.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxCanReceive.Location = new System.Drawing.Point(0, 0);
            this.groupBoxCanReceive.Name = "groupBoxCanReceive";
            this.groupBoxCanReceive.Size = new System.Drawing.Size(770, 150); // Adjusted
            this.groupBoxCanReceive.TabIndex = 0;
            this.groupBoxCanReceive.TabStop = false;
            this.groupBoxCanReceive.Text = "CAN Receive";
            // 
            // listViewCanReceive
            // 
            this.listViewCanReceive.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colRxSeq,
            this.colRxTime,
            this.colRxId,
            this.colRxDlc,
            this.colRxData,
            this.colRxPSoCTime,
            this.colRxProps});
            this.listViewCanReceive.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewCanReceive.FullRowSelect = true;
            this.listViewCanReceive.GridLines = true;
            this.listViewCanReceive.HideSelection = false;
            this.listViewCanReceive.Location = new System.Drawing.Point(3, 16);
            this.listViewCanReceive.Name = "listViewCanReceive";
            this.listViewCanReceive.Size = new System.Drawing.Size(764, 131); // Adjusted
            this.listViewCanReceive.TabIndex = 0;
            this.listViewCanReceive.UseCompatibleStateImageBehavior = false;
            this.listViewCanReceive.View = System.Windows.Forms.View.Details;
            // Column Headers for listViewCanReceive
            this.colRxSeq.Text = "No."; this.colRxSeq.Width = 40;
            this.colRxTime.Text = "Time"; this.colRxTime.Width = 80;
            this.colRxId.Text = "ID (Hex)"; this.colRxId.Width = 70;
            this.colRxDlc.Text = "DLC"; this.colRxDlc.Width = 40;
            this.colRxData.Text = "Data (Hex)"; this.colRxData.Width = 200;
            this.colRxPSoCTime.Text = "PSoC TS"; this.colRxPSoCTime.Width = 80;
            this.colRxProps.Text = "Props"; this.colRxProps.Width = 50;
            // 
            // groupBoxCanTransmit
            // 
            this.groupBoxCanTransmit.Controls.Add(this.chkCanExtendedId);
            this.groupBoxCanTransmit.Controls.Add(this.btnSendCanMessage);
            this.groupBoxCanTransmit.Controls.Add(this.listViewCanTransmit);
            this.groupBoxCanTransmit.Controls.Add(this.txtCanTransmitData);
            this.groupBoxCanTransmit.Controls.Add(this.labelCanTransmitData);
            this.groupBoxCanTransmit.Controls.Add(this.numCanTransmitDlc);
            this.groupBoxCanTransmit.Controls.Add(this.labelCanTransmitDlc);
            this.groupBoxCanTransmit.Controls.Add(this.txtCanTransmitId);
            this.groupBoxCanTransmit.Controls.Add(this.labelCanTransmitId);
            this.groupBoxCanTransmit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxCanTransmit.Location = new System.Drawing.Point(0, 0);
            this.groupBoxCanTransmit.Name = "groupBoxCanTransmit";
            this.groupBoxCanTransmit.Size = new System.Drawing.Size(770, 164); // Adjusted
            this.groupBoxCanTransmit.TabIndex = 0;
            this.groupBoxCanTransmit.TabStop = false;
            this.groupBoxCanTransmit.Text = "CAN Transmit";
            //
            // labelCanTransmitId
            //
            this.labelCanTransmitId.AutoSize = true;
            this.labelCanTransmitId.Location = new System.Drawing.Point(6, 22);
            this.labelCanTransmitId.Name = "labelCanTransmitId";
            this.labelCanTransmitId.Size = new System.Drawing.Size(49, 13);
            this.labelCanTransmitId.Text = "ID (Hex):";
            //
            // txtCanTransmitId
            //
            this.txtCanTransmitId.Location = new System.Drawing.Point(60, 19);
            this.txtCanTransmitId.Name = "txtCanTransmitId";
            this.txtCanTransmitId.Size = new System.Drawing.Size(100, 20);
            this.txtCanTransmitId.TabIndex = 1;
            this.txtCanTransmitId.Text = "100";
            //
            // chkCanExtendedId
            //
            this.chkCanExtendedId.AutoSize = true;
            this.chkCanExtendedId.Location = new System.Drawing.Point(170, 21);
            this.chkCanExtendedId.Name = "chkCanExtendedId";
            this.chkCanExtendedId.Size = new System.Drawing.Size(70, 17);
            this.chkCanExtendedId.Text = "Extended";
            this.chkCanExtendedId.UseVisualStyleBackColor = true;
            //
            // labelCanTransmitDlc
            //
            this.labelCanTransmitDlc.AutoSize = true;
            this.labelCanTransmitDlc.Location = new System.Drawing.Point(250, 22);
            this.labelCanTransmitDlc.Name = "labelCanTransmitDlc";
            this.labelCanTransmitDlc.Size = new System.Drawing.Size(31, 13);
            this.labelCanTransmitDlc.Text = "DLC:";
            //
            // numCanTransmitDlc
            //
            this.numCanTransmitDlc.Location = new System.Drawing.Point(285, 19);
            this.numCanTransmitDlc.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            this.numCanTransmitDlc.Name = "numCanTransmitDlc";
            this.numCanTransmitDlc.Size = new System.Drawing.Size(40, 20);
            this.numCanTransmitDlc.TabIndex = 3;
            this.numCanTransmitDlc.Value = new decimal(new int[] { 8, 0, 0, 0 });
            //
            // labelCanTransmitData
            //
            this.labelCanTransmitData.AutoSize = true;
            this.labelCanTransmitData.Location = new System.Drawing.Point(6, 48);
            this.labelCanTransmitData.Name = "labelCanTransmitData";
            this.labelCanTransmitData.Size = new System.Drawing.Size(100, 13);
            this.labelCanTransmitData.Text = "Data (Hex, örn: 00 AA FF):";
            //
            // txtCanTransmitData
            //
            this.txtCanTransmitData.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCanTransmitData.Location = new System.Drawing.Point(115, 45);
            this.txtCanTransmitData.Name = "txtCanTransmitData";
            this.txtCanTransmitData.Size = new System.Drawing.Size(550, 20); // Adjusted
            this.txtCanTransmitData.TabIndex = 5;
            this.txtCanTransmitData.Text = "00 11 22 33 44 55 66 77";
            //
            // btnSendCanMessage
            //
            this.btnSendCanMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSendCanMessage.Location = new System.Drawing.Point(680, 19); // Adjusted
            this.btnSendCanMessage.Name = "btnSendCanMessage";
            this.btnSendCanMessage.Size = new System.Drawing.Size(75, 46);
            this.btnSendCanMessage.TabIndex = 6;
            this.btnSendCanMessage.Text = "Gönder";
            this.btnSendCanMessage.UseVisualStyleBackColor = true;
            //
            // listViewCanTransmit
            //
            this.listViewCanTransmit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewCanTransmit.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colTxSeq,
            this.colTxTime,
            this.colTxId,
            this.colTxDlc,
            this.colTxData});
            this.listViewCanTransmit.FullRowSelect = true;
            this.listViewCanTransmit.GridLines = true;
            this.listViewCanTransmit.HideSelection = false;
            this.listViewCanTransmit.Location = new System.Drawing.Point(6, 71);
            this.listViewCanTransmit.Name = "listViewCanTransmit";
            this.listViewCanTransmit.Size = new System.Drawing.Size(758, 87); // Adjusted
            this.listViewCanTransmit.TabIndex = 7;
            this.listViewCanTransmit.UseCompatibleStateImageBehavior = false;
            this.listViewCanTransmit.View = System.Windows.Forms.View.Details;
            // Column Headers for listViewCanTransmit
            this.colTxSeq.Text = "No."; this.colTxSeq.Width = 40;
            this.colTxTime.Text = "Time"; this.colTxTime.Width = 80;
            this.colTxId.Text = "ID (Hex)"; this.colTxId.Width = 70;
            this.colTxDlc.Text = "DLC"; this.colTxDlc.Width = 40;
            this.colTxData.Text = "Data (Hex)"; this.colTxData.Width = 200;
            // 
            // panelLog
            // 
            this.panelLog.Controls.Add(this.txtLog);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(0, 0);
            this.panelLog.Name = "panelLog";
            this.panelLog.Size = new System.Drawing.Size(784, 185); // Adjusted
            this.panelLog.TabIndex = 0;
            // 
            // txtLog
            // 
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Location = new System.Drawing.Point(0, 0);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new System.Drawing.Size(784, 185); // Adjusted
            this.txtLog.TabIndex = 0;
            this.txtLog.Text = "";
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.tabControlMain);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.panelLog);
            this.splitContainerMain.Size = new System.Drawing.Size(784, 539); // Adjusted
            this.splitContainerMain.SplitterDistance = 350; // Adjusted
            this.splitContainerMain.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561); // Adjusted for more space
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.statusStrip1);
            this.MinimumSize = new System.Drawing.Size(800, 600); // Adjusted
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PSoC USB & CAN Test Uygulaması"; // Title updated
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabControlMain.ResumeLayout(false);
            this.tabPageUsbControl.ResumeLayout(false);
            this.panelUsbControl.ResumeLayout(false);
            this.panelUsbControl.PerformLayout();
            this.tabPageCanInterface.ResumeLayout(false);
            this.splitContainerCan.Panel1.ResumeLayout(false);
            this.splitContainerCan.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCan)).EndInit();
            this.splitContainerCan.ResumeLayout(false);
            this.groupBoxCanReceive.ResumeLayout(false);
            this.groupBoxCanTransmit.ResumeLayout(false);
            this.groupBoxCanTransmit.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCanTransmitDlc)).EndInit();
            this.panelLog.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageUsbControl;
        private System.Windows.Forms.Panel panelUsbControl;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.TextBox txtData;
        private System.Windows.Forms.Label labelData;
        private System.Windows.Forms.ComboBox cmbCommands;
        private System.Windows.Forms.Label labelCommand;
        private System.Windows.Forms.TabPage tabPageCanInterface;
        private System.Windows.Forms.SplitContainer splitContainerCan;
        private System.Windows.Forms.GroupBox groupBoxCanReceive;
        private System.Windows.Forms.ListView listViewCanReceive;
        private System.Windows.Forms.GroupBox groupBoxCanTransmit;
        private System.Windows.Forms.ListView listViewCanTransmit;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.TextBox txtCanTransmitData;
        private System.Windows.Forms.Label labelCanTransmitData;
        private System.Windows.Forms.NumericUpDown numCanTransmitDlc;
        private System.Windows.Forms.Label labelCanTransmitDlc;
        private System.Windows.Forms.TextBox txtCanTransmitId;
        private System.Windows.Forms.Label labelCanTransmitId;
        private System.Windows.Forms.Button btnSendCanMessage;
        private System.Windows.Forms.CheckBox chkCanExtendedId;
        private System.Windows.Forms.Button btnClearCanTransmitFields;
        private System.Windows.Forms.Button btnAddCanTransmitJob;

        private System.Windows.Forms.ColumnHeader colRxSeq;
        private System.Windows.Forms.ColumnHeader colRxTime;
        private System.Windows.Forms.ColumnHeader colRxId;
        private System.Windows.Forms.ColumnHeader colRxDlc;
        private System.Windows.Forms.ColumnHeader colRxData;
        private System.Windows.Forms.ColumnHeader colRxPSoCTime;
        private System.Windows.Forms.ColumnHeader colRxProps;

        private System.Windows.Forms.ColumnHeader colTxSeq;
        private System.Windows.Forms.ColumnHeader colTxTime;
        private System.Windows.Forms.ColumnHeader colTxId;
        private System.Windows.Forms.ColumnHeader colTxDlc;
        private System.Windows.Forms.ColumnHeader colTxData;
    }
}