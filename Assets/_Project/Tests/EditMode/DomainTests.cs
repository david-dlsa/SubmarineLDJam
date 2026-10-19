using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SubmarineLDJam.Domain.Caves;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Lights;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Domain.Progress;
using SubmarineLDJam.Domain.Oxygen;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.Domain.Sonar;
using SubmarineLDJam.UseCases;

namespace SubmarineLDJam.Tests
{
    public class MovementModuleTests
    {
        [Test]
        public void Compass_spins_clockwise()
        {
            var m = new MovementModule(90f, 1f, 5);
            m.Tick(1f);
            Assert.AreEqual(90f, m.CurrentHeading.Degrees, 0.001f);
        }

        [Test]
        public void First_confirm_locks_heading_second_fires_impulse()
        {
            var m = new MovementModule(90f, 1f, 5);
            m.Tick(1f);

            Assert.IsNull(m.Confirm());
            Assert.AreEqual(MovementPhase.SelectingForce, m.Phase);

            m.Tick(0.5f); // compass is frozen now, bar moves to 0.5
            var impulse = m.Confirm();

            Assert.IsTrue(impulse.HasValue);
            Assert.AreEqual(90f, impulse.Value.Heading.Degrees, 0.001f);
            Assert.AreEqual(3, impulse.Value.Force.Level);
            Assert.AreEqual(MovementPhase.Executing, m.Phase);
        }

        [Test]
        public void Movement_is_locked_until_it_finishes()
        {
            var m = new MovementModule(90f, 1f, 5);
            m.Confirm();
            m.Tick(0.5f);
            m.Confirm();
            float compass = m.CurrentHeading.Degrees;
            float bar = m.ForcePosition;

            m.Tick(2f);
            Assert.IsNull(m.Confirm());
            Assert.AreEqual(MovementPhase.Executing, m.Phase);
            Assert.AreEqual(compass, m.CurrentHeading.Degrees, 0.0001f);
            Assert.AreEqual(bar, m.ForcePosition, 0.0001f);

            Assert.IsTrue(m.FinishMovement());
            Assert.AreEqual(MovementPhase.SelectingHeading, m.Phase);
            Assert.AreEqual(0f, m.ForcePosition, 0.0001f);
            Assert.IsFalse(m.FinishMovement()); // nothing left to finish
        }

        [Test]
        public void Compass_snaps_to_step()
        {
            var m = new MovementModule(10f, 1f, 5, 15f);
            m.Tick(2f); // 20 degrees -> nearest step is 15
            Assert.AreEqual(15f, m.CurrentHeading.Degrees, 0.001f);
            m.Confirm();
            Assert.AreEqual(15f, m.LockedHeading.Degrees, 0.001f);
        }

        [Test]
        public void Force_bar_bounces_back()
        {
            var m = new MovementModule(90f, 1f, 5);
            m.Confirm();
            m.Tick(1.5f);
            Assert.AreEqual(0.5f, m.ForcePosition, 0.001f);
        }

        [Test]
        public void Max_force_bar_gives_max_level()
        {
            var m = new MovementModule(90f, 1f, 5);
            m.Confirm();
            m.Tick(1f);
            Assert.AreEqual(5, m.Confirm().Value.Force.Level);
        }
    }

    public class OxygenSupplyTests
    {
        [Test]
        public void Drains_over_time()
        {
            var o = new OxygenSupply(100f, 2f);
            o.Tick(10f);
            Assert.AreEqual(80f, o.Current, 0.001f);
        }

        [Test]
        public void Boost_ends_in_the_middle_of_a_tick()
        {
            var o = new OxygenSupply(100f, 1f);
            o.AddDrainBoost(2f, 3f);
            o.Tick(5f); // 3s * 2 + 2s * 1 = 8
            Assert.AreEqual(92f, o.Current, 0.001f);
            Assert.IsFalse(o.IsBoosted);
        }

