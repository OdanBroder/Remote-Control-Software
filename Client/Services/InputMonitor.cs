using Client.Models;
using Gma.System.MouseKeyHook;
using System;
using System.Windows.Forms;
using Client.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Windows.Forms.Cursors;

namespace Client.Services
{
    /// <summary>
    /// Monitors and handles global keyboard and mouse input events
    /// </summary>
    public class InputMonitor : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool fBlockIt);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowCursor(bool bShow);

        [DllImport("user32.dll", EntryPoint = "GetCursorPos")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, uint pvParam, uint fWinIni);

        // Constants for SystemParametersInfo
        private const uint SPI_SETSCREENSAVERRUNNING = 0x0061;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        // Add these constants and delegate definitions
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private POINT _originalCursorPos;
        // Event handlers
        private KeyEventHandler _keyDownHandler;
        private KeyEventHandler _keyUpHandler;
        private MouseEventHandler _mouseDownHandler;
        private MouseEventHandler _mouseUpHandler;
        private MouseEventHandler _mouseClickHandler;
        private MouseEventHandler _mouseDoubleClickHandler;
        private MouseEventHandler _mouseMoveHandler;
        private MouseEventHandler _mouseWheelHandler;

        private IKeyboardMouseEvents _appHook;
        private readonly SendInputServices _inputSender;
        private bool _isDisposed;
        private Stopwatch _lastMouseMoveTime;
        private const int MOUSE_MOVE_THROTTLE_MS = 8; // Increased to ~120fps for better responsiveness
        private Point _lastMousePosition;
        private bool _isLocalInputDisabled = false;
        private IntPtr _lastActiveWindow;
        private readonly IntPtr _screenCaptureViewHandle; // Store the window handle for ScreenCaptureView

        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardProc;

        /// <summary>
        /// Initializes a new instance of the InputMonitor class
        /// </summary>
        /// <param name="inputSender">Service responsible for sending input actions</param>
        /// <param name="screenCaptureViewHandle">Handle of the ScreenCaptureView window</param>
        /// <exception cref="ArgumentNullException">Thrown when inputSender is null</exception>
        public InputMonitor(SendInputServices inputSender, IntPtr screenCaptureViewHandle)
        {
            _inputSender = inputSender ?? throw new ArgumentNullException(nameof(inputSender));
            _screenCaptureViewHandle = screenCaptureViewHandle;
            _lastMouseMoveTime = new Stopwatch();
            _lastMouseMoveTime.Start();
            _lastMousePosition = new Point(0, 0);
        }

        /// <summary>
        /// Checks if the ScreenCaptureView window is currently active
        /// </summary>
        private bool IsScreenCaptureViewActive()
        {
            return GetForegroundWindow() == _screenCaptureViewHandle;
        }

        /// <summary>
        /// Starts monitoring for input events
        /// </summary>
        /// <exception cref="Exception">Thrown when initialization fails</exception>
        public void Start()
        {
            if (_appHook != null) return;

            try
            {
                _appHook = Hook.AppEvents();

                // Initialize keyboard handlers
                _keyDownHandler = CreateKeyEventHandler("keydown");
                _keyUpHandler = CreateKeyEventHandler("keyup");

                // Initialize mouse handlers
                _mouseDownHandler = CreateMouseEventHandler("mousedown");
                _mouseUpHandler = CreateMouseEventHandler("mouseup");
                _mouseClickHandler = CreateMouseEventHandler("click");
                _mouseDoubleClickHandler = CreateMouseEventHandler("doubleclick");
                _mouseMoveHandler = CreateMouseEventHandler("mousemove");
                _mouseWheelHandler = CreateMouseEventHandler("wheel");

                // Subscribe to events
                SubscribeToEvents();

                // Only disable local input if ScreenCaptureView is active
                if (IsScreenCaptureViewActive())
                {
                    DisableLocalInput();
                }

                Console.WriteLine("InputMonitor started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start InputMonitor: {ex.Message}");
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Creates a mouse event handler for the specified action
        /// </summary>
        private MouseEventHandler CreateMouseEventHandler(string actionName)
        {
            return async (s, e) =>
            {
                // Always allow Ctrl+Alt combination to pass through
                if (Control.ModifierKeys.HasFlag(Keys.Control) && Control.ModifierKeys.HasFlag(Keys.Alt))
                {
                    ToggleLocalInput();
                    return;
                }

                // Only process events when ScreenCaptureView is active
                if (!IsScreenCaptureViewActive())
                {
                    return;
                }

                // When local input is disabled, send to remote
                if (_isLocalInputDisabled)
                {
                    // For mouse move events, implement throttling
                    if (actionName == "mousemove")
                    {
                        // Check if enough time has passed since last move
                        if (_lastMouseMoveTime.ElapsedMilliseconds < MOUSE_MOVE_THROTTLE_MS)
                        {
                            return;
                        }

                        // Check if position has changed significantly
                        if (Math.Abs(e.X - _lastMousePosition.X) < 1 &&
                            Math.Abs(e.Y - _lastMousePosition.Y) < 1)
                        {
                            return;
                        }

                        _lastMousePosition = new Point(e.X, e.Y);
                        _lastMouseMoveTime.Restart();
                    }

                    // Get screen dimensions using Windows Forms
                    var screen = Screen.PrimaryScreen;
                    int screenWidth = screen.Bounds.Width;
                    int screenHeight = screen.Bounds.Height;

                    // Convert absolute coordinates to relative (0-100)
                    double relativeX = (e.X * 100.0) / screenWidth;
                    double relativeY = (e.Y * 100.0) / screenHeight;

                    var action = new InputAction
                    {
                        Type = "mouse",
                        Action = actionName,
                        Button = actionName == "mousemove" ? "None" : 
                                actionName == "wheel" ? "wheel" : e.Button.ToString(),
                        X = (int)relativeX,
                        Y = actionName == "wheel" ? e.Delta : (int)relativeY,
                        Modifiers = GetModifierKeys()
                    };

                    try
                    {
                        await _inputSender.SendInputAsync(action);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while sending mouse action: {ex.Message}");
                        if (actionName == "mousemove")
                        {
                            Console.WriteLine($"Mouse move failed at position: X={e.X}, Y={e.Y}");
                        }
                    }
                }
            };
        }

        private KeyEventHandler CreateKeyEventHandler(string actionName)
        {
            return async (s, e) =>
            {
                // Always allow Ctrl+Alt combination to pass through
                if (Control.ModifierKeys.HasFlag(Keys.Control) && Control.ModifierKeys.HasFlag(Keys.Alt))
                {
                    ToggleLocalInput();
                    return;
                }

                // Only process events when ScreenCaptureView is active
                if (!IsScreenCaptureViewActive())
                {
                    return;
                }

                // When local input is disabled, handle system keys
                if (_isLocalInputDisabled)
                {
                    // Create the action first
                    var action = new InputAction
                    {
                        Type = "keyboard",
                        Action = actionName,
                        Key = e.KeyCode.ToString(),
                        Modifiers = GetModifierKeys()
                    };

                    // Check if it's a system key that needs to be blocked locally
                    if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin || // Windows key
                        e.KeyCode == Keys.Alt || e.KeyCode == Keys.Tab || // Alt+Tab
                        e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu || // Alt key variants
                        e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey || // Ctrl key variants
                        e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey || // Shift key variants
                        e.KeyCode == Keys.Escape || // Escape key
                        e.KeyCode == Keys.PrintScreen || // Print Screen
                        e.KeyCode == Keys.Pause || // Pause/Break
                        e.KeyCode == Keys.Insert || // Insert
                        e.KeyCode == Keys.Delete || // Delete
                        e.KeyCode == Keys.Home || // Home
                        e.KeyCode == Keys.End || // End
                        e.KeyCode == Keys.PageUp || // Page Up
                        e.KeyCode == Keys.PageDown || // Page Down
                        e.KeyCode == Keys.NumLock || // Num Lock
                        e.KeyCode == Keys.CapsLock) // Caps Lock
                    {
                        e.Handled = true; // Prevent local handling
                    }

                    try
                    {
                        // Always send to remote machine
                        await _inputSender.SendInputAsync(action);
                    }
                    catch (Exception ex)
                    {
                        Stop();
                        Console.WriteLine($"Error while sending keyboard action: {ex.Message}");
                    }
                }
            };
        }

        private string[] GetModifierKeys()
        {
            var modifiers = new List<string>();
            if (Control.ModifierKeys.HasFlag(Keys.Control)) modifiers.Add("Control");
            if (Control.ModifierKeys.HasFlag(Keys.Alt)) modifiers.Add("Alt");
            if (Control.ModifierKeys.HasFlag(Keys.Shift)) modifiers.Add("Shift");
            return modifiers.ToArray();
        }

        /// <summary>
        /// Subscribes to all input events
        /// </summary>
        private void SubscribeToEvents()
        {
            _appHook.KeyDown += _keyDownHandler;
            _appHook.KeyUp += _keyUpHandler;
            _appHook.MouseDown += _mouseDownHandler;
            _appHook.MouseUp += _mouseUpHandler;
            _appHook.MouseClick += _mouseClickHandler;
            _appHook.MouseDoubleClick += _mouseDoubleClickHandler;
            _appHook.MouseMove += _mouseMoveHandler;
            _appHook.MouseWheel += _mouseWheelHandler;
        }

        /// <summary>
        /// Stops monitoring for input events and cleans up resources
        /// </summary>
        public void Stop()
        {
            if (_appHook == null) return;

            try
            {
                UnsubscribeFromEvents();
                _appHook.Dispose();
                _appHook = null;

                // Re-enable input when stopping
                if (_isLocalInputDisabled)
                {
                    EnableLocalInput();
                }

                Console.WriteLine("InputMonitor stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while stopping InputMonitor: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unsubscribes from all input events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            _appHook.KeyDown -= _keyDownHandler;
            _appHook.KeyUp -= _keyUpHandler;
            _appHook.MouseDown -= _mouseDownHandler;
            _appHook.MouseUp -= _mouseUpHandler;
            _appHook.MouseClick -= _mouseClickHandler;
            _appHook.MouseDoubleClick -= _mouseDoubleClickHandler;
            _appHook.MouseMove -= _mouseMoveHandler;
            _appHook.MouseWheel -= _mouseWheelHandler;
        }

        /// <summary>
        /// Disposes of the InputMonitor and its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the InputMonitor and its resources
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Make sure to re-enable input when disposing
                        if (_isLocalInputDisabled)
                        {
                            BlockInput(false);

                            // Remove the low-level keyboard hook
                            if (_hookID != IntPtr.Zero)
                            {
                                UnhookWindowsHookEx(_hookID);
                                _hookID = IntPtr.Zero;
                            }

                            // Show cursor and restore position
                            ShowCursor(true);
                            if (_originalCursorPos.X != 0 || _originalCursorPos.Y != 0)
                            {
                                SetCursorPos(_originalCursorPos.X, _originalCursorPos.Y);
                            }
                        }

                        // Stop monitoring and clean up resources
                        Stop();

                        // Clean up any remaining resources
                        _lastMouseMoveTime?.Stop();
                        _lastMouseMoveTime = null;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during InputMonitor disposal: {ex.Message}");
                    }
                }
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~InputMonitor()
        {
            Dispose(false);
        }

        /// <summary>
        /// Toggles the local input state
        /// </summary>
        private void ToggleLocalInput()
        {
            // Console.WriteLine("ToggleLocalInput");
            if (_isLocalInputDisabled)
            {
                EnableLocalInput();
            }
            else
            {
                DisableLocalInput();
            }
        }

        /// <summary>
        /// Disables local input
        /// </summary>
        public void DisableLocalInput()
        {
            if (!_isLocalInputDisabled && IsScreenCaptureViewActive())
            {
                // Store current active window
                _lastActiveWindow = GetForegroundWindow();

                // Store original cursor position
                GetCursorPos(out _originalCursorPos);

                // Hide the cursor
                ShowCursor(false);

                // Set up the low-level keyboard hook
                _keyboardProc = HookCallback;
                _hookID = SetHook(_keyboardProc);

                // Disable input and system shortcuts
                BlockInput(true);

                // Disable screen saver and system shortcuts
                SystemParametersInfo(SPI_SETSCREENSAVERRUNNING, 1, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                _isLocalInputDisabled = true;
                // Console.WriteLine("Local input disabled for ScreenCaptureView");
            }
        }

        /// <summary>
        /// Enables local input
        /// </summary>
        public void EnableLocalInput()
        {
            if (_isLocalInputDisabled)
            {
                // Re-enable input and system shortcuts
                BlockInput(false);

                // Remove the low-level keyboard hook
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }

                // Show the cursor
                ShowCursor(true);

                // Restore cursor position
                SetCursorPos(_originalCursorPos.X, _originalCursorPos.Y);

                _isLocalInputDisabled = false;
                // Console.WriteLine("Local input enabled");

                // Restore the last active window
                if (_lastActiveWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(_lastActiveWindow);
                }
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isLocalInputDisabled && IsScreenCaptureViewActive())
            {
                int wParamInt = wParam.ToInt32();
                if (wParamInt == WM_KEYDOWN || wParamInt == WM_KEYUP ||
                    wParamInt == WM_SYSKEYDOWN || wParamInt == WM_SYSKEYUP)
                {
                    KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    // Check for Ctrl+Alt key combination - allow this to pass through for local input toggle
                    bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    bool isAlt = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;
                    
                    if (isCtrl && isAlt)
                    {
                        return CallNextHookEx(_hookID, nCode, wParam, lParam); // Allow Ctrl+Alt to pass through
                    }

                    // Check for system keys (Windows, Alt, Tab, etc.)
                    if (hookStruct.vkCode == (uint)Keys.LWin ||
                        hookStruct.vkCode == (uint)Keys.RWin ||
                        hookStruct.vkCode == (uint)Keys.Tab ||
                        hookStruct.vkCode == (uint)Keys.Escape ||
                        (hookStruct.flags & 0x20) == 0x20) // Check if it's an Alt key combination
                    {
                        // For key down events, send to remote machine
                        if (wParamInt == WM_KEYDOWN || wParamInt == WM_SYSKEYDOWN)
                        {
                            Keys key = (Keys)hookStruct.vkCode;
                            SendKeyToRemote(key, "keydown");
                        }
                        else if (wParamInt == WM_KEYUP || wParamInt == WM_SYSKEYUP)
                        {
                            Keys key = (Keys)hookStruct.vkCode;
                            SendKeyToRemote(key, "keyup");
                        }
                        
                        return (IntPtr)1; // Block the key locally
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // Add this helper method to send intercepted keys to the remote machine
        private void SendKeyToRemote(Keys keyCode, string actionType)
        {
            try
            {
                var action = new InputAction
                {
                    Type = "keyboard",
                    Action = actionType,
                    Key = keyCode.ToString(),
                    Modifiers = GetModifierKeys()
                };

                // Use Task.Run to avoid blocking the hook callback
                Task.Run(async () => 
                {
                    try
                    {
                        await _inputSender.SendInputAsync(action);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending key to remote: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error preparing key for remote: {ex.Message}");
            }
        }
    }
}