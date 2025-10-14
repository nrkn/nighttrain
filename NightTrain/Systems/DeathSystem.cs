using System;
using GTA;
using GTA.Math;
using GTA.Native;

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
