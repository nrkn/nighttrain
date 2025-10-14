using System;
using System.Collections.Generic;
using GTA.Math;
using GTA.UI;

public class TrainPath
{
    private readonly string _filename;

    public const int PathWindowBehind = 10;  // how many points behind the nearest to draw
    public const int PathWindowAhead = 30;  // how many points ahead to draw
    public const int MarkerSpacing = 50; // approx every 50m - use the tools to find out spacing if you re-record the path


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
