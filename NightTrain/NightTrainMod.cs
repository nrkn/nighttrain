using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System.Media;

public class NightTrainMod : Script
{
    // later we will likely tidy up the state, but it's good to KISS when prototyping
    private readonly bool _recordPath = false;
    private readonly bool _showPath = true;
    private Vector3 _startPosition = new Vector3(926.74f, 6436.79f, 32.12f);
    private readonly float _startHeading = 265.6f;
    private readonly string _filename = "nighttrain_path.txt";

    private readonly List<IModSubsystem> _subsystems = new List<IModSubsystem>();
    private bool _isRunning;

    public NightTrainMod()
    {
        var startupSound = new StartupSoundSystem();
        var death = new DeathSystem(_startPosition, _startHeading, Restart);
        var train = new TrainSystem(_startPosition, _startHeading);

        _subsystems.Add(startupSound);
        _subsystems.Add(death);
        _subsystems.Add(train);

        if (_recordPath)
        {
            var pathRecorder = new RecordPathSystem(_startPosition, _startHeading, train.Engine, _filename);

            _subsystems.Add(pathRecorder);
        }

        if (_showPath)
        {
            var debugPathViewer = new DebugPathViewerSystem(train.Engine, _filename);

            _subsystems.Add(debugPathViewer);
        }

        KeyUp += OnKeyUp;
        Tick += OnTick;
        Interval = 0;

        Notification.PostTicker("~p~Night Train~s~ loaded (F5 to start, F6 to stop).", true);
    }

    void Restart()
    {
        Stop();
        Start();
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            if (!_isRunning)
            {
                Start();

                Notification.PostTicker("Night Train Started. Press F6 to stop.", true);
            }
            else Notification.PostTicker("Night Train already running! Press F6 to stop.", true);
        }
        else if (e.KeyCode == Keys.F6)
        {
            if (_isRunning) {
                Stop();
                Notification.PostTicker("Night Train Stopped. Press F5 to start.", true);
            }
            else Notification.PostTicker("Night Train already stopped! Press F5 to start.", true);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!_isRunning) return;

        foreach (var sys in _subsystems)
        {
            sys.Tick();
        }
    }

    private void Start()
    {
        _isRunning = true;

        foreach (var sys in _subsystems)
        {
            sys.Start();
        }
    }

    private void Stop()
    {
        _isRunning = false;

        foreach (var sys in _subsystems)
        {
            sys.Stop();
        }
    }
}

// so we don't have to implement empty methods on subclasses
public class ModSubsystemBase : IModSubsystem
{
    public virtual void Start() { }

    public virtual void Stop() { }

    public virtual void Tick() { }
}

public class DeathSystem : ModSubsystemBase
{
    private readonly Vector3 _startPosition;
    private readonly float _startHeading;
    private readonly Action _onRestart;

    public DeathSystem(Vector3 startPosition, float startHeading, Action onRestart)
    {
        _startPosition = startPosition;
        _startHeading = startHeading;
        _onRestart = onRestart;
    }

    public override void Start()
    {
        Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");
        Function.Call(Hash.IGNORE_NEXT_RESTART, true);
        Function.Call(Hash.PAUSE_DEATH_ARREST_RESTART, true);
    }

    public override void Stop()
    {
        Function.Call(Hash.PAUSE_DEATH_ARREST_RESTART, false);
    }

    public override void Tick()
    {
        var player = Game.Player.Character;

        if (player.IsDead)
        {
            while (!Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)) Script.Wait(0);

            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");
            Game.TimeScale = 1f;
            Function.Call(Hash.ANIMPOSTFX_STOP_ALL);
            Function.Call(Hash.NETWORK_REQUEST_CONTROL_OF_ENTITY, player);
            Function.Call(Hash.NETWORK_RESURRECT_LOCAL_PLAYER, _startPosition.X, _startPosition.Y, _startPosition.Z, _startHeading, false, false);

            Script.Wait(2000);

            Function.Call(Hash.DO_SCREEN_FADE_IN, 3500);
            Function.Call(Hash.FORCE_GAME_STATE_PLAYING);
            Function.Call(Hash.RESET_PLAYER_ARREST_STATE, player);
            Function.Call(Hash.DISPLAY_HUD, true);

            player.IsPositionFrozen = false;

            _onRestart();
        }
    }
}

