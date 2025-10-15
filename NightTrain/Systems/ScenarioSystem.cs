using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

public class ScenarioSystem : ModSubsystemBase
{
    private readonly TrainPath _trainPath;
    private readonly Random _random;

    // scheduled scenarios keyed by track index (not yet spawned)
    private readonly Dictionary<int, ScenarioType> _scenarios;
    // active spawned scenarios keyed by track index
    private readonly Dictionary<int, ActiveScenario> _active = new Dictionary<int, ActiveScenario>();

    // simple config knobs
    private const float PickupRadius = 3.0f;     // distance to collect TrackPickup
    private const float SpawnZOffsetOnTrack = 0.5f; // nudge prop slightly off ground to avoid clipping
    private const float TargetSideOffset = 10f;  // left of track
    private const float TargetUpOffset = 10f;    // above track

    private Vector3 PosAt(int mark)
    {
        var p = _trainPath.Positions[mark]; // Vector4: X,Y,Z,W(heading)
        return new Vector3(p.X, p.Y, p.Z);
    }

    private float HeadingAt(int mark)
    {
        // Already GTA degrees [0..360). Normalize just in case.
        var h = _trainPath.Positions[mark].W;
        h %= 360f; if (h < 0) h += 360f;
        return h;
    }

    public ScenarioSystem(TrainPath trainPath, Func<Random> getRandom)
    {
        _trainPath = trainPath;
        _random = getRandom();
        _scenarios = new Dictionary<int, ScenarioType>();
    }

    public override void Start()
    {
        GenerateScenarios();
        SpawnInitialWindow(); // pre-spawn anything within 0..PathWindowAhead
    }

    public override void Stop()
    {
        // delete anything still around
        foreach (var s in _active.Values) DespawnScenario(s);
        _active.Clear();
        _scenarios.Clear();
    }

    public override void Tick()
    {
        // face the “TargetPickup” target toward player, check pickups, and general cleanup
        UpdateActiveScenarios();
    }

    public void OnMarker(int mark)
    {
        var target = mark + TrainPath.PathWindowAhead;

        if (_scenarios.TryGetValue(target, out var type))
        {
            _scenarios.Remove(target);
            Spawn(type, target);
        }

        // Event-driven pickup collection: if a TrackPickup exists exactly on this mark, collect immediately
        if (_active.TryGetValue(mark, out var activeHere) && activeHere.Type == ScenarioType.TrackPickup)
        {
            // collect and cleanup
            var prop = activeHere.Entities.FirstOrDefault() as Prop;
            if (prop != null && prop.Exists())
            {
                Notification.PostTicker("~g~You got the thing!", true);
                try { prop.Delete(); } catch { }
            }
            activeHere.Entities.Clear();
            _active.Remove(mark);
        }

        CleanScenarios(mark);
    }


    private void GenerateScenarios()
    {
        // --- ORIGINAL (kept for later) -----------------------------
        /*
        // naive: scatter scenarios of random types beyond the lookahead window
        const int scenarioCount = 50;
        var typeValues = Enum.GetValues(typeof(ScenarioType)).Cast<ScenarioType>().ToArray();

        for (var i = 0; i < scenarioCount; i++)
        {
            var index = NewScenarioIndex();
            var type = typeValues[_random.Next(typeValues.Length)];
            _scenarios[index] = type;
        }
        */
        // -----------------------------------------------------------

        // DEBUG: deterministically place scenarios every 10 marks:
        // 10 = TrackPickup, 20 = TrackSinglePed, 30 = TargetPickup, 40 = TrackPickup, ...
        _scenarios.Clear();

        // cap at both 580 and the last valid index in the path
        var maxIndex = Math.Min(580, _trainPath.Positions.Count - 1);

        var cycle = new[]
        {
            ScenarioType.TrackPickup,
            ScenarioType.TrackSinglePed,
            ScenarioType.TargetPickup
        };

        var cycleIdx = 0;
        for (int mark = 10; mark <= maxIndex; mark += 10)
        {
            _scenarios[mark] = cycle[cycleIdx % cycle.Length];
            cycleIdx++;
        }
    }


    private int NewScenarioIndex()
    {
        // keep scenarios off the immediate nose of the train
        var min = TrainPath.PathWindowAhead + 1;
        var maxExclusive = Math.Max(min + 1, _trainPath.Positions.Count); // guard

        int r()
        {
            // pick any mark from [min, count-1]
            return _random.Next(min, maxExclusive);
        }

        var index = r();
        while (_scenarios.ContainsKey(index) || _active.ContainsKey(index)) index = r();
        return index;
    }

