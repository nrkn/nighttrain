using System;
using GTA;
using GTA.Math;

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
