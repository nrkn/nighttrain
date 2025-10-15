using System;
using System.Linq;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

// Spawns an invisible weaponized vehicle, attaches it to the train engine, and seats the player
// in a turret seat if available (fallback: passenger for drive-by). This gives reliable aiming/shooting
// while the train moves, without clearance issues.
public class TurretPlatformSystem : ModSubsystemBase
{
    private readonly Func<Entity> _getEngine;

    // Candidate vehicles that support turrets/gunner seats. We'll try in order.
    private static readonly string[] TurretVehicleModels = new[]
    {
        // Common GTA V weaponized platforms (feel free to tweak order)

        // pretty good - has turret, turns correctly, player is vulnerable, looks good
        "technical",    // Technical
        "technical2",   // Technical Aqua/variant
        "technical3",   // Technical Custom
        "halftrack",    // Half-Track

        // eh, they're ok - player is getting set into wrong seat though
        "menacer",      // Menacer (roof turret)
        "barrage",      // Barrage (mounted guns)
        "caracara",     // Caracara (turret bed)
        "caracara2",    // Caracara 4x4

        // really good but overpowered - so easy lol - maybe use sparingly, as temp powerups?
        // we could overcome this partially by overriding the explosive rounds to use a normal weapon
        // however - the player is not vulnerable while inside the tank and we currently use player death as fail state
        "rhino",        // Rhino Tank 
        "khanjali",    // Khanjali Tank

        // cool but target crosshairs are restricted to a range that only allows shooting out the left of the vehicle
        "apc",          // APC (driver cannon; different feel)


        
        // player is placed in vehicle seat - so in vehicle shooting - limited range, can't shoot on some angles - not very good
        "insurgent2",   // Insurgent Pick-Up
        "insurgent3",   // Insurgent Pick-Up Custom
    };

    private Vehicle _platform;
    private Ped _player;
    private bool _seated;
    private bool _attached;
    private bool _followOnly; // tanks follow engine instead of attaching to preserve turret rotation

    // Attachment tuning (relative to engine origin)
    private readonly Vector3 _attachOffset = new Vector3(0f, 0f, 2.0f); // a bit above cab
    private readonly Vector3 _attachRot = Vector3.Zero; // yaw/pitch/roll degrees

    public TurretPlatformSystem(Func<Entity> getEngine)
    {
        _getEngine = getEngine;
    }

    public override void Start()
    {
        _player = Game.Player.Character;
        TryCreateAndAttachPlatform();
    }

    public override void Stop()
    {
        SafeCleanup();
    }

    public override void Tick()
    {
        // Keep platform healthy and reattach if needed (engine can respawn across restarts)
        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;

        if (_platform == null || !_platform.Exists())
        {
            TryCreateAndAttachPlatform();
            return;
        }

        // Prefer follow-only for all supported platforms to avoid attach yaw constraints
        _followOnly = true;

        if (_followOnly)
        {
            var desired = engine.Position + _attachOffset;
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _platform.Handle, desired.X, desired.Y, desired.Z, true, true, true);
            try { _platform.Heading = engine.Heading; } catch { }
        }
        else if (!_attached)
        {
            AttachToEngine(engine);
        }

        if (!_seated)
        {
            SeatPlayerPreferTurret();
        }

