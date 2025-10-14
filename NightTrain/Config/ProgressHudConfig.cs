using GTA;

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