        [Test]
        public void Restore_does_not_pass_max()
        {
            var o = new OxygenSupply(100f, 1f);
            o.Drain(5f);
            o.Restore(50f);
            Assert.IsTrue(o.IsFull);
        }
    }

    public class StrictRefillTests
    {
        private static Arrow Other(Arrow a) => a == Arrow.Up ? Arrow.Down : Arrow.Up;

        [Test]
        public void Mistake_restarts_the_same_sequence_and_restores_nothing()
        {
            var o = new OxygenSupply(100f, 0f);
            var p = new OxygenRefillPuzzle(3, 10f, 0.75f, 0.25f, new Random(1), restartOnMistake: true);
            o.Drain(30f);
            p.Refresh(o);
            var seq = new System.Collections.Generic.List<Arrow>(p.Sequence);

            p.Input(seq[0], o);
            var bad = p.Input(Other(seq[1]), o);

            Assert.IsTrue(bad.SequenceFailed);
            Assert.AreEqual(0, p.Progress);
            Assert.AreEqual(70f, o.Current, 0.001f);
            CollectionAssert.AreEqual(seq, new System.Collections.Generic.List<Arrow>(p.Sequence));
            foreach (var slot in p.Slots) Assert.AreEqual(RefillSlotState.Pending, slot);
        }

        [Test]
        public void Full_right_sequence_restores_everything()
        {
            var o = new OxygenSupply(100f, 0f);
            var p = new OxygenRefillPuzzle(3, 10f, 0.75f, 0.25f, new Random(1), restartOnMistake: true);
            o.Drain(30f);
            p.Refresh(o);
            var seq = new System.Collections.Generic.List<Arrow>(p.Sequence);

            p.Input(seq[0], o);
            p.Input(Other(seq[1]), o); // mistake, start over
            p.Input(seq[0], o);
            p.Input(seq[1], o);
            var last = p.Input(seq[2], o);

            Assert.IsTrue(last.SequenceCompleted);
            Assert.AreEqual(10f, last.Restored, 0.001f);
            Assert.AreEqual(80f, o.Current, 0.001f);
        }
    }

    public class OxygenRefillPuzzleTests
    {
        private static (OxygenSupply, OxygenRefillPuzzle) Make()
            => (new OxygenSupply(100f, 0f), new OxygenRefillPuzzle(3, 10f, 0.75f, 0.25f, new Random(1)));

        [Test]
        public void Turns_on_at_trigger_percent()
        {
            var (o, p) = Make();
            Assert.IsFalse(p.Refresh(o));
            o.Drain(25f);
            Assert.IsTrue(p.Refresh(o));
            Assert.IsTrue(p.IsActive);
            Assert.AreEqual(3, p.Sequence.Count);
        }

        [Test]
        public void Finished_sequence_restores_and_gives_new_one()
        {
            var (o, p) = Make();
            o.Drain(40f);
            p.Refresh(o);

            var seq = new List<Arrow>(p.Sequence);
            RefillInputOutcome last = default;
            foreach (var a in seq) last = p.Input(a, o);

            Assert.IsTrue(last.SequenceCompleted);
            Assert.AreEqual(0, last.Mistakes);
            Assert.AreEqual(70f, o.Current, 0.001f);
            Assert.IsTrue(p.IsActive);
            Assert.AreEqual(0, p.Progress);
        }

        [Test]
        public void Wrong_arrow_uses_slot_but_does_not_block_sequence()
        {
            var (o, p) = Make();
            o.Drain(40f);
            p.Refresh(o);

            var seq = new List<Arrow>(p.Sequence);
            var wrong = Enum.GetValues(typeof(Arrow)).Cast<Arrow>().First(a => a != seq[0]);

            var first = p.Input(wrong, o);
            Assert.AreEqual(RefillInputStatus.Wrong, first.Status);
            Assert.AreEqual(1, p.Progress);
            Assert.AreEqual(RefillSlotState.Wrong, p.Slots[0]);

            p.Input(seq[1], o);
            var last = p.Input(seq[2], o);

            Assert.IsTrue(last.SequenceCompleted);
            Assert.AreEqual(1, last.Mistakes);
            Assert.AreEqual(7.5f, last.Restored, 0.001f); // 10 * (1 - 0.25)
            Assert.AreEqual(67.5f, o.Current, 0.001f);
        }

