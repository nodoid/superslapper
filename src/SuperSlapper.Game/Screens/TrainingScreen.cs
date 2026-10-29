using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;
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
        ClearControls();

        // Two columns of sessions, skipping rest (which gets its own big button).
        var activities = TrainingCatalog.All.Where(a => a.Id != "rest").ToList();
        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            var col = i % 2;
            var row = i / 2;
            var rect = new Rectangle(80 + col * 420, 210 + row * 118, 400, 104);

            Add(new Button(rect, activity.DisplayName, () => Train(activity), ButtonStyle.Normal)
            {
                SubText = Loc.T("camp.energyLine", activity.EnergyCost, FocusName(activity.Focus)),
                IconKey = activity.IconKey
            });
        }

        Add(new Button(new Rectangle(80, 890, 400, 120), Loc.T("camp.restButton"),
            () => Train(TrainingCatalog.Rest), ButtonStyle.Ghost)
        {
            IconKey = "icon_medkit",
            SubText = Loc.T("camp.restNote")
        });

        // On the morning of the bout there is nothing left to train: the way out of
        // the camp is the scales, so the way out of the screen is too.
        var fightDay = Context.State is { Offer: { } offer } state && state.Day >= offer.FightDay;

        Add(new Button(new Rectangle(500, 890, 320, 120),
            fightDay ? Loc.T("camp.goWeighIn") : Loc.T("common.backToHub"),
            () =>
            {
                Context.Persist();

                if (fightDay)
                {
                    // Replaced rather than stacked on top of the camp, so BACK on
                    // the scales still lands on the hub.
                    Context.Screens.Pop();
                    Context.Screens.Push(new WeighInScreen(Context));
                    return;
                }

                Context.Screens.Pop();
            }, fightDay ? ButtonStyle.Danger : ButtonStyle.Ghost));
    }

    private static string FocusName(TrainingFocus focus) => Loc.T(focus switch
    {
        TrainingFocus.Strength => "camp.focus.power",
        TrainingFocus.Endurance => "camp.focus.endurance",
        TrainingFocus.Aim => "camp.focus.aim",
        TrainingFocus.SlapPower => "camp.focus.slapPower",
        TrainingFocus.Reflex => "camp.focus.reflex",
        TrainingFocus.Recovery => "camp.focus.recovery",
        TrainingFocus.WeightCut => "camp.focus.weightCut",
        _ => "camp.focus.study"
    });

    private void Train(TrainingActivity activity)
    {
        var state = State;
        if (state.Offer is not null && state.Day >= state.Offer.FightDay)
        {
            ShowToast(Loc.T("camp.fightDay"), Theme.RedLight);
            return;
        }

        var before = state.Player.Energy;
        var result = Context.Career.TrainDay(state, activity);
        Context.Persist();

        if (!result.MadeProgress && result.Injury is null && result.EnergyAfter == before)
        {
            ShowToast(Loc.T("camp.tooTired"), Theme.RedLight);
            return;
        }

        Context.Assets.Play("ui_confirm", 0.4f);

        if (result.Injury is not null)
        {
            ShowToast(Loc.T("camp.injuryToast", result.Injury.DisplayName, result.Injury.DaysRemaining),
                Theme.RedLight);
        }
        else if (result.MadeProgress)
        {
            ShowToast(result.GainSummary, Theme.Green);
        }
        else
        {
            ShowToast(Loc.T("camp.noGainAgain"), Theme.Muted);
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

        foreach (var button in Buttons.ToArray())
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

        batch.Text(Context.Assets.Font("large"), Loc.T("camp.title"), new Vector2(80, 40), Theme.Gold);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("camp.wardLine", state.Day, Context.UnitLabel,
                state.Offer is null
                    ? Loc.T("camp.noBout")
                    : Loc.T("camp.fightIn", Math.Max(0, state.Offer.DaysUntilFight(state.Day)))),
            new Vector2(80, 130), Theme.Muted);

        // Condition strip.
        StatBar.Draw(batch, Context.Assets, new Rectangle(900, 60, 300, 22),
            Loc.T("hub.energy"), player.Energy, Theme.Green);
        StatBar.Draw(batch, Context.Assets, new Rectangle(1240, 60, 300, 22),
            Loc.T("hub.headDamage"), player.Damage, Theme.Amber);
        StatBar.Draw(batch, Context.Assets, new Rectangle(1580, 60, 260, 22),
            Loc.T("hub.popularity"), player.Popularity, Theme.Gold);

        foreach (var button in Buttons.ToArray())
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
        // Tall enough for the four blocks below to sit one under the other: the camp
        // log, the injury report, the weight watch and the attributes. Each block is
        // anchored from the top of the panel, except the attributes, which are pinned
        // to the bottom so a long camp log can never push them out of the frame.
        var rect = new Rectangle(980, 210, 860, 790);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        batch.Text(Context.Assets.Font("small"), Loc.T("camp.log"),
            new Vector2(rect.Left + 36, rect.Top + 24), Theme.Gold);

        var log = state.TrainingLog.TakeLast(6).Reverse().ToList();
        var y = rect.Top + 64;
        foreach (var entry in log)
        {
            batch.Text(Context.Assets.Font("small"), Loc.T("camp.day", entry.Day),
                new Vector2(rect.Left + 36, y), Theme.Muted);
            batch.Text(Context.Assets.Font("small"),
                string.IsNullOrWhiteSpace(entry.ActivityId)
                    ? entry.ActivityName
                    : Loc.T($"camp.{entry.ActivityId}.name"),
                new Vector2(rect.Left + 140, y), Theme.White);
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
            batch.Text(Context.Assets.Font("body"), Loc.T("camp.noSessions"),
                new Vector2(rect.Left + 36, y + 10), Theme.Muted);
        }

        // Injuries.
        var injY = rect.Top + 272;
        batch.Text(Context.Assets.Font("small"), Loc.T("camp.injuryReport"),
            new Vector2(rect.Left + 36, injY), Theme.Gold);
        var injuries = player.ActiveInjuries.ToList();
        if (injuries.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), Loc.T("camp.healthy"),
                new Vector2(rect.Left + 36, injY + 40), Theme.Green);
        }
        else
        {
            var row = 0;
            foreach (var injury in injuries)
            {
                var iy = injY + 40 + row * 34;
                batch.Text(Context.Assets.Font("small"),
                    Loc.T("camp.injuryRow", injury.DisplayName, injury.SeverityText,
                        injury.DaysRemaining),
                    new Vector2(rect.Left + 36, iy), Theme.RedLight);
                batch.Text(Context.Assets.Font("tiny"),
                    Loc.T("camp.affects", string.Join(", ",
                        injury.AffectedAttributes.Select(a => Loc.T($"attribute.{a}")))),
                    new Vector2(rect.Right - 40, iy + 4), Theme.Muted, 1f, TextAlign.Right);
                row++;
            }
        }

        // Weight watch - the training/weigh-in link.
        var wtY = rect.Top + 418;
        batch.Text(Context.Assets.Font("small"), Loc.T("camp.weightWatch"),
            new Vector2(rect.Left + 36, wtY), Theme.Gold);
        var division = WeightClasses.Get(state.PlayerDivision);
        batch.Text(Context.Assets.Font("body"),
            Loc.T("camp.scale", Context.FormatWeight(player.WeightPounds),
                Context.FormatWeight(player.WalkAroundPounds)),
            new Vector2(rect.Left + 36, wtY + 36), Theme.White);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("camp.limit", division.Name, division.LimitText(Context.Units)),
            new Vector2(rect.Left + 36, wtY + 78), Theme.Muted);
        var over = division.Limit is null
            ? 0m
            : Math.Max(0m, player.WeightPounds - division.Limit.Value.Pounds);
        if (over > 0m)
        {
            batch.Text(Context.Assets.Font("small"),
                Loc.T("camp.over", Context.FormatWeight(over)),
                new Vector2(rect.Left + 36, wtY + 114), Theme.RedLight);
        }
        else
        {
            batch.Text(Context.Assets.Font("small"), Loc.T("camp.insideLimit"),
                new Vector2(rect.Left + 36, wtY + 114), Theme.Green);
        }

        // Attribute readout. Two columns leave the right of the panel to the fighter.
        batch.Text(Context.Assets.Font("small"), Loc.T("camp.attributes"),
            new Vector2(rect.Left + 36, rect.Bottom - 196), Theme.Gold);

        var effective = player.EffectiveAttributes();
        var attributes = new[]
        {
            (Loc.T("attribute.Power.short"), effective.Power),
            (Loc.T("attribute.Aim.short"), effective.Aim),
            (Loc.T("attribute.Chin.short"), effective.Chin),
            (Loc.T("attribute.Endurance.short"), effective.Endurance),
            (Loc.T("attribute.Reflex.short"), effective.Reflex),
            (Loc.T("attribute.Mass.short"), effective.Mass)
        };

        for (var i = 0; i < attributes.Length; i++)
        {
            var x = rect.Left + 36 + (i % 2) * 260;
            var ay = rect.Bottom - 134 + (i / 2) * 54;
            StatBar.Draw(batch, Context.Assets, new Rectangle(x, ay, 220, 20), attributes[i].Item1,
                attributes[i].Item2, i == 0 ? Theme.Red : i == 1 ? Theme.Blue : Theme.Green);
        }
    }
}
