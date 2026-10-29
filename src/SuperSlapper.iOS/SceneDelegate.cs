using Foundation;
using UIKit;

namespace SuperSlapper.iOS;

/// <summary>
/// iOS 26 kills apps that still use the pre-scene life cycle, so the scene has to
/// own a window. MonoGame builds its own window, view controller and GL view before
/// any scene exists, and the three only work as a piece, so the scene is handed to
/// MonoGame's window rather than a second window being built around its view.
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

        // The window goes up here and the game's view controller is moved into it as
        // soon as the game has one.
        Window = Program.ConnectScene(windowScene, this);
    }

    [Export("sceneDidBecomeActive:")]
    public void DidBecomeActive(UIScene scene) => Program.OnSceneActive();
}