        [Test]
        public void Many_mistakes_never_make_restore_negative()
        {
            var (o, p) = Make();
            o.Drain(40f);
            p.Refresh(o);

            var seq = new List<Arrow>(p.Sequence);
            RefillInputOutcome last = default;
            foreach (var a in seq)
            {
                var wrong = Enum.GetValues(typeof(Arrow)).Cast<Arrow>().First(x => x != a);
                last = p.Input(wrong, o);
            }

            Assert.AreEqual(3, last.Mistakes);
            Assert.AreEqual(2.5f, last.Restored, 0.001f); // 10 * (1 - 0.75)
        }

        [Test]
        public void Turns_off_when_full()
        {
            var (o, p) = Make();
            o.Drain(40f);
            p.Refresh(o);
            o.Restore(100f);
            Assert.IsTrue(p.Refresh(o));
            Assert.IsFalse(p.IsActive);
        }

        [Test]
        public void Input_is_ignored_when_off()
        {
            var (o, p) = Make();
            Assert.AreEqual(RefillInputStatus.Ignored, p.Input(Arrow.Up, o).Status);
        }
    }

    public class SubmarineRunTests
    {
        private static SubmarineRun Make(float drain = 1f)
            => new SubmarineRun(
                new LevelRules("t", new StarThresholds(30f, 60f), maxOxygen: 100f, oxygenDrainPerSecond: drain),
                new Random(1));

        [Test]
        public void Run_fails_when_oxygen_hits_zero()
        {
            var run = Make(10f);
            run.Tick(10f);

            Assert.AreEqual(RunStatus.Failed, run.Status);
            Assert.IsTrue(run.PullEvents().Any(e => e is RunFailed));
        }

        [Test]
        public void Reaching_destination_gives_stars_by_time()
        {
            var run = Make();
            run.Tick(45f);
            run.ReachDestination();

            Assert.AreEqual(RunStatus.Completed, run.Status);
            Assert.AreEqual(2, run.Stars);
        }

        [Test]
        public void Fast_run_gets_three_stars()
        {
            var run = Make();
            run.Tick(20f);
            run.ReachDestination();
            Assert.AreEqual(3, run.Stars);
        }

        [Test]
        public void Wall_hit_costs_oxygen_once_during_cooldown()
        {
            var run = Make(0f);
            run.RegisterCollision(CollisionKind.Wall);
            run.RegisterCollision(CollisionKind.Wall);
            Assert.AreEqual(90f, run.Oxygen.Current, 0.001f);

            run.Tick(0.6f);
            run.RegisterCollision(CollisionKind.Wall);
            Assert.AreEqual(80f, run.Oxygen.Current, 0.001f);
        }

        [Test]
        public void Custom_penalty_can_ignore_the_cooldown()
        {
            var run = Make(0f);
            run.RegisterCollision(CollisionKind.Wall); // -10, starts the cooldown
            run.RegisterCollision(CollisionKind.Bomb, new CollisionPenalty(20f), true);
            Assert.AreEqual(70f, run.Oxygen.Current, 0.001f);

            run.RegisterCollision(CollisionKind.Bomb, new CollisionPenalty(20f)); // cooldown still on
            Assert.AreEqual(70f, run.Oxygen.Current, 0.001f);
        }

        [Test]
        public void Enemy_hit_makes_oxygen_fall_faster()
        {
            var run = Make(1f);
            run.RegisterCollision(CollisionKind.Enemy);
            Assert.IsTrue(run.Oxygen.IsBoosted);
            Assert.AreEqual(2f, run.Oxygen.CurrentDrainPerSecond, 0.001f);
        }

