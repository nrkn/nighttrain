using GTA;

public class NightTrainConfig
{
    public NightTrainConfig()
    {
        Reload();
    }

    public void Reload()
    {
        var cfg = ScriptSettings.Load(@"scripts\NightTrain.ini");

        General = new GeneralConfig(cfg);
        ProgressHud = new ProgressHudConfig(cfg);
        Debug = new DebugConfig(cfg); // NEW
    }

    public GeneralConfig General { get; private set; }
    public ProgressHudConfig ProgressHud { get; private set; }
    public DebugConfig Debug { get; private set; } 
}
