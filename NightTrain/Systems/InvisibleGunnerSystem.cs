using System;
using GTA;
using GTA.Math;
using GTA.Native;

// Invisible gunner + on-screen reticle + 3D hit marker.
// Attach an invisible ped to the train, aim at camera ray, fire on Attack.
public class InvisibleGunnerSystem : ModSubsystemBase
{
    private readonly Func<Entity> _getEngine;

    // --- quick knobs (no external config) ---
    private bool _enabled = true;                         // master switch
    private Vector3 _attachOffset = new Vector3(0f, 0f, 2.0f); // relative to engine roof-ish

    private bool _usePlayerWeapon = false;                // default OFF for visibility
    private WeaponHash _fallbackWeapon = WeaponHash.Minigun; // very visible tracers / muzzle

    private int _accuracy = 100;                          // 0..100
    private int _shootRate = 1200;                        // shots/min hint for AI
    private string _firingPattern = "FULLAUTO";           // FULLAUTO | BURST_FIRE | SINGLE_SHOT
    private bool _aimThroughGlass = true;                 // loosen LOS heuristics

    // Reticle/marker
    private bool _drawReticle2D = true;                   // screen-space square at aim point
    private float _reticleSize = 0.008f;                  // fraction of screen (w,h)
    private byte _reticleAlpha = 220;                     // 0..255

    private bool _drawHitMarker3D = true;                 // small 3D marker where the ray hits
    private float _hitMarkerSize = 0.06f;                 // world units

    // --- internals ---
    private Ped _gunner;

    public InvisibleGunnerSystem(Func<Entity> getEngine) { _getEngine = getEngine; }

    public override void Start()
    {
        if (!_enabled) return;
        EnsureGunner();
        AttachGunner();
        ArmGunner();
    }

    public override void Stop() => Cleanup();

    public override void Tick()
    {
        if (!_enabled) { Cleanup(); return; }

        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists()) return;

        if (_gunner == null || !_gunner.Exists())
        {
            EnsureGunner();
            AttachGunner();
            ArmGunner();
        }

        // Keep attached (engine might respawn)
        if (!Function.Call<bool>(Hash.IS_ENTITY_ATTACHED_TO_ANY_OBJECT, _gunner.Handle))
            AttachGunner();

        if (_usePlayerWeapon) MirrorPlayerWeapon();

        // Build aim target from gameplay camera ray (prefer hit point)
        var camPos = GameplayCamera.Position;
        var camDir = GameplayCamera.Direction;
        var target = camPos + camDir * 5000f;

        var ray = World.Raycast(camPos, camDir, 5000f, IntersectFlags.Everything, Game.Player.Character);
        bool haveHit = ray.DidHit;
        if (haveHit) target = ray.HitPosition;

        // Draw reticle(s)
        if (_drawReticle2D) DrawReticle2D(target);
        if (_drawHitMarker3D && haveHit) DrawHitMarker3D(target);

        bool wantFire = Game.IsControlPressed(Control.Attack);