        [Test]
        public void Refill_puzzle_shows_after_hit_drops_oxygen_under_trigger()
        {
            var run = Make(0f);
            run.RegisterCollision(CollisionKind.Enemy); // -15
            run.Tick(1f);
            run.RegisterCollision(CollisionKind.Enemy); // -15 -> 70%

            Assert.IsTrue(run.Refill.IsActive);
            Assert.IsTrue(run.PullEvents().Any(e => e is RefillPuzzleActivated));
        }

        [Test]
        public void Ended_run_ignores_commands()
        {
            var run = Make();
            run.ReachDestination();
            run.ConfirmMovement();
            run.Tick(5f);

            Assert.AreEqual(0f, run.ElapsedSeconds, 0.001f);
        }
    }

    public class SonarTests
    {
        [Test]
        public void Ping_works_then_needs_cooldown()
        {
            var sonar = new SonarModule(8f);
            Assert.IsTrue(sonar.IsReady);
            Assert.IsTrue(sonar.TryPing());
            Assert.IsFalse(sonar.TryPing());
            Assert.AreEqual(1f, sonar.CooldownPercent, 0.001f);

            sonar.Tick(4f);
            Assert.AreEqual(0.5f, sonar.CooldownPercent, 0.001f);
            Assert.IsFalse(sonar.TryPing());

            sonar.Tick(4f);
            Assert.IsTrue(sonar.IsReady);
            Assert.IsTrue(sonar.TryPing());
        }

        [Test]
        public void Run_raises_ping_then_rejects_spam()
        {
            var run = new SubmarineRun(
                new LevelRules("t", new StarThresholds(30f, 60f), oxygenDrainPerSecond: 0f, sonarCooldownSeconds: 5f),
                new Random(1));

            run.PingSonar();
            run.PingSonar();
            var events = run.PullEvents();
            Assert.AreEqual(1, events.Count(e => e is SonarPinged));
            Assert.AreEqual(1, events.Count(e => e is SonarPingRejected));

            run.Tick(5f);
            run.PingSonar();
            Assert.IsTrue(run.PullEvents().Any(e => e is SonarPinged));
        }
    }

    public class SonarModeTests
    {
        [Test]
        public void Auto_pings_at_start_then_every_interval()
        {
            var sonar = new SonarModule(6f, SonarMode.Auto);

            Assert.IsTrue(sonar.Tick(0.1f));   // first ping comes at once
            Assert.IsFalse(sonar.Tick(3f));
            Assert.IsFalse(sonar.Tick(2.5f));
            Assert.IsTrue(sonar.Tick(0.5f));   // 6s after the first
        }

        [Test]
        public void Auto_ignores_manual_ping()
        {
            var sonar = new SonarModule(6f, SonarMode.Auto);
            Assert.IsFalse(sonar.TryPing());
        }

        [Test]
        public void Always_never_pings()
        {
            var sonar = new SonarModule(6f, SonarMode.Always);
            Assert.IsFalse(sonar.Tick(100f));
            Assert.IsFalse(sonar.TryPing());
        }

        [Test]
        public void Run_in_auto_mode_raises_pings_without_key()
        {
            var run = new SubmarineRun(
                new LevelRules("t", new StarThresholds(30f, 60f), oxygenDrainPerSecond: 0f,
                    sonarMode: SonarMode.Auto, sonarAutoIntervalSeconds: 5f),
                new Random(1));

            run.Tick(0.1f);
            run.PingSonar(); // M is ignored
            run.Tick(5f);

            var events = run.PullEvents();
            Assert.AreEqual(2, events.Count(e => e is SonarPinged));
            Assert.AreEqual(0, events.Count(e => e is SonarPingRejected));
        }
    }

