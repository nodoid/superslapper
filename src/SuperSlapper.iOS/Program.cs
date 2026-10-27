using Foundation;
using SuperSlapper.Game;
using UIKit;

namespace SuperSlapper.iOS;

/// <summary>iOS entry point. Runs the shared MonoGame game in a UIApplication.</summary>
[Register("AppDelegate")]
public class Program : UIApplicationDelegate
{
    private static SuperSlapperGame? _game;

    /// <summary>The platform window MonoGame builds for itself.</summary>
    internal static UIWindow? GameWindow =>
        _game?.Services.GetService(typeof(UIWindow)) as UIWindow;

    /// <summary>The view controller that hosts the game view.</summary>
    internal static UIViewController? GameViewController =>
        _game?.Services.GetService(typeof(UIViewController)) as UIViewController;

    /// <summary>The GL view itself, which is what has to end up on screen.</summary>
    internal static UIView? GameView => GameViewController?.View;

    internal static void RunGame()
    {
        var output = Environment.GetEnvironmentVariable("SUPERSLAPPER_SELFTEST");
        _game = new SuperSlapperGame();
        if (!string.IsNullOrWhiteSpace(output))
        {
            // A device launch has no writable working directory, so a relative
            // capture path is resolved inside the app's own Documents folder.
            _game.SelfTestOutputDirectory = Path.IsPathRooted(output)
                ? output
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), output);
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
}
