using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class InputService
{
    private static readonly ILogger Logger = Logging.Create<InputService>();

    private Point MouseCenter => new(_game.Window.ClientBounds.Width / 2, _game.Window.ClientBounds.Height / 2);

    private readonly Game _game;

    private readonly Dictionary<string, List<Binding>> _bindings;

    private KeyboardState _currentKeyboardState;

    private KeyboardState _previousKeyboardState;

    private MouseState _currentMouseState;

    private MouseState _previousMouseState;

    private bool _mouseCaptured;

    private bool _mouseWasCaptured;

    private bool _scrollCaptured;

    public InputService(Game game)
    {
        _game = game;
        _bindings = game.GetService<ContentService>()
            .Global.LoadJson<Dictionary<string, List<Binding>>>("InputActions");
        Logger.Information("Loaded {0} binding(s)", _bindings.Count);
    }

    public void AddAction(string action, params Keys[] keys)
    {
        GetLazyBindings(action).AddRange(keys.Select(k => new Binding(k)));
    }

    public void AddAction(string action, Keys key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        GetLazyBindings(action).Add(new Binding(key, ctrl, shift, alt));
    }

    public void EraseAction(string action)
    {
        _bindings.Remove(action);
    }

    public bool HasAction(string action)
    {
        return _bindings.ContainsKey(action);
    }

    public string GetActionBinding(string action, int index = 0)
    {
        var binding = _bindings[action][index];
        var strings = new List<string>();

        if (binding.Ctrl)
        {
            strings.Add("Ctrl");
        }

        if (binding.Shift)
        {
            strings.Add("Shift");
        }

        if (binding.Alt)
        {
            strings.Add("Alt");
        }

        strings.Add(binding.Key.ToString());

        return string.Join("+", strings);
    }

    public bool IsActionJustPressed(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyDown(k) && _previousKeyboardState.IsKeyUp(k));
    }

    public bool IsActionPressed(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyDown(k));
    }

    public bool IsActionJustReleased(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyUp(k) && _previousKeyboardState.IsKeyDown(k));
    }

    public float GetActionStrength(string action)
    {
        return IsActionJustPressed(action) || IsActionPressed(action) ? 1f : 0f;
    }

    public float GetActionAxis(string negative, string positive)
    {
        return GetActionStrength(positive) - GetActionStrength(negative);
    }

    public Vector2 GetActionsVector(string negativeX, string positiveX, string negativeY, string positiveY)
    {
        var vector = new Vector2(GetActionAxis(negativeX, positiveX), GetActionAxis(negativeY, positiveY));
        if (!Mathz.IsZeroApprox(vector.X) || !Mathz.IsZeroApprox(vector.Y))
        {
            vector.Normalize();
        }

        return vector;
    }

    public bool IsRightMousePressed()
    {
        return _currentMouseState.RightButton == ButtonState.Pressed;
    }

    public bool IsMiddleMousePressed()
    {
        return _currentMouseState.MiddleButton == ButtonState.Pressed;
    }

    public bool IsLeftMousePressed()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed;
    }

    public Vector2 GetMousePosition()
    {
        return new Vector2(_currentMouseState.X, _currentMouseState.Y);
    }

    public Vector2 GetMouseDelta()
    {
        var deltaX = _currentMouseState.X - _previousMouseState.X;
        var deltaY = _currentMouseState.Y - _previousMouseState.Y;
        return new Vector2(deltaX, deltaY);
    }

    public int GetScrollWheelDelta()
    {
        if (!_scrollCaptured)
        {
            return 0;
        }

        return _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
    }

    public void CaptureScroll(bool captured)
    {
        _scrollCaptured = captured;
    }

    public void CaptureMouse(bool captured)
    {
        _mouseCaptured = captured;
        ImGui.GetIO().WantCaptureMouse = !captured;
        _game.IsMouseVisible = !captured;
    }

    public void Update()
    {
        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();
        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();
        if (_mouseCaptured && _game.IsActive)
        {
            _previousMouseState = new MouseState(
                MouseCenter.X, MouseCenter.Y,
                _previousMouseState.ScrollWheelValue,
                _previousMouseState.LeftButton,
                _previousMouseState.MiddleButton,
                _previousMouseState.RightButton,
                _previousMouseState.XButton1,
                _previousMouseState.XButton2
            );
            // If capture just started this frame, also align current to center
            // so the delta is zero and the capture-start position doesn't bleed in.
            if (!_mouseWasCaptured)
            {
                _currentMouseState = new MouseState(
                    MouseCenter.X, MouseCenter.Y,
                    _currentMouseState.ScrollWheelValue,
                    _currentMouseState.LeftButton,
                    _currentMouseState.MiddleButton,
                    _currentMouseState.RightButton,
                    _currentMouseState.XButton1,
                    _currentMouseState.XButton2
                );
            }

            Mouse.SetPosition(MouseCenter.X, MouseCenter.Y);
        }

        _mouseWasCaptured = _mouseCaptured;
        _mouseCaptured = false;
    }

    private List<Binding> GetLazyBindings(string action)
    {
        if (!_bindings.TryGetValue(action, out var value))
        {
            value = new List<Binding>();
            _bindings[action] = value;
        }

        return value;
    }

    private bool CheckAction(string action, Func<Keys, bool> check)
    {
        if (!_bindings.TryGetValue(action, out var list))
        {
            return false;
        }

        foreach (var b in list)
        {
            if (!check(b.Key))
            {
                continue;
            }

            if (b.Ctrl && !IsCtrlDown())
            {
                continue;
            }

            if (b.Shift && !IsShiftDown())
            {
                continue;
            }

            if (b.Alt && !IsAltDown())
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsCtrlDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl);
    }

    private bool IsShiftDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift);
    }

    private bool IsAltDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftAlt) || _currentKeyboardState.IsKeyDown(Keys.RightAlt);
    }

    private record struct Binding(Keys Key, bool Ctrl = false, bool Shift = false, bool Alt = false);
}