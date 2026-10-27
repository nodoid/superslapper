using SuperSlapper.Game;

namespace SuperSlapper.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var game = new SuperSlapperGame();
        var settings = LaunchSettings.From(args);

        if (settings.SelfTestDirectory is not null)
        {
            game.SelfTestOutputDirectory = settings.SelfTestDirectory;
        }

        if (settings.Playthrough)
        {
            game.PlaythroughMode = true;
        }


        try
        {
            game.Run();
        }
        finally
        {
            game.Dispose();
        }
    }
}

/// <summary>
/// Command line switches, with a fallback to a marker file next to the executable.
/// The marker exists so the automated Windows smoke test can launch the game
/// through the shell without being able to pass arguments.
/// </summary>
public sealed class LaunchSettings
{
    public string? SelfTestDirectory { get; private set; }
    public bool Playthrough { get; private set; }

    public static LaunchSettings From(string[] args)
    {
        var settings = new LaunchSettings();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--selftest" or "-s" when i + 1 < args.Length:
                    settings.SelfTestDirectory = args[i + 1];
                    break;
                case "--playthrough":
                    settings.Playthrough = true;
                    break;
            }
        }

        var marker = Path.Combine(AppContext.BaseDirectory, "launch.txt");
        if (!File.Exists(marker))
        {
            return settings;
        }

        try
        {
            foreach (var line in File.ReadAllLines(marker))
            {
                var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                if (parts[0].Equals("selftest", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SelfTestDirectory = parts[1];
                }
                else if (parts[0].Equals("playthrough", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Playthrough = !parts[1].Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read launch.txt: {ex.Message}");
        }

        return settings;
    }
}