    public class CampaignTests
    {
        [Test]
        public void Intro_is_marked_once_and_remembered()
        {
            var c = new Campaign(3);
            Assert.IsFalse(c.HasSeenIntro(1));
            Assert.IsTrue(c.MarkIntroSeen(1));
            Assert.IsFalse(c.MarkIntroSeen(1)); // already seen
            Assert.IsTrue(c.HasSeenIntro(1));
            Assert.IsFalse(c.HasSeenIntro(0));
            Assert.IsFalse(c.MarkIntroSeen(9)); // stage does not exist

            var loaded = new Campaign(3, null, c.IntroSeen);
            Assert.IsTrue(loaded.HasSeenIntro(1));
        }

        [Test]
        public void Only_first_stage_is_open_at_start()
        {
            var c = new Campaign(3);
            Assert.IsTrue(c.IsUnlocked(0));
            Assert.IsFalse(c.IsUnlocked(1));
            Assert.IsFalse(c.IsUnlocked(2));
            Assert.IsFalse(c.IsUnlocked(3)); // does not exist
            Assert.AreEqual(0, c.HighestUnlocked);
        }

        [Test]
        public void Finishing_a_stage_opens_the_next_one()
        {
            var c = new Campaign(3);
            Assert.IsTrue(c.RecordCompletion(0, 50f, 2));
            Assert.IsTrue(c.IsUnlocked(1));
            Assert.IsFalse(c.IsUnlocked(2));
            Assert.AreEqual(1, c.HighestUnlocked);
        }

        [Test]
        public void Locked_stage_cannot_be_recorded()
        {
            var c = new Campaign(3);
            Assert.Throws<InvalidOperationException>(() => c.RecordCompletion(1, 10f, 3));
        }

        [Test]
        public void Keeps_best_time_and_best_stars_apart()
        {
            var c = new Campaign(2);
            c.RecordCompletion(0, 60f, 3);

            Assert.IsFalse(c.RecordCompletion(0, 80f, 1));   // worse in both: nothing changes
            Assert.IsTrue(c.RecordCompletion(0, 40f, 2));    // faster, fewer stars: time improves, stars stay

            c.TryGetRecord(0, out var record);
            Assert.AreEqual(40f, record.BestSeconds, 0.001f);
            Assert.AreEqual(3, record.Stars);
        }

        [Test]
        public void Loads_saved_records_and_ignores_stages_that_no_longer_exist()
        {
            var c = new Campaign(2, new[] { new StageRecord(0, 30f, 3), new StageRecord(5, 10f, 1) });
            Assert.IsTrue(c.IsCompleted(0));
            Assert.IsFalse(c.IsCompleted(5));
            Assert.IsTrue(c.IsUnlocked(1));
        }
    }

    public class RunContextTests
    {
        [SetUp]
        public void Reset() => RunContext.ResetAll();

        [TearDown]
        public void Cleanup() => RunContext.ResetAll();

        [Test]
        public void Set_publishes_the_run_and_tells_listeners()
        {
            int changes = 0;
            RunContext.Changed += () => changes++;

            var service = new RunService();
            RunContext.Set(service);
            Assert.AreSame(service, RunContext.Current);

            RunContext.Clear();
            Assert.IsNull(RunContext.Current);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Exit_request_is_handled_only_when_someone_listens()
        {
            Assert.IsFalse(RunContext.RequestExit());

            bool heard = false;
            RunContext.ExitRequested += () => heard = true;
            Assert.IsTrue(RunContext.RequestExit());
            Assert.IsTrue(heard);
        }
    }

    public class CaveGeneratorTests
    {
        private static CaveParameters Make(int seed = 1)
            => new CaveParameters(width: 44, height: 30, seed: seed, minPathCells: 30, obstacles: 3, enemies: 2, pickups: 2);

