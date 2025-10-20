using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.UI;

public class NightTrainMod : Script
{
    private NightTrainConfig _config;

    private List<IModSubsystem> _subsystems;
    private bool _isRunning;

    private TrainPath _trainPath;

    private Random _random;

    private ScenarioSystem scenario;

    public NightTrainMod()
    {
        KeyUp += OnKeyUp;
        Tick += OnTick;
        Interval = 0;

        Notification.PostTicker("~p~Night Train~s~ loaded (F5 to start, F6 to stop).", true);
    }

    void OnMarker(int mark)
    {
        // win condition - we will make it fancy later, eg spawn some peds to cheer, fireworks etc
        if (mark == _trainPath.Positions.Count - 1)
        {
            Notification.PostTicker("YOU WIN!", true);

            // should stop but we'll just keep doing laps while we debug
        }

        scenario.OnMarker(mark);
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

    private void Init()
    {        
        _subsystems = new List<IModSubsystem>();
        _config = new NightTrainConfig();
        _random = new Random(_config.General.Seed);

        _trainPath = new TrainPath(_config.General);

        var startupSound = new StartupSoundSystem();
        var death = new DeathSystem(_config.General, Restart);
        var train = new TrainSystem(_config.General);

        Func<Entity> getEngine = () => train.Engine;

        var progress = new PathProgressSystem(_trainPath, getEngine, OnMarker);
        var progressHud = new ProgressHudSystem(_config.ProgressHud, progress);
        var turret = new TurretPlatformSystem(getEngine, _config);
        //var gunner = new InvisibleGunnerSystem(getEngine);

        scenario = new ScenarioSystem(_trainPath, () => _random);

        _subsystems.Add(startupSound);
        _subsystems.Add(death);
        _subsystems.Add(train);
        _subsystems.Add(progress);
        _subsystems.Add(progressHud);
        _subsystems.Add(scenario);
        _subsystems.Add(turret);
        //_subsystems.Add(gunner);

        if (_config.General.RecordPath)
        {
            var pathRecorder = new RecordPathSystem(_config.General, train.Engine);

            _subsystems.Add(pathRecorder);
        }

        if (_config.General.ShowPath)
        {
            var debugPathViewer = new DebugPathViewerSystem(_trainPath, progress);

            _subsystems.Add(debugPathViewer);

            Notification.PostTicker("Showing Debug Path", true);
        }
    }

    private void Start()
    {
        Init();

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

    private void OnTick(object sender, EventArgs e)
    {
        if (!_isRunning) return;

        foreach (var sys in _subsystems)
        {
            sys.Tick();
        }
    }
}
