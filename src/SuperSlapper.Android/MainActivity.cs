using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using SuperSlapper.Game;

namespace SuperSlapper.Android;

/// <summary>Android entry point. Full screen, landscape, no title bar.</summary>
[Activity(
    Label = "SuperSlapper",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    Theme = "@style/Theme.SuperSlapper",
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class MainActivity : AndroidGameActivity
{
    private SuperSlapperGame? _game;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _game = new SuperSlapperGame();
        var output = System.Environment.GetEnvironmentVariable("SUPERSLAPPER_SELFTEST");
        if (!string.IsNullOrWhiteSpace(output))
        {
            _game.SelfTestOutputDirectory = output;
        }

        _game.Window.AllowUserResizing = false;

        var view = (View)_game.Services.GetService(typeof(View))!;
        view.SystemUiVisibility = StatusBarVisibility.Hidden;
        SetContentView(view);
        _game.Run();
    }

    protected override void OnDestroy()
    {
        _game?.Dispose();
        _game = null;
        base.OnDestroy();
    }
}
