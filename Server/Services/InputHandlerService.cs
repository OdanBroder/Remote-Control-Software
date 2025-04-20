using System.Text.Json;
using Server.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Concurrent;

namespace Server.Services
{
    public class InputHandlerService
    {
        private readonly ILogger<InputHandlerService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastInputTimes;
        private readonly TimeSpan _inputRateLimit = TimeSpan.FromMilliseconds(16); // ~60 FPS
        private readonly object _inputLock = new object();

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        // Mouse event constants
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        // Keyboard event constants
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public InputHandlerService(ILogger<InputHandlerService> logger)
        {
            _logger = logger;
            _lastInputTimes = new ConcurrentDictionary<string, DateTime>();
        }

        public void ProcessInput(InputAction action)
        {
            if (!IsInputAllowed(action.SessionIdentifier))
            {
                _logger.LogWarning($"Input rate limit exceeded for session {action.SessionIdentifier}");
                return;
            }

            try
            {
                var inputData = JsonSerializer.Deserialize<InputData>(action.Action);
                if (inputData == null)
                {
                    _logger.LogWarning($"Invalid input data format in session {action.SessionIdentifier}");
                    return;
                }

                lock (_inputLock)
                {
                    switch (inputData.Type.ToLower())
                    {
                        case "mouse":
                            ProcessMouseInput(inputData);
                            break;
                        case "keyboard":
                            ProcessKeyboardInput(inputData);
                            break;
                        default:
                            _logger.LogWarning($"Unknown input type: {inputData.Type} in session {action.SessionIdentifier}");
                            break;
                    }
                }

                UpdateLastInputTime(action.SessionIdentifier);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Failed to deserialize input data in session {action.SessionIdentifier}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing input in session {action.SessionIdentifier}");
            }
        }

        private bool IsInputAllowed(string sessionIdentifier)
        {
            if (_lastInputTimes.TryGetValue(sessionIdentifier, out var lastInputTime))
            {
                var timeSinceLastInput = DateTime.UtcNow - lastInputTime;
                return timeSinceLastInput >= _inputRateLimit;
            }
            return true;
        }

        private void UpdateLastInputTime(string sessionIdentifier)
        {
            _lastInputTimes.AddOrUpdate(sessionIdentifier, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }

        private void ProcessMouseInput(InputData inputData)
        {
            try
            {
                var mouseData = JsonSerializer.Deserialize<MouseInputData>(inputData.Data);
                if (mouseData == null)
                {
                    _logger.LogWarning("Invalid mouse input data format");
                    return;
                }

                switch (mouseData.Action.ToLower())
                {
                    case "move":
                        SetCursorPos(mouseData.X, mouseData.Y);
                        _logger.LogDebug($"Mouse moved to X: {mouseData.X}, Y: {mouseData.Y}");
                        break;
                    case "click":
                        ProcessMouseClick(mouseData);
                        break;
                    case "scroll":
                        ProcessMouseScroll(mouseData);
                        break;
                    default:
                        _logger.LogWarning($"Unknown mouse action: {mouseData.Action}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize mouse input data");
            }
        }

        private void ProcessMouseClick(MouseInputData mouseData)
        {
            uint downFlag = mouseData.Button?.ToLower() switch
            {
                "left" => MOUSEEVENTF_LEFTDOWN,
                "right" => MOUSEEVENTF_RIGHTDOWN,
                "middle" => MOUSEEVENTF_MIDDLEDOWN,
                _ => 0
            };

            uint upFlag = mouseData.Button?.ToLower() switch
            {
                "left" => MOUSEEVENTF_LEFTUP,
                "right" => MOUSEEVENTF_RIGHTUP,
                "middle" => MOUSEEVENTF_MIDDLEUP,
                _ => 0
            };

            if (downFlag != 0 && upFlag != 0)
            {
                SetCursorPos(mouseData.X, mouseData.Y);
                mouse_event(downFlag, mouseData.X, mouseData.Y, 0, 0);
                Thread.Sleep(10); // Small delay between down and up events
                mouse_event(upFlag, mouseData.X, mouseData.Y, 0, 0);
                _logger.LogDebug($"Mouse {mouseData.Button} clicked at X: {mouseData.X}, Y: {mouseData.Y}");
            }
        }

        private void ProcessMouseScroll(MouseInputData mouseData)
        {
            if (mouseData.Delta.HasValue)
            {
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)mouseData.Delta.Value, 0);
                _logger.LogDebug($"Mouse scrolled {mouseData.Delta} units");
            }
        }

