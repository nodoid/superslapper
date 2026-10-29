using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using SuperSlapper.Game;
using UIKit;

namespace SuperSlapper.iOS;

/// <summary>iOS entry point. Runs the shared MonoGame game in a UIApplication.</summary>
[Register("AppDelegate")]
public class Program : UIApplicationDelegate
{
    /// <summary>The launch variable that starts the automated career on a phone.</summary>
    private const string SUPERSLAPPER_PLAYTHROUGH = "SUPERSLAPPER_PLAYTHROUGH";

    private static SuperSlapperGame? _game;

    /// <summary>The scene the app was launched into.</summary>
    private static UIWindowScene? _scene;

    /// <summary>The scene's window. It is built here and never swapped.</summary>
    private static UIWindow? _window;

    /// <summary>The window MonoGame builds for itself.</summary>
    internal static UIWindow? GameWindow =>
        _game?.Services.GetService(typeof(UIWindow)) as UIWindow;

    /// <summary>The controller that carries MonoGame's GL view.</summary>
    internal static UIViewController? GameViewController =>
        _game?.Services.GetService(typeof(UIViewController)) as UIViewController;

    /// <summary>
    /// Gives the app a window on the scene from the very first frame, and moves the
    /// game's view controller into it once the game has one.
    /// </summary>
    /// <remarks>
    /// The window is never replaced. iOS presents the scene's window, and a second
    /// window that arrives later - or one that is given a scene after it was already
    /// shown - can end up present but empty, which is the black screen that only
    /// turned up on some launches. MonoGame's own window is hidden instead, and its
    /// view controller carries the GL view, so moving the controller takes the whole
    /// drawing surface with it.
    /// </remarks>
    internal static UIWindow ConnectScene(UIWindowScene scene, SceneDelegate host)
    {
        _scene = scene;

        _window = new UIWindow(scene)
        {
            BackgroundColor = UIColor.FromRGB(0x0d, 0x15, 0x26),
        };
        _window.RootViewController = new UIViewController();
        _window.MakeKeyAndVisible();
        Trace("scene window up");

        // The game may already be up when the scene connects, in which case this puts
        // it straight in; otherwise Ready calls it again once the view exists.
        MoveGameIn();

        var left = 8;
        NSTimer.CreateRepeatingScheduledTimer(1.0, timer =>
        {
            if (left-- <= 0)
            {
                timer.Invalidate();
                return;
            }

            DumpScreenState();
        });

        return _window;
    }

    /// <summary>
    /// Resizes the game's view by one point and back.
    /// </summary>
    /// <remarks>
    /// MonoGame builds its GL framebuffer when the view changes size, and presents
    /// into that framebuffer. A view that arrives in the scene's window already the
    /// size it wants never changes size, so it keeps the framebuffer it made in
    /// MonoGame's own window - which nothing on screen presents. The screen is then
    /// black while the game itself draws perfectly, on whichever launches happened to
    /// settle before the move.
    /// </remarks>
    /// <summary>True while the view is being held one point narrow, mid-nudge.</summary>
    private static bool _nudging;

    private static void NudgeGameView()
    {
        if (_nudging || _window is not { } window || GameViewController?.View is not { } view)
        {
            return;
        }

        var bounds = window.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        _nudging = true;
        view.Frame = new CGRect(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height);
        window.LayoutIfNeeded();
        Trace($"nudge to {view.Frame.Size} drawable {Drawable(view)}");

        // The restore has to wait for the next run-loop turn. UIKit coalesces two
        // frame changes made in the same pass into one layout, so shrinking and
        // growing the view back in a single turn lays it out once, at the size it
        // already had - which is a resize MonoGame never sees, and a framebuffer it
        // never rebuilds.
        DispatchQueue.MainQueue.DispatchAfter(
            new DispatchTime(DispatchTime.Now, TimeSpan.FromMilliseconds(60)), () =>
            {
                _nudging = false;

                if (_window is not { } target || GameViewController?.View is not { } restored)
                {
                    return;
                }

                restored.Frame = target.Bounds;
                target.LayoutIfNeeded();
                Trace($"nudge back to {restored.Frame.Size} drawable {Drawable(restored)}");
            });
    }

    /// <summary>
    /// Size of the surface the GL layer will actually be given, in pixels. A layer
    /// that is not in an on-screen window has no drawable, and a game view drawing
    /// into one shows a black screen with everything else about the launch looking
    /// perfectly healthy.
    /// </summary>
    private static string Drawable(UIView? view)
    {
        if (view?.Layer is not CAEAGLLayer layer)
        {
            return "-";
        }

        // The drawable is the layer's bounds in device pixels; a layer that is not on
        // screen reports a zero scale.
        var scale = layer.ContentsScale;
        return $"{(int)(layer.Bounds.Width * scale)}x{(int)(layer.Bounds.Height * scale)} @{scale}";
    }

