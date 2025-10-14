using GTA;
using GTA.Math;

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
        Seed = cfg.GetValue("General", "Seed", 66642069);
    }

    public bool RecordPath { get; private set; }
    public bool ShowPath { get; private set; }
    public bool ShowProgress { get; private set; }
    public string PathFile { get; private set; }
    public Vector3 StartPosition { get; private set; }
    public float StartHeading { get; private set; }
    public float StartSpeed { get; private set; }
    public int TrackIndex { get; private set; }
    public int Seed { get; private set; }
}