        // Keep it invisible and intangible every tick (belt and braces)
        KeepInvisibleIntangible(_platform);
    }

    private void TryCreateAndAttachPlatform()
    {
        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;

        // Pick the first available model we can load
        Model picked = null;
        foreach (var name in TurretVehicleModels)
        {
            var m = new Model(name);
            if (!m.IsInCdImage) { m.MarkAsNoLongerNeeded(); continue; }
            m.Request(2000);
            if (m.IsLoaded) { picked = m; break; }
            m.MarkAsNoLongerNeeded();
        }

        if (picked == null)
        {
            Notification.PostTicker("~y~Turret platform: no weaponized model available, using fallback.", true);
            picked = new Model(VehicleHash.Blazer); // simple small vehicle
            picked.Request(1500);
        }

        var pos = engine.Position + _attachOffset;
        _platform = World.CreateVehicle(picked, pos, engine.Heading);
        picked.MarkAsNoLongerNeeded();

        if (_platform == null || !_platform.Exists())
        {
            Notification.PostTicker("~r~Failed to create turret platform vehicle.", true);
            return;
        }

        // Mission entity and persistence to avoid random cleanup
        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _platform.Handle, true, true);
        _platform.IsPersistent = true;

        // Invisible + intangible
        KeepInvisibleIntangible(_platform);

        // Follow-only to keep heading/rotation natural and avoid sideways attach
        _followOnly = true;
        try { _platform.Heading = engine.Heading; } catch { }
        // No attach in follow-only mode
        SeatPlayerPreferTurret();

        Notification.PostTicker("Gun platform ready.", true);
    }

    private void AttachToEngine(Entity engine)
    {
        if (_platform == null || !_platform.Exists()) return;
        if (engine == null || !engine.Exists()) return;

        // Attach flags tuned to follow rotation and position, no collision transfer
        // ATTACH_ENTITY_TO_ENTITY(entity, target, boneIndex, x, y, z, pitch, roll, yaw, p9, useSoftPinning, collision, isPed, vertexIndex, fixedRot, p15)
        Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY,
            _platform.Handle,
            engine.Handle,
            0,
            _attachOffset.X, _attachOffset.Y, _attachOffset.Z,
            _attachRot.X, _attachRot.Y, _attachRot.Z,
            false, // p9
            true,  // useSoftPinning
            false, // collision (false: don't collide)
            false, // isPed
            2,     // vertexIndex / bone (0/2 are common defaults)
            false, // fixedRot: allow local rotation
            true   // p15
        );

        _attached = true;
    }

    private void SeatPlayerPreferTurret()
    {
        if (_platform == null || !_platform.Exists()) return;
        if (_player == null || !_player.Exists()) return;

        try
        {
            // Try some likely turret/gunner seat indices first.
            // If none succeed, fallback to front passenger for drive-by shooting.
            int[] preferredSeats = new[] { 1, 2, 3, 4 };

            // Ensure vehicle is actually usable for weapons & UI
            try { _platform.IsUndriveable = false; } catch { }
            try { _platform.IsEngineRunning = true; } catch { }
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _platform.Handle, true, true, false);

            bool seated = false;
            // Tanks (rhino/khanjali) use the driver seat for the cannon.
            int modelHash = _platform.Model.Hash;
            int rhinoHash = new Model("rhino").Hash;
            int khanjaliHash = new Model("khanjali").Hash;
            bool tankDriverWeapon = (modelHash == rhinoHash) || (modelHash == khanjaliHash);

            if (tankDriverWeapon)
            {
                if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, _platform.Handle, -1))
                {
                    Function.Call(Hash.SET_PED_INTO_VEHICLE, _player.Handle, _platform.Handle, -1);
                    Script.Yield();
                    if (_player.IsInVehicle(_platform)) { seated = true; }
                }
            }
            else
            {
                foreach (var seatIndex in preferredSeats)
                {
                    if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, _platform.Handle, seatIndex))
                    {
                        Function.Call(Hash.SET_PED_INTO_VEHICLE, _player.Handle, _platform.Handle, seatIndex);
                        Script.Yield();
                        if (_player.IsInVehicle(_platform)) { seated = true; break; }
                    }
                }
            }

            if (!seated)
            {
                // Fallback: passenger seat (0)
                if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, _platform.Handle, 0))
                {
                    Function.Call(Hash.SET_PED_INTO_VEHICLE, _player.Handle, _platform.Handle, 0);
                }
                else
                {
                    // Last resort: driver seat (-1)
                    Function.Call(Hash.SET_PED_INTO_VEHICLE, _player.Handle, _platform.Handle, -1);
                }

                // Allow drive-by if we aren't in a turret
                Function.Call(Hash.SET_ENABLE_HANDCUFFS, _player.Handle, false);
                Function.Call(Hash.SET_PED_CAN_SWITCH_WEAPON, _player.Handle, true);
                Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, _player.Handle, false, false);
            }

            // Extra QoL: prevent getting yanked, keep tasks simple
            _player.BlockPermanentEvents = true;
            _player.KnockOffVehicleType = KnockOffVehicleType.Never;
            _player.CanBeDraggedOutOfVehicle = false;
            _player.IsOnlyDamagedByPlayer = false;

            _seated = _player.IsInVehicle(_platform);

            // Radio: enable control (note some vehicles like tanks simply have no radio)
            try { Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, _platform.Handle, true); } catch { }
            try { Function.Call(Hash.SET_USER_RADIO_CONTROL_ENABLED, true); } catch { }
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~y~Gun platform seat failed: {ex.Message}", true);
        }
    }

    private static void KeepInvisibleIntangible(Vehicle v)
    {
        if (v == null || !v.Exists()) return;

        // Visibility off and collision disabled

        // we may be keeping in on if we can get a good train engine + turret combo that doesn't look weird
        //Function.Call(Hash.SET_ENTITY_VISIBLE, v.Handle, false, false);

        Function.Call(Hash.SET_ENTITY_COLLISION, v.Handle, false, false);

        v.IsInvincible = true;
        v.IsPositionFrozen = false; // let attachment handle transform
        v.CanBeVisiblyDamaged = false;
        v.IsPersistent = true;

        // Keep it driveable so turret/driver weapons and UI work
        try { v.IsUndriveable = false; } catch { }
        try { v.IsEngineRunning = true; } catch { }
        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, v.Handle, true, true, false);

        // Also mute engine and stop lights for stealth - todo - these methods have changed, figure out what they are called now
        //v.SirenActive = false;
        //v.LightsOn = false;
    }

    private void SafeCleanup()
    {
        _attached = false;
        _seated = false;

        if (_platform != null)
        {
            try
            {
                if (_platform.Exists())
                {
                    // Detach first to avoid deleting attached entity crashes
                    Function.Call(Hash.DETACH_ENTITY, _platform.Handle, true, true);
                    _platform.Delete();
                }
            }
            catch { /* ignore */ }
            finally { _platform = null; }
        }
    }
}
