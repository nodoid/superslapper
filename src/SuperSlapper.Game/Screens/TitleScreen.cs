using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Units;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

public sealed class TitleScreen : Screen
{
    private readonly List<Button> _buttons = new();
    private Button? _unitButton;
    private float _logoBob;

    public TitleScreen(GameContext context) : base(context)
    {
    }

    /// <summary>The button names the unit it will switch to.</summary>
    private string UnitActionLabel =>
        Context.Units == WeightUnit.Metric ? "CHANGE TO POUNDS" : "CHANGE TO KG";

    public override void OnEnter()
    {
        base.OnEnter();
        _buttons.Clear();

        var hasSave = Context.Repository.Exists();
        var x = 90;
        var y = 606;

        _buttons.Add(new Button(new Rectangle(x, y, 420, 92), "NEW CAREER",
            () => Context.Screens.Push(new CreateCareerScreen(Context)), ButtonStyle.Normal));

        _buttons.Add(new Button(new Rectangle(x, y + 100, 420, 92), hasSave ? "CONTINUE" : "NO SAVED CAREER",
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

        _buttons.Add(new Button(new Rectangle(x, y + 200, 420, 92), "RULES & DIVISIONS",
            () => Context.Screens.Push(new RecordsScreen(Context, RecordsScreen.Tab.Rules)),
            ButtonStyle.Ghost));

        _unitButton = new Button(new Rectangle(x, y + 300, 420, 92), UnitActionLabel,
            () =>
            {
                Context.ToggleUnits();
                _unitButton!.Text = UnitActionLabel;
            }, ButtonStyle.Ghost);
        _buttons.Add(_unitButton);
    }

    public override void Update(GameTime time, InputState input)
    {
        _logoBob += (float)time.ElapsedGameTime.TotalSeconds;

        if (Elapsed > 0.4f)
        {
            foreach (var button in _buttons.ToArray())
            {
                button.Update(time, input, () => Context.Assets.Play("ui_click", 0.5f));
            }
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
        batch.Text(Context.Assets.Font("body"), "THE SLAP FIGHTING CHAMPIONSHIP",
            new Vector2(90, 520), Theme.GoldLight);

        // The slap landing, painted for the title. It bleeds off the right and
        // bottom edges so it reads as part of the scene, not a pasted card.
        batch.Draw(Context.Assets.Tex("hero_slap"), new Rectangle(700, 428, 1220, 651), Color.White);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        batch.Text(Context.Assets.Font("small"), GameSettings.Copyright,
            new Vector2(90, 1020), Theme.Muted);
    }
}