public class StartupSoundSystem : ModSubsystemBase
{
    readonly SoundPlayer _soundPlayer;

    public StartupSoundSystem()
    {
        _soundPlayer = new SoundPlayer(@"scripts\nt.wav");
        _soundPlayer.LoadAsync();
    }

    public override void Start()
    {
        _soundPlayer?.Play();
    }

    public override void Stop()
    {
        _soundPlayer?.Stop();
    }
}

public class RecordPathSystem : ModSubsystemBase
{
    private const int SampleEveryMs = 500;
    private const float LoopDistanceThreshold = 3.0f;   // meters
    private const float LoopHeadingTolerance = 15.0f;   // degrees (0..180)
    private const int MinSamplesBeforeLoop = 50;      // avoid early accidental stop

    private Vector3 _startPosition;
    private readonly float _startHeading;
    private readonly Entity _target;
    private readonly List<Vector4> _path = new List<Vector4>();
    private readonly string _filename;

    private int _nextSampleAt = 0;
    private bool _recorderArmed = false;

    public RecordPathSystem(Vector3 startPosition, float startHeading, Entity target, string filename)
    {
        _startPosition = startPosition;
        _startHeading = startHeading;
        _target = target;
        _filename = filename;
    }

    public override void Start()
    {
        _path.Clear();
        _nextSampleAt = Game.GameTime; // sample immediately
        _recorderArmed = true;
    }

    public override void Stop()
    {
        _recorderArmed = false;
        _path.Clear();
    }

    public override void Tick()
    {
        if (_recorderArmed)
        {
            MaybeSamplePath();
            MaybeAutoStopOnLoop();
        }
    }

    private void MaybeSamplePath()
    {
        if (Game.GameTime < _nextSampleAt) return;
        _nextSampleAt = Game.GameTime + SampleEveryMs;

        Entity target = (_target != null && _target.Exists())
            ? _target
            : Game.Player.Character;

        var p = target.Position;
        float h = target.Heading; // 0..360

        _path.Add(new Vector4(p.X, p.Y, p.Z, h));
    }

    private void MaybeAutoStopOnLoop()
    {
        if (_path.Count < MinSamplesBeforeLoop) return;

        Entity target = (_target != null && _target.Exists())
            ? _target
            : Game.Player.Character;

        var now = target.Position;
        float hNow = target.Heading;

        float dist = now.DistanceTo(_startPosition);
        float dh = HeadingDeltaDegrees(hNow, _startHeading);

        if (dist <= LoopDistanceThreshold && dh <= LoopHeadingTolerance)
        {
            DumpPathToFile();
            _path.Clear();
            _recorderArmed = false;
            Notification.PostTicker("~p~Night Train~s~ path saved.", true);
        }
    }

    private static float HeadingDeltaDegrees(float a, float b)
    {
        float d = Math.Abs(a - b) % 360f;
        return d > 180f ? 360f - d : d;
    }

    private void DumpPathToFile()
    {
        try
        {
            string dataDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nighttrain");
            System.IO.Directory.CreateDirectory(dataDir);
            string fullPath = System.IO.Path.Combine(dataDir, _filename);

            using (var sw = new System.IO.StreamWriter(fullPath, false, System.Text.Encoding.UTF8))
            {
                // x y z h (space-separated), one sample per line
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var v in _path)
                    sw.WriteLine(string.Format(ci, "{0} {1} {2} {3}", v.X, v.Y, v.Z, v.W));
            }
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Path save failed:~s~ {ex.Message}", true);
        }
    }
}

