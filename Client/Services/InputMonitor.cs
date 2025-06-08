using Client.Models;
using Gma.System.MouseKeyHook;
using System;
using System.Windows.Forms;
using Client.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

namespace Client.Services
{
    /// <summary>
    /// Monitors and handles global keyboard and mouse input events
    /// </summary>
    public class InputMonitor : IDisposable
    {
        private IKeyboardMouseEvents _globalHook;
        private readonly SendInputServices _inputSender;
        private bool _isDisposed;
        private Stopwatch _lastMouseMoveTime;
        private const int MOUSE_MOVE_THROTTLE_MS = 16; // ~60fps
        private Point _lastMousePosition;

        // Event handlers
        private KeyEventHandler _keyDownHandler;
        private MouseEventHandler _mouseDownHandler;
        private MouseEventHandler _mouseUpHandler;
        private MouseEventHandler _mouseClickHandler;
        private MouseEventHandler _mouseDoubleClickHandler;
        private MouseEventHandler _mouseMoveHandler;
        private MouseEventHandler _mouseWheelHandler;

        /// <summary>
        /// Initializes a new instance of the InputMonitor class
        /// </summary>
        /// <param name="inputSender">Service responsible for sending input actions</param>
        /// <exception cref="ArgumentNullException">Thrown when inputSender is null</exception>
        public InputMonitor(SendInputServices inputSender)
        {
            _inputSender = inputSender ?? throw new ArgumentNullException(nameof(inputSender));
            _lastMouseMoveTime = new Stopwatch();
            _lastMouseMoveTime.Start();
            _lastMousePosition = new Point(0, 0);
        }

        /// <summary>
        /// Starts monitoring for input events
        /// </summary>
        /// <exception cref="Exception">Thrown when initialization fails</exception>
        public void Start()
        {
            if (_globalHook != null) return;

            try
            {
                _globalHook = Hook.GlobalEvents();

                // Initialize keyboard handler
                _keyDownHandler = async (s, e) =>
                {
                    var action = new InputAction
                    {
                        Type = "keyboard",
                        Action = "keydown",
                        Key = e.KeyCode.ToString()
                    };
                    try
                    {
                        await _inputSender.SendInputAsync(action);
                    }
                    catch (Exception ex)
                    {
                        Stop();
                        Console.WriteLine($"Error while sending keyboard action: {ex.Message}");
                    }
                };

                // Initialize mouse handlers
                _mouseDownHandler = CreateMouseEventHandler("mousedown");
                _mouseUpHandler = CreateMouseEventHandler("mouseup");
                _mouseClickHandler = CreateMouseEventHandler("click");
                _mouseDoubleClickHandler = CreateMouseEventHandler("doubleclick");
                _mouseMoveHandler = CreateMouseEventHandler("mousemove");
                _mouseWheelHandler = CreateMouseEventHandler("wheel");

                // Subscribe to events
                SubscribeToEvents();

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
                // For mouse move events, implement throttling
                if (actionName == "mousemove")
                {
                    // Check if enough time has passed since last move
                    if (_lastMouseMoveTime.ElapsedMilliseconds < MOUSE_MOVE_THROTTLE_MS)
                    {
                        return;
                    }

                    // Check if position has changed significantly (optional)
                    if (Math.Abs(e.X - _lastMousePosition.X) < 2 && 
                        Math.Abs(e.Y - _lastMousePosition.Y) < 2)
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
                    Button = e.Button.ToString(),
                    X = (int)relativeX,
                    Y = (int)relativeY,
                    Modifiers = GetModifierKeys()
                };

                try
                {
                    await _inputSender.SendInputAsync(action);
                }
                catch (Exception ex)
                {
                    Stop();
                    Console.WriteLine($"Error while sending mouse action: {ex.Message}");
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
            _globalHook.KeyDown += _keyDownHandler;
            _globalHook.MouseDown += _mouseDownHandler;
            _globalHook.MouseUp += _mouseUpHandler;
            _globalHook.MouseClick += _mouseClickHandler;
            _globalHook.MouseDoubleClick += _mouseDoubleClickHandler;
            _globalHook.MouseMove += _mouseMoveHandler;
            _globalHook.MouseWheel += _mouseWheelHandler;
        }

        /// <summary>
        /// Stops monitoring for input events and cleans up resources
        /// </summary>
        public void Stop()
        {
            if (_globalHook == null) return;

            try
            {
                UnsubscribeFromEvents();
                _globalHook.Dispose();
                _globalHook = null;

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
            _globalHook.KeyDown -= _keyDownHandler;
            _globalHook.MouseDown -= _mouseDownHandler;
            _globalHook.MouseUp -= _mouseUpHandler;
            _globalHook.MouseClick -= _mouseClickHandler;
            _globalHook.MouseDoubleClick -= _mouseDoubleClickHandler;
            _globalHook.MouseMove -= _mouseMoveHandler;
            _globalHook.MouseWheel -= _mouseWheelHandler;
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
                    Stop();
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
    }
}