        [Test]
        public void Same_seed_gives_same_cave()
        {
            var a = CaveGenerator.Generate(Make(5));
            var b = CaveGenerator.Generate(Make(5));

            Assert.AreEqual(a.Start, b.Start);
            Assert.AreEqual(a.Goal, b.Goal);
            for (int x = 0; x < a.Width; x++)
                for (int y = 0; y < a.Height; y++)
                    Assert.AreEqual(a.IsRock(x, y), b.IsRock(x, y));
        }

        [Test]
        public void Different_seed_gives_different_cave()
        {
            var a = CaveGenerator.Generate(Make(1));
            var b = CaveGenerator.Generate(Make(2));

            int differences = 0;
            for (int x = 0; x < a.Width; x++)
                for (int y = 0; y < a.Height; y++)
                    if (a.IsRock(x, y) != b.IsRock(x, y)) differences++;

            Assert.Greater(differences, 0);
        }

        [Test]
        public void Border_is_always_rock()
        {
            var map = CaveGenerator.Generate(Make());
            for (int x = 0; x < map.Width; x++)
            {
                Assert.IsTrue(map.IsRock(x, 0));
                Assert.IsTrue(map.IsRock(x, map.Height - 1));
            }
            for (int y = 0; y < map.Height; y++)
            {
                Assert.IsTrue(map.IsRock(0, y));
                Assert.IsTrue(map.IsRock(map.Width - 1, y));
            }
        }

        [Test]
        public void Start_and_goal_are_open_and_can_be_swum_between()
        {
            var map = CaveGenerator.Generate(Make());

            Assert.IsFalse(map.IsRock(map.Start.X, map.Start.Y));
            Assert.IsFalse(map.IsRock(map.Goal.X, map.Goal.Y));
            Assert.GreaterOrEqual(map.PathLength, 30);
            Assert.IsTrue(CanSwim(map, map.Start, map.Goal));
        }

        [Test]
        public void Things_sit_in_open_water_and_are_reachable()
        {
            var map = CaveGenerator.Generate(Make());

            Assert.LessOrEqual(map.Obstacles.Count, 3);
            Assert.LessOrEqual(map.Enemies.Count, 2);
            Assert.LessOrEqual(map.Pickups.Count, 2);

            foreach (var cell in map.Obstacles) AssertOpenAndReachable(map, cell);
            foreach (var cell in map.Enemies) AssertOpenAndReachable(map, cell);
            foreach (var cell in map.Pickups) AssertOpenAndReachable(map, cell);
        }

        [Test]
        public void Many_pickups_are_placed_and_spread_over_the_cave()
        {
            var p = new CaveParameters(width: 60, height: 40, seed: 3, minPathCells: 30, pickups: 30);
            var map = CaveGenerator.Generate(p);

            Assert.GreaterOrEqual(map.Pickups.Count, 20);
            foreach (var cell in map.Pickups) AssertOpenAndReachable(map, cell);

            for (int i = 0; i < map.Pickups.Count; i++)
                for (int j = i + 1; j < map.Pickups.Count; j++)
                    Assert.AreNotEqual(map.Pickups[i], map.Pickups[j]);
        }

        private static void AssertOpenAndReachable(CaveMap map, CaveCell cell)
        {
            Assert.IsFalse(map.IsRock(cell.X, cell.Y));
            Assert.IsTrue(CanSwim(map, map.Start, cell));
        }

        private static bool CanSwim(CaveMap map, CaveCell from, CaveCell to)
        {
            var seen = new HashSet<CaveCell> { from };
            var queue = new Queue<CaveCell>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c.Equals(to)) return true;

                foreach (var n in new[]
                {
                    new CaveCell(c.X + 1, c.Y), new CaveCell(c.X - 1, c.Y),
                    new CaveCell(c.X, c.Y + 1), new CaveCell(c.X, c.Y - 1)
                })
                {
                    if (map.IsRock(n.X, n.Y) || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return false;
        }
    }

    public class ExitHintTests
    {
        [Test]
        public void Always_mode_is_always_visible_and_has_nothing_to_collect()
        {
            var hint = new ExitHintModule(ExitHintMode.Always, 10f);
            Assert.IsTrue(hint.IsVisible);
            Assert.IsFalse(hint.Collect());
        }

