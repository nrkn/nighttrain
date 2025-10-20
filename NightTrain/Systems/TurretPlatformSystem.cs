using System;
using GTA;
using GTA.Math;
using GTA.Native;

// Minimal: spawns a configurable weaponized vehicle (default "technical"),
// attaches to the train engine, seats player in a configured seat (default turret seat = 1).
public class TurretPlatformSystem : ModSubsystemBase
{
    private readonly Func<Entity> _getEngine;
    private readonly NightTrainConfig _config;

    private Vehicle _platform;
    private Ped _player;
    private bool _attached;
    private bool _seated;

    public TurretPlatformSystem(Func<Entity> getEngine, NightTrainConfig config)
    {
        _getEngine = getEngine;
        _config = config;
    }

    public override void Start()
    {
        _player = Game.Player.Character;

        if (!_config.Debug.EnableTurretPlatform) return;

        EnsurePlatform();
        TryAttach();
        TrySeatTurret();
    }

    public override void Tick()
    {
        if (!_config.Debug.EnableTurretPlatform) return;

        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;

        // If the platform got deleted, recreate and reattach.
        if (_platform == null || !_platform.Exists())
        {
            EnsurePlatform();
            _attached = false;
            _seated = false;
        }

        if (!_attached) TryAttach();
        if (!_seated) TrySeatTurret();

        PlatformTick(_platform);
    }

    private void EnsurePlatform()
    {
        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;

        var model = new Model(_config.Debug.TurretModel);
        model.Request(2000);
        if (!model.IsLoaded) return;

        var pos = engine.Position + new Vector3(
            _config.Debug.AttachOffsetX,
            _config.Debug.AttachOffsetY,
            _config.Debug.AttachOffsetZ
        );

        _platform = World.CreateVehicle(model, pos, engine.Heading);
        model.MarkAsNoLongerNeeded();

        if (_platform == null || !_platform.Exists()) return;

        // Fix: align heading to engine on spawn to avoid awkward angle
        if (_config.Debug.SetHeadingOnSpawn)
        {
            try { _platform.Heading = engine.Heading; } catch { }
        }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _platform.Handle, true, true);
        _platform.IsPersistent = true;

        PlatformTick(_platform);
    }

    private void TryAttach()
    {
        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;
        if (_platform == null || !_platform.Exists()) return;

        // ATTACH_ENTITY_TO_ENTITY(entity, target, boneIndex, x,y,z, pitch,roll,yaw, p9, useSoftPin, collision, isPed, bone, fixedRot, p15)
        Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY,
            _platform.Handle,
            engine.Handle,
            0,
            _config.Debug.AttachOffsetX,
            _config.Debug.AttachOffsetY,
            _config.Debug.AttachOffsetZ,
            _config.Debug.AttachRotPitch,
            _config.Debug.AttachRotRoll,
            _config.Debug.AttachRotYaw,
            false,                                    // p9
            _config.Debug.UseSoftPinning,             // useSoftPinning
            !_config.Debug.DisableCollisionWhileAttached ? true : false, // collision flag
            true,                                    // isPed
            _config.Debug.AttachBoneIndex,            // bone index (0/2 common)
            _config.Debug.FixedRotation,              // fixedRot
            true                                      // p15
        );

        _attached = true;
    }

    private void TrySeatTurret()
    {
        if (_platform == null || !_platform.Exists()) return;
        if (_player == null || !_player.Exists()) return;

        // Direct seat assignment from config (default 1 for Technical turret)
        Function.Call(Hash.SET_PED_INTO_VEHICLE, _player.Handle, _platform.Handle, _config.Debug.TurretSeatIndex);
        Script.Yield();

        _seated = _player.IsInVehicle(_platform);
    }

    private void PlatformTick(Vehicle v)
    {
        if (v == null || !v.Exists()) return;

        // If you want the explicit control, this mirrors SET_ENTITY_COLLISION(entity, toggle, keepPhysics)
        if (_config.Debug.UseSetEntityCollision)
        {
            Function.Call(Hash.SET_ENTITY_COLLISION, v.Handle,
                _config.Debug.SetEntityCollision_Toggle,
                _config.Debug.SetEntityCollision_KeepPhysics);
        }
        else
        {
            // Legacy convenience switch: off means toggle=false, keepPhysics=false
            if (_config.Debug.DisableCollisionWhileAttached)
            {
                Function.Call(Hash.SET_ENTITY_COLLISION, v.Handle, false, false);
            }
        }

        v.IsPersistent = true;
    }

    public override void Stop()
    {
        _attached = false;
        _seated = false;

        if (_platform != null)
        {
            try
            {
                if (_platform.Exists())
                {
                    Function.Call(Hash.DETACH_ENTITY, _platform.Handle, true, true);
                    _platform.Delete();
                }
            }
            catch { }
            finally { _platform = null; }
        }
    }
}