    // -------------------
    // Spawning + Cleanup
    // -------------------

    private void SpawnInitialWindow()
    {
        if (_trainPath?.Positions == null || _trainPath.Positions.Count == 0) return;

        var max = Math.Min(TrainPath.PathWindowAhead, _trainPath.Positions.Count - 1);

        // Gather first to avoid mutating the dictionary while iterating
        var toSpawn = _scenarios.Keys
            .Where(k => k >= 0 && k <= max)
            .OrderBy(k => k)
            .ToArray();

        foreach (var k in toSpawn)
        {
            var type = _scenarios[k];
            _scenarios.Remove(k);
            Spawn(type, k);
        }
    }

    private static bool EnsureModelLoaded(Model model, int timeoutMs = 1500)
    {
        if (!model.IsInCdImage || !model.IsValid) return false;
        model.Request(timeoutMs);
        return model.IsLoaded;
    }

    private void Spawn(ScenarioType type, int mark)
    {
        if (mark < 0 || mark >= _trainPath.Positions.Count) return;

        var basePos = PosAt(mark);
        var scenario = new ActiveScenario { Index = mark, Type = type };
        var lodDistance = TrainPath.PathWindowAhead * TrainPath.MarkerSpacing;

        switch (type)
        {
            case ScenarioType.TrackPickup:
                {
                    var pickupNames = new[]
                    {
                        "prop_large_gold",
                        "prop_large_gold_alt_a",
                        "prop_large_gold_alt_b",
                        "prop_large_gold_alt_c"
                    };

                    var name = pickupNames[_random.Next(pickupNames.Length)];
                    var model = new Model(name);
                    if (!EnsureModelLoaded(model)) break;

                    var pos = basePos + new Vector3(0, 0, SpawnZOffsetOnTrack);

                    // IMPORTANT: placeOnGround = false
                    var prop = World.CreateProp(model, pos, true, false);
                    if (prop != null)
                    {
                        prop.IsPersistent = true;

                        // make sure it doesn’t tumble off the rail while physics resolves
                        prop.IsPositionFrozen = true;

                        // keep it from being culled too aggressively (belt + braces)
                        Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, prop.Handle, true);

                        prop.LodDistance = lodDistance;
                        Function.Call(Hash.SET_ENTITY_LOD_DIST, prop.Handle, lodDistance);

                        // culling natives are deprecated due to known issues :(
                        //Function.Call(Hash.SET_ENTITY_DISTANCE_CULLING_RADIUS, prop.Handle, 600.0f);

                        scenario.Entities.Add(prop);
                    }

                    model.MarkAsNoLongerNeeded();
                    Notification.PostTicker($"Spawned pickup {name} at {mark}", true);
                    break;
                }


            case ScenarioType.TrackSinglePed:
                {
                    var model = new Model(PedHash.Michael);
                    if (!model.IsInCdImage || !model.IsValid) break;
                    model.Request(500);

                    var ped = World.CreatePed(model, basePos);
                    if (ped != null)
                    {
                        ped.IsPersistent = true;
                        ped.BlockPermanentEvents = true; // less likely to flee/panic
                        //ped.CanRagdoll = false;
                        ped.Health = Math.Max(100, ped.Health);
                        ped.Task.StandStill(-1);

                        scenario.Entities.Add(ped);
                    }

                    model.MarkAsNoLongerNeeded();

                    Notification.PostTicker($"Spawned Michael on track at ${ mark }", true);

                    break;
                }

            case ScenarioType.TargetPickup:
                {
                    var model = new Model("prop_billboard_01");
                    if (!EnsureModelLoaded(model)) break;

                    var (left, forward) = TrackBasis(mark);
                    var pos = basePos + (left * TargetSideOffset) + (Vector3.WorldUp * TargetUpOffset);

                    // IMPORTANT: placeOnGround = false
                    var prop = World.CreateProp(model, pos, true, false);
                    if (prop != null)
                    {
                        prop.IsPersistent = true;
                        prop.IsPositionFrozen = true;

                        Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, prop.Handle, true);
                        // see above
                        // Function.Call(Hash.SET_ENTITY_DISTANCE_CULLING_RADIUS, prop.Handle, 600.0f);

                        prop.LodDistance = lodDistance;
                        Function.Call(Hash.SET_ENTITY_LOD_DIST, prop.Handle, lodDistance);


                        FaceEntityTowards(prop, Game.Player.Character.Position);
                        scenario.Entities.Add(prop);
                    }

                    model.MarkAsNoLongerNeeded();
                    Notification.PostTicker($"Spawned target prop_target_backboard at {mark}", true);
                    break;
                }

        }

        if (scenario.Entities.Count > 0)
        {
            _active[mark] = scenario;
        }
        else
        {
            // if spawn failed (bad model etc), re-schedule a different mark later
            // (optional) _scenarios[NewScenarioIndex()] = type;
        }
    }

    private void DespawnScenario(ActiveScenario scenario)
    {
        foreach (var e in scenario.Entities)
        {
            try
            {
                if (e.Exists()) e.Delete();
            }
            catch { /* ignore */ }
        }
        scenario.Entities.Clear();
    }

    private void CleanScenarios(int mark)
    {
        var minKeep = Math.Max(0, mark - TrainPath.PathWindowBehind);
        var toRemove = _active.Keys.Where(k => k < minKeep).ToArray();

        foreach (var k in toRemove)
        {
            DespawnScenario(_active[k]);
            _active.Remove(k);
        }
    }

    // -------------------
    // Per-tick updates
    // -------------------

    private void UpdateActiveScenarios()
    {
        var player = Game.Player.Character;

        foreach (var kv in _active.ToArray())
        {
            var scenario = kv.Value;

            switch (scenario.Type)
            {
                case ScenarioType.TrackPickup:
                    {
                        // No per-tick collision checks needed; handled in OnMarker when we pass the pickup mark
                        break;
                    }

                case ScenarioType.TargetPickup:
                    {
                        var prop = scenario.Entities.FirstOrDefault() as Prop;
                        if (prop != null && prop.Exists())
                        {
                            FaceEntityTowards(prop, player.Position);
                        }
                        break;
                    }

                case ScenarioType.TrackSinglePed:
                    {
                        // keep them standing their ground; you can add combat or other behavior here later
                        // e.g., if close enough, make them fight the player:
                        var ped = scenario.Entities.FirstOrDefault() as Ped;
                        if (ped != null && ped.Exists())
                        {
                            if (!ped.IsInCombat && player.Position.DistanceToSquared(ped.Position) < 25f)
                            {
                                ped.Task.Combat(player);
                            }
                        }
                        break;
                    }
            }
        }
    }

    // -------------------
    // Helpers
    // -------------------

    private (Vector3 left, Vector3 forward) TrackBasis(int mark)
    {
        var h = HeadingAt(mark);
        var hr = h * ((float)Math.PI / 180f);

        // Horizontal forward from GTA heading
        var forward = new Vector3((float)Math.Sin(hr), (float)Math.Cos(hr), 0f);
        forward.Normalize();

        // Left vector relative to world up
        var left = Vector3.Cross(Vector3.WorldUp, forward);
        if (left.Length() < 0.001f) left = Vector3.RelativeLeft;
        else left.Normalize();

        return (left, forward);
    }

    private void FaceEntityTowards(Entity entity, Vector3 targetWorldPos)
    {
        var to = (targetWorldPos - entity.Position);
        if (to.Length() < 0.001f) return;

        // yaw from world X/Y
        var heading = MathUtil.ToDegrees((float)Math.Atan2(to.Y, to.X));
        // In GTA heading is 0=north, increases clockwise; quick convert:
        var gtaHeading = (90f - heading) % 360f;
        if (gtaHeading < 0) gtaHeading += 360f;

        entity.Heading = gtaHeading;
    }

    private static class MathUtil
    {
        public const float PI = (float)Math.PI;
        public static float ToDegrees(float radians) => radians * (180f / PI);
    }

    private class ActiveScenario
    {
        public int Index;
        public ScenarioType Type;
        public List<Entity> Entities = new List<Entity>();
    }
}

public enum ScenarioType
{
    // a prop that we check for collision with, on collide destroy and for now just Notification.PostTicker "you got the thing"
    TrackPickup,
    // spawn a ped on the track - can we set them not to panic, so that they stand their ground and fight?
    TrackSinglePed,
    // a target prop from the shooting range - it always faces the player and is 10m to the left of the current mark and 10m above
    TargetPickup
}
