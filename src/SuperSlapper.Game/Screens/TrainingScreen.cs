using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Training;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Training camp. Every day of the week is a session: weights, roadwork, aim
/// drills, power pads, recovery work or a weight cut. Fatigue, injuries and
/// wasted training all live here.
/// </summary>
public sealed class TrainingScreen : Screen
{
    private readonly List<Button> _buttons = new();
    private string _toast = "";
    private Color _toastColour = Theme.White;
    private float _toastTimer;

    public TrainingScreen(GameContext context) : base(context)
    {
    }

    private CareerState State => Context.State!;

    public override void OnEnter()
    {
        base.OnEnter();
        Rebuild();
    }

    private void Rebuild()
    {
        _buttons.Clear();

        // Two columns of sessions, skipping rest (which gets its own big button).
        var activities = TrainingCatalog.All.Where(a => a.Id != "rest").ToList();
        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            var col = i % 2;
            var row = i / 2;
            var rect = new Rectangle(80 + col * 420, 210 + row * 118, 400, 104);

            _buttons.Add(new Button(rect, activity.Name, () => Train(activity), ButtonStyle.Normal)
            {
                SubText = $"{activity.EnergyCost} energy · {FocusName(activity.Focus)}",
                IconKey = activity.IconKey
            });
        }

        _buttons.Add(new Button(new Rectangle(80, 890, 400, 120), "REST DAY",
            () => Train(TrainingCatalog.Rest), ButtonStyle.Ghost)
        {
            IconKey = "icon_medkit",
            SubText = "Recover energy, heal nothing"
        });

