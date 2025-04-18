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
                        keybd_event((byte)keyCode, 0, 0, 0);
                        _logger.LogDebug($"Key pressed: {keyboardData.Key}");
                        break;
                    case "keyup":
                        keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, 0);
                        _logger.LogDebug($"Key released: {keyboardData.Key}");
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

        private int GetKeyCode(string key)
        {
            // This is a simplified version. In a real implementation, you would need
            // a comprehensive mapping of key names to virtual key codes
            return key.ToUpper() switch
            {
                "A" => 0x41,
                "B" => 0x42,
                "C" => 0x43,
                // Add more key mappings as needed
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
    }
}
