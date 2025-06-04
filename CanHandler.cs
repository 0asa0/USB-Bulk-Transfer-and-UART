// CanHandler.cs
using System;
using System.Diagnostics;
using System.Globalization; // For TryParseHexData
using System.Threading;
using System.Threading.Tasks;
using CyUSB;

namespace usb_bulk_2
{
    public class CanHandler : IDisposable
    {
        private CyUSBDevice _usbDevice;
        private CyBulkEndPoint _canInEndpoint;  // EP6 (0x86 from PSoC's perspective)
        private CyBulkEndPoint _canOutEndpoint; // EP7 (0x07 from PSoC's perspective)

        private Task _canListenerTask;
        private CancellationTokenSource _listenerCts;
        private volatile bool _isListening; // volatile for thread safety on this flag

        public event Action<CanMessage> CanMessageReceived;
        public event Action<string, System.Drawing.Color?> LogMessageRequest; // Renamed for clarity

        private ulong _rxCanMessageCounter = 0;
        private ulong _txCanMessageCounter = 0;
        private readonly object _counterLock = new object(); // For thread-safe counter increment

        public bool IsDeviceReady => _usbDevice != null && _canInEndpoint != null && _canOutEndpoint != null && _usbDevice.DeviceHandle != IntPtr.Zero;

        public CanHandler()
        {
            // Constructor
        }

        public void InitializeDevice(CyUSBDevice device, CyBulkEndPoint canInEp, CyBulkEndPoint canOutEp)
        {
            _usbDevice = device;
            _canInEndpoint = canInEp;
            _canOutEndpoint = canOutEp;

            if (IsDeviceReady)
            {
                // Set timeouts:
                // IN endpoint timeout: Short, as we poll it. If 0, it can be blocking.
                // 50ms is a reasonable polling interval if data is not continuous.
                // If data is very frequent, a smaller timeout or event-driven approach (if supported) might be better.
                if (_canInEndpoint != null) _canInEndpoint.TimeOut = 50; // 50ms
                if (_canOutEndpoint != null) _canOutEndpoint.TimeOut = 500; // 500ms for sends, usually faster
                Log($"CAN Handler: Device and endpoints initialized. IN Timeout: {_canInEndpoint?.TimeOut}ms, OUT Timeout: {_canOutEndpoint?.TimeOut}ms", System.Drawing.Color.CornflowerBlue);
            }
            else
            {
                Log("CAN Handler: Initialization failed - USB device or CAN endpoints are not valid.", System.Drawing.Color.Red);
            }
        }

        public void StartListening()
        {
            if (!IsDeviceReady)
            {
                Log("CAN Listener: Cannot start. Device not ready or endpoints not set.", System.Drawing.Color.Red);
                return;
            }

            if (_isListening)
            {
                Log("CAN Listener: Already listening.", System.Drawing.Color.Orange);
                return;
            }

            _listenerCts = new CancellationTokenSource();
            _canListenerTask = Task.Factory.StartNew(() => ListenLoop(_listenerCts.Token), _listenerCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _isListening = true; // Set flag after task starts
            Log("CAN Listener: Started.", System.Drawing.Color.DarkGreen);
        }

        public void StopListening()
        {
            if (!_isListening)
            {
                // Log("CAN Listener: Not currently listening.", System.Drawing.Color.Gray);
                return;
            }

            _isListening = false; // Set flag immediately to prevent new operations

            if (_listenerCts != null)
            {
                Log("CAN Listener: Stopping...", System.Drawing.Color.Orange);
                _listenerCts.Cancel();
                try
                {
                    // Wait for the task to complete, with a timeout
                    bool completed = _canListenerTask?.Wait(TimeSpan.FromMilliseconds(500)) ?? true;
                    if (!completed)
                    {
                        Log("CAN Listener: Listener task did not complete in time.", System.Drawing.Color.OrangeRed);
                    }
                }
                catch (OperationCanceledException)
                {
                    Log("CAN Listener: Listener task cancellation acknowledged.", System.Drawing.Color.Gray);
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex =>
                    {
                        if (ex is OperationCanceledException)
                        {
                            Log("CAN Listener: Listener task cancellation acknowledged via AggregateException.", System.Drawing.Color.Gray);
                            return true;
                        }
                        Log($"CAN Listener: Exception during stop: {ex.Message}", System.Drawing.Color.Red);
                        return false; // Unhandled
                    });
                }
                finally
                {
                    _listenerCts.Dispose();
                    _listenerCts = null;
                    _canListenerTask = null; // Task is completed or cancelled
                    Log("CAN Listener: Stopped.", System.Drawing.Color.OrangeRed);
                }
            }
        }