        _buttons.Add(new Button(new Rectangle(500, 890, 320, 120), "BACK TO HUB",
            () =>
            {
                Context.Persist();
                Context.Screens.Pop();
            }, ButtonStyle.Ghost));
    }

    private static string FocusName(TrainingFocus focus) => focus switch
    {
        TrainingFocus.Strength => "power",
        TrainingFocus.Endurance => "endurance",
        TrainingFocus.Aim => "aim",
        TrainingFocus.SlapPower => "slap power",
        TrainingFocus.Reflex => "reflex",
        TrainingFocus.Recovery => "recovery",
        TrainingFocus.WeightCut => "weight cut",
        _ => "study"
    };

    private void Train(TrainingActivity activity)
    {
        var state = State;
        if (state.Offer is not null && state.Day >= state.Offer.FightDay)
        {
            ShowToast("It is fight day. Go and weigh in.", Theme.RedLight);
            return;
        }

        var before = state.Player.Energy;
        var result = Context.Career.TrainDay(state, activity);
        Context.Persist();

        if (!result.MadeProgress && result.Injury is null && result.EnergyAfter == before)
        {
            ShowToast("Too tired to train - take a rest day.", Theme.RedLight);
            return;
        }

        Context.Assets.Play("ui_confirm", 0.4f);

        if (result.Injury is not null)
        {
            ShowToast($"{result.Injury.DisplayName}: out {result.Injury.DaysRemaining} days", Theme.RedLight);
        }
        else if (result.MadeProgress)
        {
            ShowToast(result.GainSummary, Theme.Green);
        }
        else
        {
            ShowToast("No measurable gain. Rest and try again.", Theme.Muted);
        }
    }

    private void ShowToast(string text, Color colour)
    {
        _toast = text;
        _toastColour = colour;
        _toastTimer = 3.2f;
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        if (_toastTimer > 0)
        {
            _toastTimer -= dt;
        }

        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer)
        {
            DrawNoCareer(batch);
            return;
        }

        var state = State;
        var player = state.Player;

        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Text(Context.Assets.Font("large"), "TRAINING CAMP", new Vector2(80, 40), Theme.Gold);
        batch.Text(Context.Assets.Font("small"),
            $"Day {state.Day} · {Context.UnitLabel} · " +
            (state.Offer is null ? "no bout booked" : $"fight in {Math.Max(0, state.Offer.DaysUntilFight(state.Day))} days"),
            new Vector2(80, 130), Theme.Muted);

        // Condition strip.
        StatBar.Draw(batch, Context.Assets, new Rectangle(900, 60, 300, 22), "ENERGY", player.Energy, Theme.Green);
        StatBar.Draw(batch, Context.Assets, new Rectangle(1240, 60, 300, 22), "HEAD DAMAGE", player.Damage, Theme.Amber);
        StatBar.Draw(batch, Context.Assets, new Rectangle(1580, 60, 260, 22), "POPULARITY", player.Popularity, Theme.Gold);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        DrawSidePanel(batch, state, player);

        if (_toastTimer > 0)
        {
            var alpha = Math.Clamp(_toastTimer, 0f, 1f);
            var rect = new Rectangle(80, 160, 800, 40);
            batch.Fill(rect, Theme.Ink * (0.75f * alpha));
            batch.Text(Context.Assets.Font("body"), _toast, new Vector2(96, rect.Top + 2), _toastColour * alpha);
        }

        // Fighter preview.
        Gfx.Frame(batch, Context.Assets.Tex("fighter_player"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.Idle(Elapsed), new Vector2(1700, 900), Color.White, 0.95f);
    }

    private void DrawSidePanel(SpriteBatch batch, CareerState state, Fighter player)
    {
        var rect = new Rectangle(980, 210, 860, 690);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        batch.Text(Context.Assets.Font("small"), "CAMP LOG", new Vector2(rect.Left + 36, rect.Top + 24), Theme.Gold);

        var log = state.TrainingLog.TakeLast(7).Reverse().ToList();
        var y = rect.Top + 66;
        foreach (var entry in log)
        {
            batch.Text(Context.Assets.Font("small"), $"Day {entry.Day}", new Vector2(rect.Left + 36, y), Theme.Muted);
            batch.Text(Context.Assets.Font("small"), entry.ActivityName, new Vector2(rect.Left + 140, y), Theme.White);
            batch.Text(Context.Assets.Font("small"),
                string.IsNullOrWhiteSpace(entry.GainSummary) ? "-" : entry.GainSummary,
                new Vector2(rect.Left + 430, y), entry.Overtrained ? Theme.Amber : Theme.Green);
            if (entry.Injury is not null)
            {
                batch.Text(Context.Assets.Font("tiny"), entry.Injury, new Vector2(rect.Right - 40, y + 2),
                    Theme.RedLight, 1f, TextAlign.Right);
            }

            y += 30;
        }

        if (log.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), "No sessions yet. Pick a drill on the left.",
                new Vector2(rect.Left + 36, y + 10), Theme.Muted);
        }

        // Injuries.
        var injY = rect.Top + 330;
        batch.Text(Context.Assets.Font("small"), "INJURY REPORT", new Vector2(rect.Left + 36, injY), Theme.Gold);
        var injuries = player.ActiveInjuries.ToList();
        if (injuries.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), "Clean bill of health.",
                new Vector2(rect.Left + 36, injY + 40), Theme.Green);
        }
        else
        {
            var row = 0;
            foreach (var injury in injuries)
            {
                var iy = injY + 40 + row * 34;
                batch.Text(Context.Assets.Font("small"),
                    $"{injury.DisplayName} ({injury.SeverityText}) - {injury.DaysRemaining} days",
                    new Vector2(rect.Left + 36, iy), Theme.RedLight);
                batch.Text(Context.Assets.Font("tiny"),
                    "affects " + string.Join(", ", injury.AffectedAttributes),
                    new Vector2(rect.Right - 40, iy + 4), Theme.Muted, 1f, TextAlign.Right);
                row++;
            }
        }

        // Weight watch - the training/weigh-in link.
        var wtY = rect.Top + 500;
        batch.Text(Context.Assets.Font("small"), "WEIGHT WATCH", new Vector2(rect.Left + 36, wtY), Theme.Gold);
        var division = SuperSlapper.Core.Rules.WeightClasses.Get(state.PlayerDivision);
        batch.Text(Context.Assets.Font("body"),
            $"Scale: {Context.FormatWeight(player.WeightPounds)}   Walk-around: {Context.FormatWeight(player.WalkAroundPounds)}",
            new Vector2(rect.Left + 36, wtY + 36), Theme.White);
        batch.Text(Context.Assets.Font("small"),
            $"{division.Name} limit: {division.LimitText(Context.Units)}",
            new Vector2(rect.Left + 36, wtY + 78), Theme.Muted);

        var over = division.Limit is null
            ? 0m
            : Math.Max(0m, player.WeightPounds - division.Limit.Value.Pounds);
        if (over > 0m)
        {
            batch.Text(Context.Assets.Font("small"),
                $"{Context.FormatWeight(over)} over - cut before the weigh-in or miss weight",
                new Vector2(rect.Left + 36, wtY + 114), Theme.RedLight);
        }
        else
        {
            batch.Text(Context.Assets.Font("small"), "Inside the limit.",
                new Vector2(rect.Left + 36, wtY + 114), Theme.Green);
        }

        // Attribute readout.
        batch.Text(Context.Assets.Font("small"), "ATTRIBUTES", new Vector2(rect.Left + 36, rect.Bottom - 210),
            Theme.Gold);

        var effective = player.EffectiveAttributes();
        var attributes = new[]
        {
            ("PWR", effective.Power), ("AIM", effective.Aim), ("CHIN", effective.Chin),
            ("END", effective.Endurance), ("RFX", effective.Reflex), ("MASS", effective.Mass)
        };

        for (var i = 0; i < attributes.Length; i++)
        {
            var x = rect.Left + 36 + (i % 3) * 270;
            var ay = rect.Bottom - 170 + (i / 3) * 74;
            StatBar.Draw(batch, Context.Assets, new Rectangle(x, ay, 240, 20), attributes[i].Item1,
                attributes[i].Item2, i == 0 ? Theme.Red : i == 1 ? Theme.Blue : Theme.Green);
        }
    }
}