        private void ProcessKeyboardInput(InputData inputData)
        {
            try
            {
                var keyboardData = JsonSerializer.Deserialize<KeyboardInputData>(inputData.Data);
                if (keyboardData == null)
                {
                    _logger.LogWarning("Invalid keyboard input data format");
                    return;
                }

                var keyCode = GetKeyCode(keyboardData.Key);
                if (keyCode == 0)
                {
                    _logger.LogWarning($"Unknown key: {keyboardData.Key}");
                    return;
                }

                switch (keyboardData.Action.ToLower())
                {
                    case "keydown":
                        // Handle modifier keys first
                        if (keyboardData.Modifiers != null)
                        {
                            foreach (var modifier in keyboardData.Modifiers)
                            {
                                var modifierCode = GetKeyCode(modifier);
                                if (modifierCode != 0)
                                {
                                    keybd_event((byte)modifierCode, 0, 0, 0);
                                }
                            }
                        }

                        // If the key is uppercase and not a modifier, press SHIFT
                        if (keyboardData.Key.Length == 1 && char.IsUpper(keyboardData.Key[0]) && 
                            !IsModifierKey(keyboardData.Key))
                        {
                            keybd_event((byte)GetKeyCode("SHIFT"), 0, 0, 0);
                        }

                        // Press the actual key
                        keybd_event((byte)keyCode, 0, 0, 0);
                        _logger.LogDebug($"Key pressed: {keyboardData.Key} with modifiers: {string.Join(",", keyboardData.Modifiers ?? Array.Empty<string>())}");
                        break;

                    case "keyup":
                        // Release the key
                        keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, 0);

                        // Release SHIFT if it was an uppercase letter
                        if (keyboardData.Key.Length == 1 && char.IsUpper(keyboardData.Key[0]) && 
                            !IsModifierKey(keyboardData.Key))
                        {
                            keybd_event((byte)GetKeyCode("SHIFT"), 0, KEYEVENTF_KEYUP, 0);
                        }

                        // Release modifier keys
                        if (keyboardData.Modifiers != null)
                        {
                            foreach (var modifier in keyboardData.Modifiers)
                            {
                                var modifierCode = GetKeyCode(modifier);
                                if (modifierCode != 0)
                                {
                                    keybd_event((byte)modifierCode, 0, KEYEVENTF_KEYUP, 0);
                                }
                            }
                        }
                        _logger.LogDebug($"Key released: {keyboardData.Key} with modifiers: {string.Join(",", keyboardData.Modifiers ?? Array.Empty<string>())}");
                        break;

                    default:
                        _logger.LogWarning($"Unknown keyboard action: {keyboardData.Action}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize keyboard input data");
            }
        }

        private bool IsModifierKey(string key)
        {
            return key.ToUpper() switch
            {
                "SHIFT" => true,
                "CTRL" => true,
                "ALT" => true,
                "WIN" => true,
                _ => false
            };
        }

        private int GetKeyCode(string key)
        {
            return key.ToUpper() switch
            {
                // Alphabet keys
                "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
                "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
                "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
                "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
                "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59,
                "Z" => 0x5A,

                // Number keys
                "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
                "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,

                // Function keys
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,

                // Special keys
                "ESCAPE" => 0x1B, "TAB" => 0x09, "CAPSLOCK" => 0x14,
                "SHIFT" => 0x10, "CTRL" => 0x11, "ALT" => 0x12,
                "SPACE" => 0x20, "ENTER" => 0x0D, "BACKSPACE" => 0x08,
                "DELETE" => 0x2E, "INSERT" => 0x2D, "HOME" => 0x24,
                "END" => 0x23, "PAGEUP" => 0x21, "PAGEDOWN" => 0x22,

                // Arrow keys
                "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28,

                // Numpad keys
                "NUMPAD0" => 0x60, "NUMPAD1" => 0x61, "NUMPAD2" => 0x62,
                "NUMPAD3" => 0x63, "NUMPAD4" => 0x64, "NUMPAD5" => 0x65,
                "NUMPAD6" => 0x66, "NUMPAD7" => 0x67, "NUMPAD8" => 0x68,
                "NUMPAD9" => 0x69,
                "NUMPADMULTIPLY" => 0x6A, "NUMPADADD" => 0x6B,
                "NUMPADSUBTRACT" => 0x6D, "NUMPADDECIMAL" => 0x6E,
                "NUMPADDIVIDE" => 0x6F,

                // Punctuation and symbols
                "SEMICOLON" => 0xBA, "EQUALS" => 0xBB, "COMMA" => 0xBC,
                "MINUS" => 0xBD, "PERIOD" => 0xBE, "SLASH" => 0xBF,
                "BACKQUOTE" => 0xC0, "LEFTBRACKET" => 0xDB,
                "BACKSLASH" => 0xDC, "RIGHTBRACKET" => 0xDD,
                "QUOTE" => 0xDE,

                _ => 0
            };
        }
    }

    public class InputData
    {
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    public class MouseInputData
    {
        public string Action { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public string? Button { get; set; }
        public int? Delta { get; set; }
    }

    public class KeyboardInputData
    {
        public string Action { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string[]? Modifiers { get; set; }
    }
}
