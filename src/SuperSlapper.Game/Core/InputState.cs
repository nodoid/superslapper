using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace SuperSlapper.Game.Core;

/// <summary>
/// One frame of input, already converted into the game's virtual 1920x1080
/// space. Mouse, touch and keyboard all feed the same verbs: tap, hold, drag.
/// </summary>
public sealed class InputState
{
    private KeyboardState _keys;
    private KeyboardState _previousKeys;
    private bool _leftDown;

    /// <summary>Pointer position in virtual pixels.</summary>
    public Vector2 Pointer { get; private set; }

    /// <summary>Pointer as the platform reported it, before the virtual mapping.</summary>
    public Vector2 RawPointer { get; private set; }

    public bool LeftDown { get; private set; }
    public bool LeftPressed { get; private set; }
    public bool LeftReleased { get; private set; }

    /// <summary>True on the frame a tap ended - the main "click" verb.</summary>
    public bool Tapped { get; private set; }

    public Vector2 DragDelta { get; private set; }
    public Vector2 PressOrigin { get; private set; }

    /// <summary>Horizontal flick speed in virtual pixels per second (0 when not swiping).</summary>
    public float SwipeX { get; private set; }

    public bool AnyKeyPressed { get; private set; }
    public bool BackPressed { get; private set; }
    public bool ConfirmPressed { get; private set; }
    public bool SpacePressed { get; private set; }

    public void Update(MouseState mouse, KeyboardState keyboard, TouchCollection touches, Matrix screenToVirtual)
    {
        _previousKeys = _keys;
        _keys = keyboard;

        var wasDown = _leftDown;
        var pointer = new Vector2(mouse.X, mouse.Y);
        var down = mouse.LeftButton == ButtonState.Pressed;

        if (touches.Count > 0)
        {
            var touch = touches[0];
            pointer = touch.Position;
            down = touch.State is TouchLocationState.Pressed or TouchLocationState.Moved;
            if (touch.State == TouchLocationState.Released)
            {
                pointer = touch.Position;
                down = false;
            }
        }

        RawPointer = pointer;
        Pointer = Vector2.Transform(pointer, screenToVirtual);

        var lastDown = _leftDown;
        _leftDown = down;
        LeftPressed = down && !lastDown;
        LeftReleased = !down && lastDown;
        LeftDown = down;
        Tapped = LeftReleased && Vector2.Distance(Pointer, PressOrigin) < 40f;
        DragDelta = Pointer - (PressOrigin == default ? Pointer : PressOrigin);

        if (LeftPressed)
        {
            PressOrigin = Pointer;
            SwipeX = 0f;
        }
        else if (LeftDown)
        {
            SwipeX = DragDelta.X;
        }

        AnyKeyPressed = _keys.GetPressedKeyCount() > 0;
        BackPressed = IsNewKey(Keys.Escape) || IsNewKey(Keys.Back) || IsNewKey(Keys.B);
        ConfirmPressed = IsNewKey(Keys.Enter) || IsNewKey(Keys.Space) && !SpacePressed;
        SpacePressed = IsNewKey(Keys.Space);
    }

    public bool IsNewKey(Keys key) => _keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

    public bool IsKeyDown(Keys key) => _keys.IsKeyDown(key);

    /// <summary>True when the tap landed inside a rectangle.</summary>
    public bool TappedIn(Rectangle rect) => Tapped && rect.Contains(Pointer.ToPoint());

    public bool HoveringOver(Rectangle rect) => rect.Contains(Pointer.ToPoint());

    /// <summary>Number of frames the pointer has been held - handy for charge meters.</summary>
    public float HoldTime { get; set; }

    /// <summary>Synthetic tap, used by the automated screen capture run.</summary>
    public void SimulateTap(Vector2 position)
    {
        RawPointer = position;
        Pointer = position;
        PressOrigin = position;
        LeftPressed = true;
        LeftReleased = true;
        LeftDown = false;
        Tapped = true;
        DragDelta = Vector2.Zero;
        SwipeX = 0f;
    }

    /// <summary>Synthetic press, held down until <see cref="SimulateRelease"/>.</summary>
    public void SimulatePress(Vector2 position)
    {
        SimulateIdle();
        RawPointer = position;
        Pointer = position;
        PressOrigin = position;
        LeftPressed = true;
        LeftDown = true;
    }

    /// <summary>Finger still down, no new edges - what a real hold looks like.</summary>
    public void SimulateHold(Vector2 position)
    {
        SimulateIdle();
        Pointer = position;
        LeftDown = true;
    }

    /// <summary>Synthetic release: this is the frame a real tap fires on.</summary>
    public void SimulateRelease(Vector2 position)
    {
        Pointer = position;
        LeftDown = false;
        LeftReleased = true;
        Tapped = Vector2.Distance(position, PressOrigin) < 40f;
    }

    /// <summary>Clears every edge so the next frame reads as no input at all.</summary>
    public void SimulateIdle()
    {
        LeftPressed = false;
        LeftReleased = false;
        LeftDown = false;
        Tapped = false;
        DragDelta = Vector2.Zero;
        SwipeX = 0f;
        BackPressed = false;
        ConfirmPressed = false;
        SpacePressed = false;
        AnyKeyPressed = false;
    }
}