        if (wantFire)
        {
            // Short bursts, reissued each tick to keep tracking
            Function.Call(Hash.TASK_SHOOT_AT_COORD, _gunner.Handle, target.X, target.Y, target.Z, 75, GetFiringPatternHash());
        }
        else
        {
            Function.Call(Hash.TASK_AIM_GUN_AT_COORD, _gunner.Handle, target.X, target.Y, target.Z, 75, false, false);
        }
    }

    // --- helpers ---

    private void EnsureGunner()
    {
        var model = new Model(PedHash.Cop01SMY); model.Request(1500);
        if (!model.IsLoaded) return;

        _gunner = World.CreatePed(model, Game.Player.Character.Position);
        model.MarkAsNoLongerNeeded();
        if (_gunner == null || !_gunner.Exists()) return;

        // Invisible + intangible + invincible + no ambient AI
        Function.Call(Hash.SET_ENTITY_VISIBLE, _gunner.Handle, false, false);
        Function.Call(Hash.SET_ENTITY_COLLISION, _gunner.Handle, false, false);
        Function.Call(Hash.SET_ENTITY_INVINCIBLE, _gunner.Handle, true);
        _gunner.CanBeTargetted = false;
        _gunner.IsOnlyDamagedByPlayer = false;
        _gunner.CanRagdoll = false;
        _gunner.BlockPermanentEvents = true;

        Function.Call(Hash.SET_PED_CONFIG_FLAG, _gunner.Handle, 281, true); // DisableShockingEvents
        Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, _gunner.Handle, 1);
    }

    private void AttachGunner()
    {
        var engine = _getEngine?.Invoke();
        if (engine == null || !engine.Exists() || _gunner == null || !_gunner.Exists()) return;

        // Smooth, no collision transfer
        Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY,
            _gunner.Handle, engine.Handle, 0,
            _attachOffset.X, _attachOffset.Y, _attachOffset.Z,
            0f, 0f, 0f,
            false,   // p9
            true,    // soft pin
            false,   // collision transfer
            false,   // isPed
            0,       // vertex
            false,   // fixed rot
            true
        );
    }

    private void ArmGunner()
    {
        if (_gunner == null || !_gunner.Exists()) return;

        if (!_usePlayerWeapon)
        {
            uint hash = (uint)_fallbackWeapon;
            Function.Call(Hash.GIVE_WEAPON_TO_PED, _gunner.Handle, hash, 9999, false, true);
            Function.Call(Hash.SET_CURRENT_PED_WEAPON, _gunner.Handle, hash, true);
        }

        Function.Call(Hash.SET_PED_INFINITE_AMMO, _gunner.Handle, true, 0);
        Function.Call(Hash.SET_PED_ACCURACY, Clamp(_accuracy, 0, 100));
        Function.Call(Hash.SET_PED_SHOOT_RATE, Math.Max(1, _shootRate));
        Function.Call(Hash.SET_PED_FIRING_PATTERN, GetFiringPatternHash());

        Function.Call(Hash.SET_PED_COMBAT_MOVEMENT, _gunner.Handle, 0); // stationary
        Function.Call(Hash.SET_PED_COMBAT_ABILITY, _gunner.Handle, 2); // pro
        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _gunner.Handle, 46, true); // AlwaysFight
        Function.Call(Hash.SET_PED_CAN_SWITCH_WEAPON, _gunner.Handle, false);

        if (_aimThroughGlass)
        {
            Function.Call(Hash.SET_PED_SEEING_RANGE, _gunner.Handle, 9999f);
            Function.Call(Hash.SET_PED_HEARING_RANGE, _gunner.Handle, 0f);
        }
    }

    private void MirrorPlayerWeapon()
    {
        var p = Game.Player.Character;
        if (p == null || !p.Exists() || _gunner == null || !_gunner.Exists()) return;
        var cur = p.Weapons.Current; if (cur == null) return;

        uint hash = (uint)cur.Hash;
        Function.Call(Hash.GIVE_WEAPON_TO_PED, _gunner.Handle, hash, 9999, false, true);
        Function.Call(Hash.SET_CURRENT_PED_WEAPON, _gunner.Handle, hash, true);
    }

    private int GetFiringPatternHash()
    {
        switch ((_firingPattern ?? "FULLAUTO").ToUpperInvariant())
        {
            case "BURST_FIRE": return unchecked((int)0xD6FF6D61);
            case "SINGLE_SHOT": return unchecked((int)0x5D60E4E0);
            default: return unchecked((int)0xC6EE6B4C);
        }
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    // ---- UI helpers ----

    private void DrawReticle2D(Vector3 worldPos)
    {
        // Project world -> screen; returns normalized coords (0..1)
        float sx = 0f, sy = 0f;
        bool onScreen = Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, worldPos.X, worldPos.Y, worldPos.Z, new OutputArgument(), new OutputArgument());
        // The above signature with new OutputArgument() doesn't directly capture values in SHVDN 3,
        // so use a tiny helper to fetch by ref:
        var argsX = new OutputArgument();
        var argsY = new OutputArgument();
        onScreen = Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, worldPos.X, worldPos.Y, worldPos.Z, argsX, argsY);
        if (!onScreen) return;

        sx = argsX.GetResult<float>();
        sy = argsY.GetResult<float>();

        // DRAW_RECT(x, y, width, height, r,g,b,a) in normalized 0..1 coords
        Function.Call(Hash.DRAW_RECT, sx, sy, _reticleSize, _reticleSize, 255, 255, 255, (int)_reticleAlpha);
    }

    private void DrawHitMarker3D(Vector3 pos)
    {
        // DRAW_MARKER type 28 = debug sphere
        Function.Call(Hash.DRAW_MARKER,
            28, // MarkerType.DebugSphere
            pos.X, pos.Y, pos.Z,
            0f, 0f, 0f,
            0f, 0f, 0f,
            _hitMarkerSize, _hitMarkerSize, _hitMarkerSize,
            255, 255, 255, 180,
            false, true, 2, false, null, null, false
        );
    }

    private void Cleanup()
    {
        if (_gunner != null)
        {
            try
            {
                if (_gunner.Exists())
                {
                    Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, _gunner.Handle);
                    Function.Call(Hash.DETACH_ENTITY, _gunner.Handle, true, true);
                    _gunner.Delete();
                }
            }
            catch { }
            finally { _gunner = null; }
        }
    }
}
