using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

public class TrainSystem : ModSubsystemBase
{
    private readonly List<Entity> _spawned = new List<Entity>();
    private Vector3 _startPosition;
    private readonly float _startHeading;
    private readonly float _startSpeed;

    static readonly string[] TrainModels = new[]{
        "freight", "metrotrain", "freightcont1", "freightcar",
        "freightcar2", "freightcont2", "tankercar", "freightgrain"
    };

    public TrainSystem(GeneralConfig cfg)
    {
        _startPosition = cfg.StartPosition;
        _startHeading = cfg.StartHeading;
        _startSpeed = cfg.StartSpeed;
    }

    public override void Start()
    {
        InitPlayer();
        SetupTrain();
    }

    public override void Stop()
    {
        foreach (var e in _spawned)
        {
            if (e != null && e.Exists()) e.Delete();
        }

        _spawned.Clear();

        Function.Call(Hash.SET_RANDOM_TRAINS, true);
    }

    private void InitPlayer()
    {
        var player = Game.Player.Character;

        player.Position = _startPosition;
        player.Heading = _startHeading;
    }

    List<Model> _loadedTrainModels = new List<Model>();

    void LoadTrainModelsOrThrow(int timeoutMs = 8000)
    {
        _loadedTrainModels.Clear();

        foreach (var name in TrainModels)
        {
            var model = new Model(name);
            if (!model.IsInCdImage || !model.IsValid)
                throw new Exception($"Train model not valid/in CD image: {name}");

            model.Request();
            int start = Game.GameTime;
            while (!model.IsLoaded && Game.GameTime - start < timeoutMs)
            {
                Script.Yield(); // let the game load
            }
            if (!model.IsLoaded)
                throw new Exception($"Timed out loading model: {name}");

            _loadedTrainModels.Add(model);
        }
    }

    void ReleaseTrainModels()
    {
        foreach (var m in _loadedTrainModels)
            m.MarkAsNoLongerNeeded();
        _loadedTrainModels.Clear();
    }

    // Fields
    private int _engineHandle;
    public Entity Engine { get; private set; }
    private readonly List<Entity> _cars = new List<Entity>(); // carriages as Entities

    private void SetupTrain()
    {
        // required before SET_RANDOM_TRAINS
        Function.Call(Hash.SWITCH_TRAIN_TRACK, 0, true);
        Function.Call(Hash.SET_TRAIN_TRACK_SPAWN_FREQUENCY, 0, 120000);

        Function.Call(Hash.SET_RANDOM_TRAINS, false);

        LoadTrainModelsOrThrow();
        _engineHandle = Function.Call<int>(Hash.CREATE_MISSION_TRAIN, 0, _startPosition.X, _startPosition.Y, _startPosition.Z, true, true, true);
        ReleaseTrainModels();

        if (_engineHandle == 0) { Notification.PostTicker("~r~Failed to create train", true); return; }

        Engine = Entity.FromHandle(_engineHandle);
        if (Engine == null || !Engine.Exists())
        {
            Notification.PostTicker("~r~Engine invalid", true);
            return;
        }

        _spawned.Add(Engine);
        _cars.Clear();

        HardenTrainEntity(Engine);

        // Collect carriages (indices 1..N)
        for (int i = 1; i < 30; i++)
        {
            int carHandle = Function.Call<int>(Hash.GET_TRAIN_CARRIAGE, _engineHandle, i);
            if (carHandle == 0) break;

            var car = Entity.FromHandle(carHandle);
            if (car != null && car.Exists())
            {
                HardenTrainEntity(car);
                _cars.Add(car);
                _spawned.Add(car);
            }
        }

        // Seat player in engine cab if possible
        try { Function.Call(Hash.SET_PED_INTO_VEHICLE, Game.Player.Character.Handle, _engineHandle, -1); } catch { }

        // Initial speed (mph?)
        Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, Engine.Handle, _startSpeed);

        Notification.PostTicker($"~g~Train ready: ${ Engine.Handle }", true);
    }

    private void HardenTrainEntity(Entity e)
    {
        if (e == null || !e.Exists()) return;

        e.IsPersistent = true;

        // Ownership/mission
        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, e.Handle, true, true);
        Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, e.Handle, true);

        // Core “god mode” flags
        e.IsInvincible = true;
        Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, e.Handle, false);

        // Bullet/fire/explosion/physics proofing (covers many ChaosMod effects)
        // SET_ENTITY_PROOFS(entity, bullet, fire, explosion, collision, melee, steam, p7, drown)
        Function.Call(Hash.SET_ENTITY_PROOFS, e.Handle, true, true, true, true, true, true, true, true);

        // Vehicle-specific hardening
        if (e is Vehicle v && v.Exists())
        {
            // Make it “tough”
            Function.Call(Hash.SET_VEHICLE_STRONG, v.Handle, true);
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, v.Handle, false);
            Function.Call(Hash.SET_DISABLE_VEHICLE_PETROL_TANK_DAMAGE, v.Handle, true);
            Function.Call(Hash.SET_DISABLE_VEHICLE_PETROL_TANK_FIRES, v.Handle, true);
            Function.Call(Hash.SET_VEHICLE_ENGINE_CAN_DEGRADE, v.Handle, false);
            Function.Call(Hash.SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE, v.Handle, false);

            // Doors locked just to keep chaos scripts from yeeting you out
            try { Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, v.Handle, 4); } catch { }

            // Big health pool and top it up
            Function.Call(Hash.SET_ENTITY_MAX_HEALTH, v.Handle, 10000);
            Function.Call(Hash.SET_ENTITY_HEALTH, v.Handle, 10000);
            Function.Call(Hash.SET_VEHICLE_ENGINE_HEALTH, v.Handle, 10000.0f);
            Function.Call(Hash.SET_VEHICLE_PETROL_TANK_HEALTH, v.Handle, 10000.0f);
            Function.Call(Hash.SET_VEHICLE_BODY_HEALTH, v.Handle, 1000.0f);
        }

        // LOD so it doesn’t pop under heavy load
        Function.Call(Hash.SET_ENTITY_LOD_DIST, e.Handle, 0xFFFF);
    }
}
