using System.Media;

public class StartupSoundSystem : ModSubsystemBase
{
    private SoundPlayer _soundPlayer;

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
