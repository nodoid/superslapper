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
    /// <summary>
    /// Watches real touches, when a head wants a record of them. Only taps that come
    /// from the device are traced: the scripted taps the tests and the demo inject
    /// go through the Simulate* methods and are silent here.
    /// </summary>
    public static Action<string>? Trace { get; set; }

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

    /// <param name="screenToVirtual">
    /// Inverse of the letterbox transform the frame is drawn with: drawable pixels
    /// back to the 1920x1080 virtual frame.
    /// </param>
    /// <param name="drawable">
    /// Size of the surface actually being drawn into. The touch panel has its own
    /// size and stretches over the whole screen, so a touch has to be scaled to the
    /// drawable before it can be mapped into the virtual frame.
    /// </param>
    public void Update(MouseState mouse, KeyboardState keyboard, TouchCollection touches,
        Matrix screenToVirtual, Point drawable)
    {
        _previousKeys = _keys;
        _keys = keyboard;

        var wasDown = _leftDown;
        var pointer = new Vector2(mouse.X, mouse.Y);
        var down = mouse.LeftButton == ButtonState.Pressed;
        var fromTouch = touches.Count > 0;

        if (fromTouch)
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

        Pointer = fromTouch
            ? FromTouchPanel(pointer, drawable, screenToVirtual)
            : Vector2.Transform(pointer, screenToVirtual);

        var lastDown = _leftDown;
        _leftDown = down;
        LeftPressed = down && !lastDown;
        LeftReleased = !down && lastDown;
        LeftDown = down;
        Tapped = LeftReleased && Vector2.Distance(Pointer, PressOrigin) < 40f;

        // Every edge, not just completed taps: a finger that goes down and never
        // comes back up is a different bug from a finger the game never sees.
        if (LeftPressed || LeftReleased)
        {
            var state = touches.Count > 0 ? touches[0].State.ToString() : "mouse";
            Trace?.Invoke($"{(LeftPressed ? "down" : "up")} {state} raw " +
                          $"{RawPointer.X:0},{RawPointer.Y:0} -> virtual {Pointer.X:0},{Pointer.Y:0}" +
                          (Tapped ? " tap" : ""));
        }

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

    /// <summary>
    /// Maps a touch panel position onto the game's virtual frame.
    /// </summary>
    /// <remarks>
    /// A finger is not a mouse, but it is not the drawing frame either. The touch
    /// panel reports its own space - on iOS the 1920x1080 frame the game asks for -
    /// stretched over the whole screen, while the frame itself is letterboxed into a
    /// 2532x1170 phone. So a touch is scaled to the drawable first and only then put
    /// through the same screen-to-virtual matrix as the mouse. Mapping the touch
    /// space straight onto the virtual frame skips the letterbox, which shifts every
    /// tap right of where the finger landed: on the hub, the middle of TRAINING
    /// lands on RANKINGS.
    /// </remarks>
    private static Vector2 FromTouchPanel(Vector2 position, Point drawable, Matrix screenToVirtual)
    {
        var width = TouchPanel.DisplayWidth > 0 ? TouchPanel.DisplayWidth : GameSettings.VirtualWidth;
        var height = TouchPanel.DisplayHeight > 0 ? TouchPanel.DisplayHeight : GameSettings.VirtualHeight;

        var onScreen = new Vector2(position.X * drawable.X / width,
            position.Y * drawable.Y / height);

        return Vector2.Transform(onScreen, screenToVirtual);
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
