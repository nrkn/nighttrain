using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.UI;

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