/*
  this does crazy things when there are double tracks - for some reason, 
  the game mistakes the markers for train signal lights and they ping pong 
  between their original position and the mirror position on the second 
  track, the markers also flash on and off and get recolored to the train 
  signal light colors - quite funny but harmless as this is just a debug
  view - however keep in mind if spawning entities at these locations later,
  as we're not sure under what conditions the game will treat an entity as
  a signal light
*/
public class DebugPathViewerSystem : ModSubsystemBase
{
    private readonly List<Vector4> _loadedPath = new List<Vector4>();
    private bool _pathLoaded = false;
    private string _filename;
    private readonly Entity _target;

    private int _nearestPathIdx = -1;
    private const int PathWindowBehind = 50;  // how many points behind the nearest to draw
    private const int PathWindowAhead = 70;  // how many points ahead to draw

    public DebugPathViewerSystem(Entity target, string filename)
    {
        _target = target;
        _filename = filename;
    }

    public override void Start()
    {
        TryLoadPathFromFile();
    }

    public override void Stop()
    {
        _loadedPath.Clear();
        _pathLoaded = false;
    }

    public override void Tick()
    {
        if (_pathLoaded) DrawPathMarkersWindowed();
    }

    private void TryLoadPathFromFile()
    {
        try
        {
            _loadedPath.Clear();
            string dataDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nighttrain");
            string fullPath = System.IO.Path.Combine(dataDir, _filename);
            if (!System.IO.File.Exists(fullPath))
            {
                Notification.PostTicker("~y~No path file found to visualize.", true);
                _pathLoaded = false;
                return;
            }

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in System.IO.File.ReadLines(fullPath))
            {
                var s = line.Trim();
                if (s.Length == 0) continue;
                var parts = s.Split(' ');
                if (parts.Length < 4) continue;

                if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out float x) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out float y) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out float z) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, ci, out float h))
                {
                    _loadedPath.Add(new Vector4(x, y, z, h));
                }
            }

            _pathLoaded = _loadedPath.Count > 0;
            if (_pathLoaded)
                Notification.PostTicker($"Path loaded: ~g~{_loadedPath.Count}~s~ points.", true);
            else
                Notification.PostTicker("~y~Path file had no valid points.", true);
        }
        catch (Exception ex)
        {
            _pathLoaded = false;
            Notification.PostTicker($"~r~Path load failed:~s~ {ex.Message}", true);
        }
    }

    private int FindNearestPathIndex(Vector3 pos)
    {
        if (_loadedPath.Count == 0) return -1;

        // Search locally around the last best index to keep it cheap.
        int searchStart, searchEnd;
        if (_nearestPathIdx < 0)
        {
            // First time: full scan (fine for a few hundred points)
            searchStart = 0;
            searchEnd = _loadedPath.Count - 1;
        }
        else
        {
            // Subsequent frames: search a band around the last best
            int band = (PathWindowBehind + PathWindowAhead) * 2 + 20; // generous band
            searchStart = Math.Max(0, _nearestPathIdx - band);
            searchEnd = Math.Min(_loadedPath.Count - 1, _nearestPathIdx + band);
        }

        int bestIdx = Math.Max(0, _nearestPathIdx);
        float bestDist2 = float.MaxValue;

        for (int i = searchStart; i <= searchEnd; i++)
        {
            var v = _loadedPath[i];
            float dx = pos.X - v.X;
            float dy = pos.Y - v.Y;
            float dz = pos.Z - v.Z;
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                bestIdx = i;
            }
        }

        _nearestPathIdx = bestIdx;
        return bestIdx;
    }

    private void DrawPathMarkersWindowed()
    {
        if (_loadedPath.Count == 0) return;

        // Use engine if alive, else player
        Entity target = (_target != null && _target.Exists())
            ? _target
            : Game.Player.Character;

        int center = FindNearestPathIndex(target.Position);
        if (center < 0) return;

        int start = Math.Max(0, center - PathWindowBehind);
        int end = Math.Min(_loadedPath.Count - 1, center + PathWindowAhead);

        // draw small gradient around the center
        int total = Math.Max(1, end - start);
        for (int i = start; i <= end; i++)
        {
            var v = _loadedPath[i];
            var pos = new Vector3(v.X, v.Y, v.Z);

            // Color: center bright, fades to ends
            float t = (float)(i - start) / total; // 0..1
                                                  // teal → purple blend
            int r = (int)(255 * t);
            int g = (int)(200 * (1f - Math.Abs(t - 0.5f) * 2f)); // bump mid
            int b = (int)(255 * (1f - t));
            var col = Color.FromArgb(200, r, g, b);

            World.DrawMarker(
                MarkerType.Sphere,
                pos,
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(0.8f, 0.8f, 0.8f),
                col,
                false,  // bob
                true,   // faceCamera
                false,
                null, null,
                false
            );

            // Optional: tiny forward “arrow” using heading in v.W
            // Draw a little line in heading direction to visualize orientation
            // (uncomment if you like)
            /*
            float rad = v.W * (float)Math.PI / 180f;
            var dir = new Vector3((float)Math.Sin(rad), (float)Math.Cos(rad), 0f);
            World.DrawLine(pos, pos + dir * 1.5f, System.Drawing.Color.FromArgb(200, 255, 255, 255));
            */
        }

        // Emphasize absolute start of the path (not just window start)
        var first = _loadedPath[0];

        World.DrawMarker(
            MarkerType.Cone,
            new Vector3(first.X, first.Y, first.Z + 0.8f),
            Vector3.Zero, Vector3.Zero,
            new Vector3(1.0f, 1.0f, 1.4f),
            Color.FromArgb(220, 0, 255, 0),
            false, true, false, null, null, false
        );
    }
}

