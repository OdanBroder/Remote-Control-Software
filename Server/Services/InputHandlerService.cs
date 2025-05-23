using System.Text.Json;
using Server.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Server.Services
{
    public class InputHandlerService
    {
        private readonly ILogger<InputHandlerService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastInputTimes;
        private readonly TimeSpan _inputRateLimit = TimeSpan.FromMilliseconds(16); // ~60 FPS

        public InputHandlerService(ILogger<InputHandlerService> logger)
        {
            _logger = logger;
            _lastInputTimes = new ConcurrentDictionary<string, DateTime>();
        }

        public bool ValidateInput(InputAction action)
        {
            if (!IsInputAllowed(action.SessionIdentifier))
            {
                _logger.LogWarning($"Input rate limit exceeded for session {action.SessionIdentifier}");
                return false;
            }

            try
            {
                var inputData = JsonSerializer.Deserialize<InputActionData>(action.Action);
                if (inputData == null)
                {
                    _logger.LogWarning($"Invalid input data format in session {action.SessionIdentifier}");
                    return false;
                }

                // Validate input type
                if (inputData.Type.ToLower() != "mouse" && inputData.Type.ToLower() != "keyboard")
                {
                    _logger.LogWarning($"Unknown input type: {inputData.Type} in session {action.SessionIdentifier}");
                    return false;
                }

                UpdateLastInputTime(action.SessionIdentifier);
                return true;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Failed to deserialize input data in session {action.SessionIdentifier}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating input in session {action.SessionIdentifier}");
                return false;
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
    }

    public class InputActionData
    {
        public required string Type { get; set; }
        public required string Action { get; set; }
        public required string Key { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public string? Button { get; set; }
        public string[]? Modifiers { get; set; }
    }
}