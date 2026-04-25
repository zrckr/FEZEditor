using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL = SDL3.SDL;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class InputService
{
    private static readonly ILogger Logger = Logging.Create<InputService>();

    public bool IsViewportHovered { private get; set; }

    public MouseState CurrentMouseState => _currentMouseState;

    public KeyboardState CurrentKeyboardState => _currentKeyboardState;

    private readonly Game _game;

    private readonly Dictionary<string, List<Binding>> _bindings;

    private KeyboardState _currentKeyboardState;

    private KeyboardState _previousKeyboardState;

    private MouseState _currentMouseState;

    private MouseState _previousMouseState;

    private bool _middleMouseButtonCaptured;

    private bool _rightMouseButtonCaptured;

    private Vector2 _mouseDelta;

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

    public bool CaptureMiddleMouseDelta(out Vector2 delta)
    {
        _middleMouseButtonCaptured = CaptureMouseDelta(_currentMouseState.MiddleButton == ButtonState.Pressed, out delta);
        return _middleMouseButtonCaptured;
    }

    public bool CaptureRightMouseDelta(out Vector2 delta)
    {
        _rightMouseButtonCaptured = CaptureMouseDelta(_currentMouseState.RightButton == ButtonState.Pressed, out delta);
        return _rightMouseButtonCaptured;
    }

    private bool CaptureMouseDelta(bool pressed, out Vector2 delta)
    {
        if (pressed && _game.IsActive && IsViewportHovered)
        {
            var bounds = _game.Window.ClientBounds;
            var width = MathF.Round(bounds.Width / 2f);
            var height = MathF.Round(bounds.Height / 2f);
            SDL.SDL_WarpMouseInWindow(_game.Window.Handle, width, height);
            delta = _mouseDelta;
            return true;
        }

        delta = Vector2.Zero;
        return false;
    }

    public bool CaptureScrollWheelDelta(out float delta)
    {
        if (_game.IsActive && IsViewportHovered)
        {
            delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            return !Mathz.IsZeroApprox(delta);
        }

        delta = 0f;
        return false;
    }

    public void Update()
    {
        // Read relative delta BEFORE Mouse.GetState(): FNA's GetMouseState also calls
        // SDL_GetRelativeMouseState, which drains the accumulator.
        SDL.SDL_GetRelativeMouseState(out var dx, out var dy);
        _mouseDelta = new Vector2(dx, dy);
        _game.IsMouseVisible = !_rightMouseButtonCaptured && !_middleMouseButtonCaptured;

        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();
        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();
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
        if (ImGui.GetIO().WantCaptureKeyboard || !_bindings.TryGetValue(action, out var list))
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