        [Test]
        public void Pickup_mode_is_hidden_until_collected_then_runs_out()
        {
            var hint = new ExitHintModule(ExitHintMode.OnPickup, 10f);
            Assert.IsFalse(hint.IsVisible);

            Assert.IsTrue(hint.Collect());
            Assert.IsTrue(hint.IsVisible);

            hint.Tick(6f);
            Assert.IsTrue(hint.IsVisible);
            Assert.AreEqual(4f, hint.RemainingSeconds, 0.001f);

            hint.Tick(5f);
            Assert.IsFalse(hint.IsVisible);
        }

        [Test]
        public void Second_pickup_starts_the_time_again()
        {
            var hint = new ExitHintModule(ExitHintMode.OnPickup, 10f);
            hint.Collect();
            hint.Tick(8f);
            hint.Collect();
            Assert.AreEqual(10f, hint.RemainingSeconds, 0.001f);
        }

        [Test]
        public void Run_raises_event_only_in_pickup_mode()
        {
            var always = new SubmarineRun(new LevelRules("t", new StarThresholds(30f, 60f)), new Random(1));
            always.CollectExitHint();
            Assert.IsFalse(always.PullEvents().Any(e => e is ExitHintCollected));

            var pickup = new SubmarineRun(
                new LevelRules("t", new StarThresholds(30f, 60f), exitHintMode: ExitHintMode.OnPickup, exitHintSeconds: 12f),
                new Random(1));
            Assert.IsFalse(pickup.ExitHint.IsVisible);
            pickup.CollectExitHint();
            Assert.IsTrue(pickup.ExitHint.IsVisible);
            Assert.IsTrue(pickup.PullEvents().Any(e => e is ExitHintCollected));
        }
    }

    public class CompassModeTests
    {
        private static MovementModule Make(CompassMode mode, int seed = 1)
            => new MovementModule(90f, 1f, 5, 0f, mode, new Random(seed));

        private static void FinishMovement(MovementModule m)
        {
            m.Confirm();
            m.Tick(0.5f);
            m.Confirm();
            m.FinishMovement();
        }

        [Test]
        public void Needle_IsRelativeToSubmarineFront_AfterTurn()
        {
            var m = Make(CompassMode.Clockwise);
            m.Tick(0.5f);                       // needle 45
            m.Confirm();                        // front is now 45
            Assert.AreEqual(45f, m.Facing.Degrees, 0.001f);
            m.Confirm();                        // fire
            m.FinishMovement();                 // submarine stopped
            m.Tick(3f);                         // needle continues from 45: 45 + 270 = 315 (left of the new front)
            Assert.AreEqual(315f, m.CurrentHeading.Degrees, 0.001f);
            m.Confirm();
            Assert.AreEqual(0f, m.LockedHeading.Degrees, 0.001f); // 45 + 315 = 360 = north
        }

        [Test]
        public void Counter_clockwise_spins_the_other_way()
        {
            var m = Make(CompassMode.CounterClockwise);
            m.Tick(1f);
            Assert.AreEqual(270f, m.CurrentHeading.Degrees, 0.001f);
        }

        [Test]
        public void Alternating_starts_clockwise_and_flips_after_each_finished_movement()
        {
            var m = Make(CompassMode.Alternating);

            m.Tick(1f);
            Assert.AreEqual(90f, m.CurrentHeading.Degrees, 0.001f);

            FinishMovement(m);
            m.Tick(1f); // now counter-clockwise: 90 -> 0
            Assert.AreEqual(0f, m.CurrentHeading.Degrees, 0.001f);

            FinishMovement(m);
            m.Tick(1f); // clockwise again: 0 -> 90
            Assert.AreEqual(90f, m.CurrentHeading.Degrees, 0.001f);
        }

