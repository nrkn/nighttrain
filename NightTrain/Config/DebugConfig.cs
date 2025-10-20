using GTA;

public class DebugConfig
{
    public DebugConfig(ScriptSettings cfg)
    {
        // Feature gate
        EnableTurretPlatform = cfg.GetValue("Debug", "EnableTurretPlatform", true);

        // Model + seating
        TurretModel = cfg.GetValue("Debug", "TurretModel", "technical");
        TurretSeatIndex = cfg.GetValue("Debug", "TurretSeatIndex", 1);

        // Attach transform (relative to engine)
        AttachOffsetX = cfg.GetValue("Debug", "AttachOffsetX", 0.0f);
        AttachOffsetY = cfg.GetValue("Debug", "AttachOffsetY", 0.0f);
        AttachOffsetZ = cfg.GetValue("Debug", "AttachOffsetZ", 2.0f);

        AttachRotPitch = cfg.GetValue("Debug", "AttachRotPitch", 0.0f);
        AttachRotRoll = cfg.GetValue("Debug", "AttachRotRoll", 0.0f);
        AttachRotYaw = cfg.GetValue("Debug", "AttachRotYaw", 0.0f);

        // Attach flags / behavior
        UseSoftPinning = cfg.GetValue("Debug", "UseSoftPinning", true);
        AttachBoneIndex = cfg.GetValue("Debug", "AttachBoneIndex", 2); // 0/2 are common
        FixedRotation = cfg.GetValue("Debug", "FixedRotation", false);
        SetHeadingOnSpawn = cfg.GetValue("Debug", "SetHeadingOnSpawn", true);

        // Collision controls (explicit mirror of SET_ENTITY_COLLISION args)
        UseSetEntityCollision = cfg.GetValue("Debug", "UseSetEntityCollision", true);
        SetEntityCollision_Toggle = cfg.GetValue("Debug", "SetEntityCollision_Toggle", false);
        SetEntityCollision_KeepPhysics = cfg.GetValue("Debug", "SetEntityCollision_KeepPhysics", false);

        // Optional legacy convenience (kept for compatibility)
        DisableCollisionWhileAttached = cfg.GetValue("Debug", "DisableCollisionWhileAttached", false);
    }

    public bool EnableTurretPlatform { get; private set; }
    public string TurretModel { get; private set; }
    public int TurretSeatIndex { get; private set; }

    public float AttachOffsetX { get; private set; }
    public float AttachOffsetY { get; private set; }
    public float AttachOffsetZ { get; private set; }

    public float AttachRotPitch { get; private set; }
    public float AttachRotRoll { get; private set; }
    public float AttachRotYaw { get; private set; }

    public bool UseSoftPinning { get; private set; }
    public int AttachBoneIndex { get; private set; }
    public bool FixedRotation { get; private set; }
    public bool SetHeadingOnSpawn { get; private set; }

    // Collision
    public bool UseSetEntityCollision { get; private set; }
    public bool SetEntityCollision_Toggle { get; private set; }
    public bool SetEntityCollision_KeepPhysics { get; private set; }

    // Legacy convenience (if you still want the single switch)
    public bool DisableCollisionWhileAttached { get; private set; }
}
