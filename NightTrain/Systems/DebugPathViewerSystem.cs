using System;
using System.Drawing;
using GTA;
using GTA.Math;

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
