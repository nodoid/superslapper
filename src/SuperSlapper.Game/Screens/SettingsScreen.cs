using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Units;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// The settings sheet: the units the weights read in, and a level for the music
/// and for the effects. Every row shows all of its options with the one in force
/// lit, and each choice is written the moment it is made - there is no confirm
/// button to forget.
/// </summary>
public sealed class SettingsScreen : Screen
{
    private static readonly Rectangle Panel = new(80, 190, 1760, 620);


    public SettingsScreen(GameContext context) : base(context) => Rebuild();

    /// <summary>Puts the chips back with the current choices lit.</summary>
    private void Rebuild()
    {
        ClearControls();

        foreach (var channel in new[] { AudioChannel.Music, AudioChannel.Effects })
        {
            for (var level = 0; level < PlayerSettings.Levels.Length; level++)
            {
                var chosen = level;
                Add(new Button(ChipRect(channel == AudioChannel.Music ? 0 : 1, level),
                    Loc.T($"sound.level.{level}"), () => ChooseLevel(channel, chosen))
                {
                    Style = Context.Settings.For(channel).Index == level
                        ? ButtonStyle.Normal
                        : ButtonStyle.Ghost
                });
            }
        }

        foreach (var units in new[] { WeightUnit.Metric, WeightUnit.Imperial })
        {
            var chosen = units;
            Add(new Button(UnitsRect(units), UnitsLabel(units), () => ChooseUnits(chosen))
            {
                Style = Context.Units == units ? ButtonStyle.Normal : ButtonStyle.Ghost
            });
        }

        Add(new Button(new Rectangle(80, 890, 320, 120), Loc.T("common.back"),
            () => Context.Screens.Pop(), ButtonStyle.Ghost));
    }

    private static string UnitsLabel(WeightUnit units) =>
        Loc.T(units == WeightUnit.Metric ? "units.metric" : "units.imperial");

    private static Rectangle ChipRect(int row, int index) =>
        new(600 + index * 300, 235 + row * 150, 280, 84);

    private static Rectangle UnitsRect(WeightUnit units) =>
        new(units == WeightUnit.Metric ? 600 : 900, 535, 280, 84);

    private void ChooseLevel(AudioChannel channel, int level)
    {
        Context.SetLevel(channel, level);

        // The effects row plays its own confirmation rather than the usual click,
        // so what the player hears is the level they just picked.
        Context.Assets.Play(channel == AudioChannel.Effects ? "ui_confirm" : "ui_click", 0.6f);
        Rebuild();
    }

    private void ChooseUnits(WeightUnit units)
    {
        Context.SetUnits(units);
        Context.Assets.Play("ui_click", 0.5f);
        Rebuild();
    }

    public override void Update(GameTime time, InputState input)
    {
        // Settings belong to the player, not to a career: this screen is usable
        // with no save loaded at all.
        foreach (var button in Buttons.ToArray())
        {
            button.Update(time, input);
        }

        if (input.BackPressed)
        {
            Context.Screens.Pop();
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 130), new Color(8, 12, 22) * 0.9f);
        batch.Text(Context.Assets.Font("large"), Loc.T("settings.title"), new Vector2(80, 26), Theme.Gold);
        batch.Text(Context.Assets.Font("small"), Loc.T("settings.subtitle"),
            new Vector2(82, 96), Theme.Muted);

        batch.NineSlice(Context.Assets.Tex("panel"), Panel, 40, Color.White);

        DrawRow(batch, 235, Loc.T("settings.music"), Loc.T("settings.musicNote"));
        DrawRow(batch, 385, Loc.T("settings.effects"), Loc.T("settings.effectsNote"));
        DrawRow(batch, 535, Loc.T("settings.units"), Loc.T("settings.unitsNote"));

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    /// <summary>What the row is, and what changing it does.</summary>
    private void DrawRow(SpriteBatch batch, int y, string title, string explanation)
    {
        batch.Text(Context.Assets.Font("medium"), title, new Vector2(Panel.Left + 60, y), Theme.White);
        batch.Text(Context.Assets.Font("small"), explanation,
            new Vector2(Panel.Left + 60, y + 46), Theme.Muted);
    }
}