        private void ListenLoop(CancellationToken token)
        {
            byte[] buffer = new byte[_canInEndpoint.MaxPktSize]; // Typically 64 for bulk

            while (!token.IsCancellationRequested && _isListening) // Check _isListening as well
            {
                if (!IsDeviceReady) // Re-check device readiness in loop
                {
                    Log("CAN Listener Loop: Device became unavailable. Stopping.", System.Drawing.Color.Red);
                    _isListening = false; // Ensure flag reflects state
                    break;
                }

                try
                {
                    int bytesToRead = buffer.Length; // Attempt to read up to MaxPktSize
                    bool success = _canInEndpoint.XferData(ref buffer, ref bytesToRead);

                    if (token.IsCancellationRequested) break;

                    if (success)
                    {
                        if (bytesToRead >= 18) // PSoC sends 18-byte CAN messages
                        {
                            // Assuming PSoC sends one 18-byte message per USB_LoadInEP call for CAN.
                            // If multiple messages could be packed, a loop here would be needed.
                            if (bytesToRead == 18)
                            {
                                ulong currentSeqNum;
                                lock (_counterLock) { currentSeqNum = ++_rxCanMessageCounter; }

                                CanMessage canMsg = CanMessage.FromPSoCByteArray(buffer, currentSeqNum, "Rx");
                                if (canMsg != null)
                                {
                                    CanMessageReceived?.Invoke(canMsg);
                                }
                                else
                                {
                                    Log($"CAN Rx: Failed to parse {bytesToRead} received bytes.", System.Drawing.Color.Orange);
                                }
                            }
                            else if (bytesToRead > 0) // Unexpected size but got data
                            {
                                Log($"CAN Rx: Received {bytesToRead} bytes (expected 18). Data: {BitConverter.ToString(buffer, 0, bytesToRead)}", System.Drawing.Color.Orange);
                            }
                        }
                        else if (bytesToRead > 0) // Less than 18 bytes, partial/error?
                        {
                            Log($"CAN Rx: Received {bytesToRead} bytes (less than 18). Data: {BitConverter.ToString(buffer, 0, bytesToRead)}", System.Drawing.Color.Orange);
                        }
                        // If bytesToRead == 0, it was likely a timeout, which is normal if no data.
                    }
                    else // XferData failed
                    {
                        // CyConst.USB_ERROR_TIMEOUT (1) or CyConst.USB_ERROR_IO_PENDING are common and might not be "errors" if timeout is short
                        if (_canInEndpoint.LastError != 1 && _canInEndpoint.LastError != 0)
                        {
                            Thread.Sleep(100); // Brief pause on error
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Log($"CAN Listener Loop Exception: {ex.Message}", System.Drawing.Color.Red);
                    Thread.Sleep(100); // Pause on unhandled exception
                }
                // A very short sleep can prevent tight looping if timeouts are extremely short,
                // but the endpoint timeout itself should manage this.
                // Thread.Sleep(1); // Optional, if CPU usage is high with very short timeouts
            }
            Log("CAN Listener Loop: Exited.", System.Drawing.Color.Gray);
        }

        public bool SendCanMessage(CanMessage message)
        {
            if (!IsDeviceReady)
            {
                Log("CAN Tx: Cannot send. Device not ready.", System.Drawing.Color.Red);
                return false;
            }

            message.Direction = "Tx";
            lock (_counterLock) { message.SequenceNumber = ++_txCanMessageCounter; }
            message.UiTimestamp = DateTime.Now;

            byte[] rawData = message.ToPSoCByteArray();
            int len = rawData.Length; // Should be 18

            Stopwatch sw = Stopwatch.StartNew(); // Optional for timing
            bool success = _canOutEndpoint.XferData(ref rawData, ref len);
            sw.Stop();

            if (success)
            {
                Log($"CAN Tx: Sent ID {message.IdToHexString()}, DLC {message.Length}. Time: {sw.Elapsed.TotalMilliseconds:F2}ms", System.Drawing.Color.DarkCyan);
                CanMessageReceived?.Invoke(message); // Notify UI to display the sent message
                return true;
            }
            else
            {
                Log($"CAN Tx Error: Failed to send. {_canOutEndpoint.LastError} (Code: {_canOutEndpoint.LastError})", System.Drawing.Color.Red);
                return false;
            }
        }

        public static bool TryParseHexData(string hexString, byte dlc, out byte[] data)
        {
            data = new byte[8]; // Initialize with zeros
            for (int i = 0; i < 8; i++) data[i] = 0x00;

            if (dlc == 0) return true;

            if (string.IsNullOrWhiteSpace(hexString) && dlc > 0) return false;
            if (string.IsNullOrWhiteSpace(hexString) && dlc == 0) return true;


            string[] hexValues = hexString.Split(new[] { ' ', ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (hexValues.Length != dlc) return false; // Number of data bytes must match DLC

            for (int i = 0; i < dlc; i++)
            {
                if (hexValues[i].Length > 2 || !byte.TryParse(hexValues[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out data[i]))
                {
                    return false; // Invalid hex byte format or parse error
                }
            }
            return true;
        }

        private void Log(string message, System.Drawing.Color? color)
        {
            LogMessageRequest?.Invoke(message, color);
        }

        public void Dispose()
        {
            StopListening(); // Ensure listener is stopped and resources released
            // _usbDevice and endpoints are managed by MainForm, so don't dispose them here.
        }
    }
}