    /// <summary>
    /// One line a second on what is actually on screen: the app's state, whether the
    /// window is key and visible, and whether the game's view is in it. A launch that
    /// comes up black is otherwise completely silent.
    /// </summary>
    private static void DumpScreenState()
    {
        var app = UIApplication.SharedApplication;
        var view = GameViewController?.View;

        Trace($"screen app {app.ApplicationState} window " +
              $"{(view?.Window is null ? "none" : (view.Window.IsKeyWindow ? "key" : "not key"))} " +
              $"hidden={_window?.Hidden} root={_window?.RootViewController?.GetType().Name} " +
              $"view={view?.GetType().Name} frame={view?.Frame} inWindow={view?.Window is not null} " +
              $"layer={(view?.Layer.Bounds.Size.ToString() ?? "-")} drawable={Drawable(view)}");
    }

    /// <summary>Puts MonoGame's view controller in the scene's window, once it exists.</summary>
    private static void MoveGameIn()
    {
        if (_window is not { } window || GameViewController is not { } controller)
        {
            return;
        }

        if (ReferenceEquals(window.RootViewController, controller))
        {
            return;
        }

        try
        {
            if (controller.View is not { } gameView)
            {
                return;
            }

            window.RootViewController = controller;
            gameView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            window.MakeKeyAndVisible();

            // MonoGame's own window is outside the scene; left visible it stays key and
            // paints an empty screen over the game.
            GameWindow?.Hidden = true;

            NudgeGameView();
            Trace($"game view in the scene window {window.Bounds.Width}x{window.Bounds.Height}");
        }
        catch (Exception ex)
        {
            Trace("move failed: " + ex.Message);
        }
    }

    /// <summary>Where the session trace goes: the app's own container, so it can be read back.</summary>
    private static string TracePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "play.log");

    /// <summary>Appends one line to the session trace. Never throws: this is diagnosis.</summary>
    private static void Trace(string line)
    {
        try
        {
            File.AppendAllText(TracePath, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch
        {
            // A trace that cannot be written is not worth a crash on top.
        }
    }

    internal static void RunGame()
    {
        var output = Environment.GetEnvironmentVariable("SUPERSLAPPER_SELFTEST");
        _game = new SuperSlapperGame();

        try
        {
            File.WriteAllText(TracePath, $"launch {DateTime.Now:HH:mm:ss}{Environment.NewLine}");
        }
        catch
        {
        }

        _game.Trace = Trace;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Trace("UNHANDLED " + e.ExceptionObject);

        // The scene may have connected first; this is the other half of the handover.
        _game.Ready += MoveGameIn;
        if (!string.IsNullOrWhiteSpace(output))
        {
            // A device launch has no writable working directory, so a relative
            // capture path is resolved inside the app's own Documents folder.
            _game.SelfTestOutputDirectory = Path.IsPathRooted(output)
                ? output
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), output);
        }

        // The automated career: a launch environment variable is the only switch a
        // phone app gets, since simctl and devicectl both pass one. "season" plays the
        // whole year; anything else plays a short career of three bouts.
        var playthrough = Environment.GetEnvironmentVariable(SUPERSLAPPER_PLAYTHROUGH);
        if (!string.IsNullOrWhiteSpace(playthrough))
        {
            _game.SeasonMode = playthrough.Equals("season", StringComparison.OrdinalIgnoreCase);
            _game.PlaythroughMode = !_game.SeasonMode;
        }

        // The filmed run: a launch environment variable is the only switch a phone
        // app gets, since simctl and devicectl both pass one.
        var demo = Environment.GetEnvironmentVariable("SUPERSLAPPER_DEMO");
        if (!string.IsNullOrWhiteSpace(demo))
        {
            _game.DemoMode = true;

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var captures = Environment.GetEnvironmentVariable("SUPERSLAPPER_DEMO_CAPTURE");
            if (!string.IsNullOrWhiteSpace(captures))
            {
                _game.DemoCaptureDirectory = Path.IsPathRooted(captures)
                    ? captures
                    : Path.Combine(documents, captures);
            }

            // The recorder following the run reads this from the app container: it
            // is both the picture edit's clock and how the recorder knows the run
            // is over.
            var log = Environment.GetEnvironmentVariable("SUPERSLAPPER_DEMO_LOG");
            if (!string.IsNullOrWhiteSpace(log))
            {
                _game.DemoLogFile = Path.IsPathRooted(log) ? log : Path.Combine(documents, log);
            }
        }

        _game.Run();
    }

    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(Program));
    }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        RunGame();
        return true;
    }

    /// <summary>
    /// Called when the scene comes forward: from the background, or after a launch that
    /// connected the scene before the game was up. The window that holds the game is
    /// the one that has to be on screen, and the view has to have been through a resize
    /// for MonoGame to have a framebuffer the screen will show.
    /// </summary>
    internal static void OnSceneActive()
    {
        _window?.MakeKeyAndVisible();
        MoveGameIn();
        NudgeGameView();
        Trace("scene active");
    }
}