public class TrainSystem : ModSubsystemBase
{
    private readonly List<Entity> _spawned = new List<Entity>();
    private Vector3 _startPosition;
    private readonly float _startHeading;
    private const float StartSpeed = 100f;

    static readonly string[] TrainModels = new[]{
        "freight", "metrotrain", "freightcont1", "freightcar",
        "freightcar2", "freightcont2", "tankercar", "freightgrain"
    };

    public TrainSystem(Vector3 startPosition, float startHeading)
    {
        _startPosition = startPosition;
        _startHeading = startHeading;
    }

    public override void Start()
    {
        InitPlayer();
        SetupTrain();
    }

    public override void Stop()
    {
        foreach (var e in _spawned)
        {
            if (e != null && e.Exists()) e.Delete();
        }

        _spawned.Clear();

        Function.Call(Hash.SET_RANDOM_TRAINS, true);
    }

    private void InitPlayer()
    {
        var player = Game.Player.Character;

        player.Position = _startPosition;
        player.Heading = _startHeading;
    }

    List<Model> _loadedTrainModels = new List<Model>();

    void LoadTrainModelsOrThrow(int timeoutMs = 8000)
    {
        _loadedTrainModels.Clear();

        foreach (var name in TrainModels)
        {
            var model = new Model(name);
            if (!model.IsInCdImage || !model.IsValid)
                throw new Exception($"Train model not valid/in CD image: {name}");

            model.Request();
            int start = Game.GameTime;
            while (!model.IsLoaded && Game.GameTime - start < timeoutMs)
            {
                Script.Yield(); // let the game load
            }
            if (!model.IsLoaded)
                throw new Exception($"Timed out loading model: {name}");

            _loadedTrainModels.Add(model);
        }
    }

    void ReleaseTrainModels()
    {
        foreach (var m in _loadedTrainModels)
            m.MarkAsNoLongerNeeded();
        _loadedTrainModels.Clear();
    }

    // Fields
    private int _engineHandle;
    public Entity Engine { get; private set; }
    private readonly List<Entity> _cars = new List<Entity>(); // carriages as Entities

