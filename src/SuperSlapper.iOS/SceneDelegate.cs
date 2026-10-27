using Foundation;
using UIKit;

namespace SuperSlapper.iOS;

/// <summary>
/// iOS 26 kills apps that still use the pre-scene life cycle, so the scene owns
/// the window and MonoGame's view controller is moved into it.
///
/// MonoGame builds its own UIWindow when the game is constructed, before any
/// scene exists. That window is detached from the scene hierarchy, so its
/// content is re-homed here and the leftover window is hidden - otherwise it
/// stays key and paints an empty screen over the game.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
        {
            return;
        }

        var window = new UIWindow(windowScene)
        {
            BackgroundColor = UIColor.FromRGB(0x0d, 0x15, 0x26),
        };
        Window = window;

        var host = new UIViewController();
        window.RootViewController = host;

        var gameView = Program.GameView;
        if (gameView != null)
        {
            // The view is taken rather than its controller: MonoGame has already
            // made that controller the root of its own window, and UIKit refuses
            // to reparent a root view controller as a child.
            var hostView = host.View!;
            gameView.Frame = hostView.Bounds;
            gameView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            hostView.AddSubview(gameView);
            Program.GameWindow?.Hidden = true;
        }

        window.MakeKeyAndVisible();
    }
}
