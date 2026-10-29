using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Units;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

public sealed class TitleScreen : Screen
{
    private Button? _unitButton;
    private readonly SoundToggle _music;
    private readonly SoundToggle _effects;
    private float _logoBob;

    public TitleScreen(GameContext context) : base(context)
    {
        // Clear of the logo and the art, in the one corner the screen leaves empty:
        // the logo stops at x 1550, so the column beside it is the only free space.
        _music = new SoundToggle(new Rectangle(1560, 40, 300, 84), context, AudioChannel.Music);
        _effects = new SoundToggle(new Rectangle(1560, 140, 300, 84), context, AudioChannel.Effects);
    }

    /// <summary>The button names the unit it will switch to.</summary>
    private string UnitActionLabel =>
        Loc.T(Context.Units == WeightUnit.Metric ? "title.changeToPounds" : "title.changeToKg");

    public override void OnEnter()
    {
        base.OnEnter();
        ClearControls();

        var hasSave = Context.Repository.Exists();
        var x = 90;
        var y = 606;

        Add(new Button(new Rectangle(x, y, 420, 92), Loc.T("title.newCareer"),
            () => Context.Screens.Push(new CreateCareerScreen(Context)), ButtonStyle.Normal));

        Add(new Button(new Rectangle(x, y + 100, 420, 92),
            Loc.T(hasSave ? "title.continue" : "title.noSave"),
            () =>
            {
                var loaded = Context.Repository.Load();
                if (loaded is null)
                {
                    return;
                }

                Context.State = loaded;
                Context.Screens.Push(new HubScreen(Context));
            })
        {
            Enabled = hasSave,
            Style = hasSave ? ButtonStyle.Normal : ButtonStyle.Ghost
        });

        Add(new Button(new Rectangle(x, y + 200, 420, 92), Loc.T("title.rules"),
            () => Context.Screens.Push(new RecordsScreen(Context, RecordsScreen.Tab.Rules)),
            ButtonStyle.Ghost));

        _unitButton = new Button(new Rectangle(x, y + 300, 420, 92), UnitActionLabel,
            () =>
            {
                Context.ToggleUnits();
                _unitButton!.Text = UnitActionLabel;
            }, ButtonStyle.Ghost);
        Add(_unitButton);

        // The sound chips draw and update through their own widget, so they are
        // declared as regions: the sweep presses them, the toggle handles the press.
        AddTarget(_music.ChipLabel, _music.Bounds);
        AddTarget(_effects.ChipLabel, _effects.Bounds);
    }

    public override void Update(GameTime time, InputState input)
    {
        _logoBob += (float)time.ElapsedGameTime.TotalSeconds;

        if (Elapsed > 0.4f)
        {
            foreach (var button in Buttons.ToArray())
            {
                button.Update(time, input, () => Context.Assets.Play("ui_click", 0.5f));
            }

            _music.Update(time, input);
            _effects.Update(time, input);
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        var logo = Context.Assets.Tex("logo");
        var bob = (float)Math.Sin(_logoBob * 1.6f) * 6f;
        var logoScale = 1180f / logo.Width;
        batch.Draw(logo, new Vector2(GameSettings.VirtualWidth / 2f, 290 + bob), null, Color.White, 0f,
            new Vector2(logo.Width / 2f, logo.Height / 2f), logoScale, SpriteEffects.None, 0f);

        // Clear of the logo, the fighters and the button column. No sanctioning
        // body names or "official" wording on the title screen.
        batch.Text(Context.Assets.Font("body"), Loc.T("title.tagline"),
            new Vector2(90, 520), Theme.GoldLight);

        // The slap landing, painted for the title. It bleeds off the right and
        // bottom edges so it reads as part of the scene, not a pasted card.
        batch.Draw(Context.Assets.Tex("hero_slap"), new Rectangle(700, 428, 1220, 651), Color.White);

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        _music.Draw(batch);
        _effects.Draw(batch);

        batch.Text(Context.Assets.Font("small"), GameSettings.Copyright,
            new Vector2(90, 1020), Theme.Muted);
    }
}