    private void SetupTrain()
    {
        // required before SET_RANDOM_TRAINS
        Function.Call(Hash.SWITCH_TRAIN_TRACK, 0, true);
        Function.Call(Hash.SET_TRAIN_TRACK_SPAWN_FREQUENCY, 0, 120000);

        Function.Call(Hash.SET_RANDOM_TRAINS, false);

        LoadTrainModelsOrThrow();
        _engineHandle = Function.Call<int>(Hash.CREATE_MISSION_TRAIN, 0, _startPosition.X, _startPosition.Y, _startPosition.Z, true, true, true);
        ReleaseTrainModels();

        if (_engineHandle == 0) { Notification.PostTicker("~r~Failed to create train", true); return; }

        Engine = Entity.FromHandle(_engineHandle);
        if (Engine == null || !Engine.Exists())
        {
            Notification.PostTicker("~r~Engine invalid", true);
            return;
        }

        _spawned.Add(Engine);
        _cars.Clear();

        HardenTrainEntity(Engine);

        // Collect carriages (indices 1..N)
        for (int i = 1; i < 30; i++)
        {
            int carHandle = Function.Call<int>(Hash.GET_TRAIN_CARRIAGE, _engineHandle, i);
            if (carHandle == 0) break;

            var car = Entity.FromHandle(carHandle);
            if (car != null && car.Exists())
            {
                HardenTrainEntity(car);
                _cars.Add(car);
                _spawned.Add(car);
            }
        }

        // Seat player in engine cab if possible
        try { Function.Call(Hash.SET_PED_INTO_VEHICLE, Game.Player.Character.Handle, _engineHandle, -1); } catch { }

        // Initial speed (mph?)
        Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, Engine.Handle, StartSpeed);

        Notification.PostTicker("~g~Train ready", true);
    }

    private void HardenTrainEntity(Entity e)
    {
        if (e == null || !e.Exists()) return;

        e.IsPersistent = true;

        // Ownership/mission
        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, e.Handle, true, true);
        Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, e.Handle, true);

        // Core “god mode” flags
        e.IsInvincible = true;
        Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, e.Handle, false);

        // Bullet/fire/explosion/physics proofing (covers many ChaosMod effects)
        // SET_ENTITY_PROOFS(entity, bullet, fire, explosion, collision, melee, steam, p7, drown)
        Function.Call(Hash.SET_ENTITY_PROOFS, e.Handle, true, true, true, true, true, true, true, true);

        // Vehicle-specific hardening
        if (e is Vehicle v && v.Exists())
        {
            // Make it “tough”
            Function.Call(Hash.SET_VEHICLE_STRONG, v.Handle, true);
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, v.Handle, false);
            Function.Call(Hash.SET_DISABLE_VEHICLE_PETROL_TANK_DAMAGE, v.Handle, true);
            Function.Call(Hash.SET_DISABLE_VEHICLE_PETROL_TANK_FIRES, v.Handle, true);
            Function.Call(Hash.SET_VEHICLE_ENGINE_CAN_DEGRADE, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE, v.Handle, false);

            // Doors locked just to keep chaos scripts from yeeting you out
            try { Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, v.Handle, 4); } catch { }

            // Big health pool and top it up
            Function.Call(Hash.SET_ENTITY_MAX_HEALTH, v.Handle, 10000);
            Function.Call(Hash.SET_ENTITY_HEALTH, v.Handle, 10000);
            Function.Call(Hash.SET_VEHICLE_ENGINE_HEALTH, v.Handle, 10000.0f);
            Function.Call(Hash.SET_VEHICLE_PETROL_TANK_HEALTH, v.Handle, 10000.0f);
            Function.Call(Hash.SET_VEHICLE_BODY_HEALTH, v.Handle, 1000.0f);
        }

        // LOD so it doesn’t pop under heavy load
        Function.Call(Hash.SET_ENTITY_LOD_DIST, e.Handle, 0xFFFF);
    }
}
