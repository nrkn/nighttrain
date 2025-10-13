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
    private readonly NightTrainConfig config;

    private readonly List<IModSubsystem> _subsystems = new List<IModSubsystem>();
    private bool _isRunning;

    private readonly TrainPath _trainPath;

    public NightTrainMod()
    {
        config = new NightTrainConfig();

        _trainPath = new TrainPath(config.General);

        var startupSound = new StartupSoundSystem();
        var death = new DeathSystem(config.General, Restart);
        var train = new TrainSystem(config.General);
        var progress = new PathProgressSystem(_trainPath, () => train.Engine, OnMarker);
        var progressHud = new ProgressHudSystem(config.ProgressHud, progress);

        _subsystems.Add(startupSound);
        _subsystems.Add(death);
        _subsystems.Add(train);
        _subsystems.Add(progress);
        _subsystems.Add(progressHud);


        if (config.General.RecordPath)
        {
            var pathRecorder = new RecordPathSystem(config.General, train.Engine);

            _subsystems.Add(pathRecorder);
        }

        if (config.General.ShowPath)
        {
            var debugPathViewer = new DebugPathViewerSystem(_trainPath, progress);

            _subsystems.Add(debugPathViewer);

            Notification.PostTicker("Showing Debug Path", true);
        }


        KeyUp += OnKeyUp;
        Tick += OnTick;
        Interval = 0;

        Notification.PostTicker("~p~Night Train~s~ loaded (F5 to start, F6 to stop).", true);
    }

    void OnMarker(int mark) {
        // win condition - we will make it fancy later, eg spawn some peds to cheer, fireworks etc
        if (mark == _trainPath.Positions.Count - 1) {
            Notification.PostTicker("YOU WIN!", true);
        }

        // various decisions about spawning impending scenarios to go here
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
            if (_isRunning)
            {
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
        foreach (var sys in _subsystems)
        {
            sys.Start();
        }

        _isRunning = true;
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

public class NightTrainConfig
{
    public NightTrainConfig()
    {
        var cfg = ScriptSettings.Load(@"scripts\NightTrain.ini");

        General = new GeneralConfig(cfg);
        ProgressHud = new ProgressHudConfig(cfg);
    }

    public GeneralConfig General { get; private set; }
    public ProgressHudConfig ProgressHud { get; private set; }
}

public class GeneralConfig
{
    public GeneralConfig(ScriptSettings cfg)
    {
        RecordPath = cfg.GetValue("General", "RecordPath", false);
        ShowPath = cfg.GetValue("General", "ShowPath", false);
        ShowProgress = cfg.GetValue("General", "ShowProgress", true);
        PathFile = cfg.GetValue("General", "PathFile", "nighttrain_path.txt");

        var startX = cfg.GetValue("General", "StartX", 926.74f);
        var startY = cfg.GetValue("General", "StartY", 6436.79f);
        var startZ = cfg.GetValue("General", "StartZ", 32.12f);

        StartPosition = new Vector3(startX, startY, startZ);

        StartHeading = cfg.GetValue("General", "StartHeading", 265.6f);
        StartSpeed = cfg.GetValue("General", "StartSpeed", 100f);
        TrackIndex = cfg.GetValue("General", "TrackIndex", 0);
    }

    public bool RecordPath { get; private set; }
    public bool ShowPath { get; private set; }
    public bool ShowProgress { get; private set; }
    public string PathFile { get; private set; }
    public Vector3 StartPosition { get; private set; }
    public float StartHeading { get; private set; }
    public float StartSpeed { get; private set; }
    public int TrackIndex { get; private set; }
}

public class ProgressHudConfig
{
    public ProgressHudConfig(ScriptSettings cfg)
    {
        Enabled = cfg.GetValue("ProgressHud", "Enabled", true);
        FontScale = cfg.GetValue("ProgressHud", "FontScale", 0.5f);
        PosX = cfg.GetValue("ProgressHud", "PosX", 20);
        PosY = cfg.GetValue("ProgressHud", "PosY", 20);
        UseShadow = cfg.GetValue("ProgressHud", "UseShadow", true);
    }

    public bool Enabled { get; private set; }
    public float FontScale { get; private set; }
    public int PosX { get; private set; }
    public int PosY { get; private set; }
    public bool UseShadow { get; private set; }
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

    public DeathSystem(GeneralConfig cfg, Action onRestart)
    {
        _startPosition = cfg.StartPosition;
        _startHeading = cfg.StartHeading;
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
    private const int SampleEveryMs = 500;              // keep time-based sampling
    private const float LoopDistanceThreshold = 3.0f;   // meters
    private const float LoopHeadingTolerance = 15.0f;   // degrees (0..180)
    private const int MinSamplesBeforeLoop = 50;

    private enum RecordingState { Idle, Warmup, Recording, Done }

    private Vector3 _startPosition;
    private readonly float _startHeading;
    private readonly Entity _target;
    private readonly List<Vector4> _path = new List<Vector4>();
    private readonly string _filename;

    private int _nextSampleAt = 0;
    private RecordingState _state = RecordingState.Idle;
    private bool _inGate = false;   // for edge detection
    private int _gatePasses = 0;

    public RecordPathSystem(GeneralConfig cfg, Entity target)
    {
        _startPosition = cfg.StartPosition;
        _startHeading = cfg.StartHeading;
        _filename = cfg.PathFile;
        _target = target;
    }

    public override void Start()
    {
        _path.Clear();
        _nextSampleAt = Game.GameTime; // sample immediately (once recording)
        _state = RecordingState.Warmup; // ← start with a warmup lap
        _inGate = false;
        _gatePasses = 0;
    }

    public override void Stop()
    {
        _state = RecordingState.Idle;
        _path.Clear();
        _inGate = false;
        _gatePasses = 0;
    }

    public override void Tick()
    {
        if (_state == RecordingState.Idle || _state == RecordingState.Done) return;

        // detect gate crossings and transition state
        UpdateGateStateAndMaybeAdvance();

        // only sample during the Recording state
        if (_state == RecordingState.Recording)
            MaybeSamplePathTimeBased();
    }

    private void UpdateGateStateAndMaybeAdvance()
    {
        Entity target = (_target != null && _target.Exists())
            ? _target
            : Game.Player.Character;

        var now = target.Position;
        float hNow = target.Heading;

        bool withinGate =
            now.DistanceTo(_startPosition) <= LoopDistanceThreshold &&
            HeadingDeltaDegrees(hNow, _startHeading) <= LoopHeadingTolerance;

        // Rising edge: outside → inside
        if (withinGate && !_inGate)
        {
            _gatePasses++;

            if (_state == RecordingState.Warmup && _gatePasses == 1)
            {
                // Begin recording on pass #1
                _state = RecordingState.Recording;
                _path.Clear();
                _nextSampleAt = Game.GameTime; // start sampling immediately
                Notification.PostTicker("~p~Night Train~s~ recording started.", true);
            }
            else if (_state == RecordingState.Recording && _gatePasses == 2)
            {
                // Completed one full recorded lap; save & stop on pass #2
                if (_path.Count >= MinSamplesBeforeLoop)
                {
                    WritePath();
                    Notification.PostTicker("~p~Night Train~s~ path saved.", true);
                }
                else
                {
                    Notification.PostTicker("~y~Night Train~s~ recording aborted (too few samples).", true);
                }

                _state = RecordingState.Done;
                _path.Clear();
            }
        }

        _inGate = withinGate; // update for edge detection
    }

    private void MaybeSamplePathTimeBased()
    {
        if (Game.GameTime < _nextSampleAt) return;
        _nextSampleAt = Game.GameTime + SampleEveryMs;

        Entity target = (_target != null && _target.Exists())
            ? _target
            : Game.Player.Character;

        var p = target.Position;
        float h = target.Heading;

        _path.Add(new Vector4(p.X, p.Y, p.Z, h));
    }

    private static float HeadingDeltaDegrees(float a, float b)
    {
        float d = Math.Abs(a - b) % 360f;
        return d > 180f ? 360f - d : d;
    }

    private void WritePath()
    {
        try
        {
            string dataDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nighttrain");
            System.IO.Directory.CreateDirectory(dataDir);
            string fullPath = System.IO.Path.Combine(dataDir, _filename);

            using (var sw = new System.IO.StreamWriter(fullPath, false, System.Text.Encoding.UTF8))
            {
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
    private readonly TrainPath _trainPath;
    private readonly PathProgressSystem _progress;

    public DebugPathViewerSystem(TrainPath trainPath, PathProgressSystem progress)
    {
        _trainPath = trainPath;
        _progress = progress;
    }

    public override void Tick()
    {
        var positions = _trainPath.Positions;

        if (positions.Count == 0) return;

        int center = _progress.NextIndex;

        if (center < 0) return;

        int start = Math.Max(0, center - TrainPath.PathWindowBehind);
        int end = Math.Min(positions.Count - 1, center + TrainPath.PathWindowAhead);

        // draw small gradient around the center
        int total = Math.Max(1, end - start);
        for (int i = start; i <= end; i++)
        {
            var v = positions[i];
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
        }

        // Emphasize absolute start of the path (not just window start)
        var first = positions[0];

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
    private readonly float _startSpeed;

    static readonly string[] TrainModels = new[]{
        "freight", "metrotrain", "freightcont1", "freightcar",
        "freightcar2", "freightcont2", "tankercar", "freightgrain"
    };

    public TrainSystem(GeneralConfig cfg)
    {
        _startPosition = cfg.StartPosition;
        _startHeading = cfg.StartHeading;
        _startSpeed = cfg.StartSpeed;
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
        Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, Engine.Handle, _startSpeed);

        Notification.PostTicker($"~g~Train ready: ${ Engine.Handle }", true);
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

// broken - we are going to replace with a system that keeps track of the position on the path for us
public class ProgressHudSystem : ModSubsystemBase
{
    private readonly bool _useShadow;
    private readonly float _fontScale;
    private readonly int _posX;
    private readonly int _posY;
    private readonly PathProgressSystem _progress;


    public ProgressHudSystem(ProgressHudConfig cfg, PathProgressSystem progress)
    {
        _fontScale = cfg.FontScale;
        _posX = cfg.PosX;
        _posY = cfg.PosY;
        _useShadow = cfg.UseShadow;
        _progress = progress;
    }

    public override void Start()
    {
        Notification.PostTicker($"Progress HUD: ~g~{_progress.Length}~s~ points.", true);
    }

    public override void Tick()
    {
        int total = _progress.Length;
        int n = Math.Min(_progress.PreviousIndex + 1, total); // 1-based for display
        float pct = (total > 0) ? (100f * n / total) : 0f;

        DrawText($"{n}/{total}  ({pct:0.0}%)", _posX, _posY, _fontScale, _useShadow);
    }

    private TextElement _el;

    private void DrawText(string text, int x, int y, float scale, bool shadow)
    {
        if (_el == null)
        {
            _el = new TextElement(text, new PointF(x, y), scale, Color.White, GTA.UI.Font.ChaletLondon, Alignment.Left, shadow, true)
            {
                Enabled = true
            };
        }
        else
        {
            _el.Caption = text;
        }

        _el.Draw();
    }
}


public class TrainPath
{
    private readonly string _filename;

    public const int PathWindowBehind = 50;  // how many points behind the nearest to draw
    public const int PathWindowAhead = 70;  // how many points ahead to draw


    public TrainPath(GeneralConfig cfg)
    {
        _filename = cfg.PathFile;
        Positions = new List<Vector4>();
        Reload();
    }

    public List<Vector4> Positions { get; private set; }

    public bool Reload()
    {
        try
        {
            Positions.Clear();
            string dataDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nighttrain");
            string fullPath = System.IO.Path.Combine(dataDir, _filename);

            if (!System.IO.File.Exists(fullPath))
            {
                Notification.PostTicker("~y~No path file found.", true);

                return false;
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
                    Positions.Add(new Vector4(x, y, z, h));
                }
            }

            var pathLoaded = Positions.Count > 0;

            if (pathLoaded)
                Notification.PostTicker($"Path loaded: ~g~{Positions.Count}~s~ points.", true);
            else
                Notification.PostTicker("~y~Path file had no valid points.", true);

            return pathLoaded;
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Path load failed:~s~ {ex.Message}", true);

            return false;
        }
    }
}

public class PathProgressSystem : ModSubsystemBase
{
    private const float PointEpsilon = 0.25f;
    private readonly TrainPath _trainPath;
    private readonly Func<Entity> _getTarget;
    private readonly Action<int> _onMarker;

    public int PreviousIndex { get; private set; }
    public int NextIndex { get; private set; }
    public float Distance { get; private set; }
    public int Length { get; private set; }

    // NEW: support looped paths
    private readonly bool _loop = true; // or pass via ctor/config if you want
    public int LapsCompleted { get; private set; } = 0;

    public PathProgressSystem(TrainPath trainPath, Func<Entity> getTarget, Action<int> onMarker)
    {
        _trainPath = trainPath;
        _getTarget = getTarget;
        _onMarker = onMarker;
    }

    public void FirstTick()
    {
        var target = _getTarget?.Invoke();
        if (target == null || !target.Exists()) return;

        if (_trainPath.Positions.Count < 2)
            throw new InvalidOperationException("Path must contain at least 2 points.");

        Length = _trainPath.Positions.Count;

        PreviousIndex = 0;
        NextIndex = 1;
        Distance = 0f;
        LapsCompleted = 0;

        // Snap to wherever the train actually is
        SnapForwardToSegmentContaining(target.Position, emitMarkers: false);

        _firstTime = false;
    }

    public override void Stop()
    {
        PreviousIndex = 0;
        NextIndex = 0;
        Distance = 0f;

        // IMPORTANT: ensure we re-init on next Start
        _firstTime = true;
        LapsCompleted = 0;
    }

    private bool _firstTime = true;

    public override void Tick()
    {
        if (_firstTime) FirstTick();

        if (_trainPath.Positions.Count < 2) return;

        var target = _getTarget?.Invoke();
        if (target == null || !target.Exists()) return;

        SnapForwardToSegmentContaining(target.Position, emitMarkers: true);
    }

    private void SnapForwardToSegmentContaining(Vector3 worldPos, bool emitMarkers)
    {
        int count = _trainPath.Positions.Count;

        while (true)
        {
            // --- Handle wrapping/clamping at the end ---
            if (NextIndex >= count)
            {
                if (_loop)
                {
                    // We just finished the last marker in the previous step -> new lap
                    LapsCompleted++;
                    PreviousIndex = count - 1;
                    NextIndex = 0;
                    Distance = 0f;
                    // continue to evaluate the last->first segment
                }
                else
                {
                    // Non-loop: clamp at end
                    PreviousIndex = count - 2;
                    NextIndex = count - 1;
                    Distance = 1f - float.Epsilon;
                    return;
                }
            }

            var a = AsV3(_trainPath.Positions[PreviousIndex]);
            var b = AsV3(_trainPath.Positions[NextIndex]);

            var seg = b - a;
            float segLenSq = seg.LengthSquared();
            if (segLenSq < 1e-6f)
            {
                if (emitMarkers) _onMarker(NextIndex);
                PreviousIndex = NextIndex;
                NextIndex = NextIndex + 1; // may wrap at top of loop
                Distance = 0f;
                continue;
            }

            float t = Vector3.Dot(worldPos - a, seg) / segLenSq;

            if (t < 0f)
            {
                Distance = 0f;
                return;
            }

            var closest = a + seg * MathUtil.Clamp(t, 0f, 1f);
            bool withinPoint = (closest - b).Length() <= PointEpsilon;

            if (t >= 1f || withinPoint)
            {
                if (emitMarkers) _onMarker(NextIndex);
                PreviousIndex = NextIndex;
                NextIndex = NextIndex + 1; // may wrap at top of loop
                Distance = 0f;
                continue;
            }

            Distance = MathUtil.Clamp(t, 0f, 1f - float.Epsilon);
            return;
        }
    }

    private static Vector3 AsV3(in Vector4 v) => new Vector3(v.X, v.Y, v.Z);

    private static class MathUtil
    {
        public static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);
    }
}