        [Test]
        public void Alternating_does_not_flip_when_only_the_heading_is_locked()
        {
            var m = Make(CompassMode.Alternating);
            m.Tick(1f);
            m.Confirm(); // heading locked, force not yet
            Assert.AreEqual(MovementPhase.SelectingForce, m.Phase);

            m.Tick(0.5f);
            m.Confirm();
            m.FinishMovement();
            m.Tick(1f);
            Assert.AreEqual(0f, m.CurrentHeading.Degrees, 0.001f); // flipped only once
        }

        [Test]
        public void Chaotic_reaches_every_direction()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var m = Make(CompassMode.Chaotic, seed);
                var seen = new HashSet<int>();

                for (int i = 0; i < 6000 && seen.Count < 12; i++) // up to 300 seconds
                {
                    m.Tick(0.05f);
                    seen.Add((int)(m.CurrentHeading.Degrees / 30f) % 12);
                }

                Assert.AreEqual(12, seen.Count, $"seed {seed} did not reach every direction");
            }
        }

        [Test]
        public void Chaotic_is_the_same_for_the_same_seed()
        {
            var a = Make(CompassMode.Chaotic, 7);
            var b = Make(CompassMode.Chaotic, 7);

            for (int i = 0; i < 400; i++)
            {
                a.Tick(0.05f);
                b.Tick(0.05f);
            }

            Assert.AreEqual(a.CurrentHeading.Degrees, b.CurrentHeading.Degrees, 0.0001f);
        }

        [Test]
        public void Chaotic_does_not_move_while_the_force_is_chosen()
        {
            var m = Make(CompassMode.Chaotic);
            m.Tick(1f);
            m.Confirm();
            float locked = m.CurrentHeading.Degrees;

            m.Tick(0.3f);
            Assert.AreEqual(locked, m.CurrentHeading.Degrees, 0.0001f);
        }
    }
    public class BlackoutTests
    {
        private static BlackoutModule Make(int seed = 1)
            => new BlackoutModule(new BlackoutSettings(10f, 20f, 3f, 6f), new Random(seed));

        [Test]
        public void Disabled_never_goes_dark()
        {
            var m = new BlackoutModule(BlackoutSettings.Disabled);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(BlackoutChange.None, m.Tick(1f));
            Assert.IsFalse(m.IsDark);
        }

        [Test]
        public void Goes_dark_after_the_interval_and_comes_back_after_the_duration()
        {
            var m = Make();
            float waited = 0f;
            while (m.Tick(0.1f) != BlackoutChange.Started) waited += 0.1f;

            Assert.IsTrue(m.IsDark);
            Assert.GreaterOrEqual(waited, 9.8f);
            Assert.LessOrEqual(waited, 20.2f);
            Assert.GreaterOrEqual(m.CurrentDurationSeconds, 3f);
            Assert.LessOrEqual(m.CurrentDurationSeconds, 6f);

            float dark = 0f;
            while (m.Tick(0.1f) != BlackoutChange.Ended) dark += 0.1f;

            Assert.IsFalse(m.IsDark);
            Assert.AreEqual(m.CurrentDurationSeconds, 0f, 0.0001f);
            Assert.GreaterOrEqual(dark, 2.8f);
            Assert.LessOrEqual(dark, 6.2f);
        }

        [Test]
        public void Same_seed_gives_the_same_blackouts()
        {
            var a = Make(5);
            var b = Make(5);
            for (int i = 0; i < 2000; i++) Assert.AreEqual(a.Tick(0.1f), b.Tick(0.1f));
        }

        [Test]
        public void Bad_settings_are_refused()
        {
            Assert.Throws<ArgumentException>(() => new BlackoutSettings(0f, 5f, 1f, 2f));
            Assert.Throws<ArgumentException>(() => new BlackoutSettings(10f, 5f, 1f, 2f));
            Assert.Throws<ArgumentException>(() => new BlackoutSettings(5f, 10f, 3f, 2f));
        }
    }
}
