using Controller.Character;
using Controller.Manager;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Controller.Player
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class PlayerInputsSystem : SystemBase
    {
        private PlayerInputActions m_InputActions;

        protected override void OnCreate()
        {
            m_InputActions = new PlayerInputActions();
            m_InputActions.Enable();
            m_InputActions.GameplayMap.Enable();

            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData>().Build());
            RequireForUpdate<GameComponent>();
            RequireForUpdate<NetworkTime>();
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            var gameplayMap = m_InputActions.GameplayMap;
            foreach (var (playerCommands, player, ghostOwner, entity) in SystemAPI
                         .Query<RefRW<PlayerCommands>, RefRW<PlayerData>, GhostOwner>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                var isOnNewTick = !player.ValueRW.LastKnownCommandsTick.IsValid || tick.IsNewerThan(player.ValueRW.LastKnownCommandsTick);

                playerCommands.ValueRW = default;
                playerCommands.ValueRW.Move = Vector2.ClampMagnitude(gameplayMap.Move.ReadValue<Vector2>(), 1f);
                if (math.lengthsq(gameplayMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(gameplayMap.LookDelta.ReadValue<Vector2>()))
                {
                    playerCommands.ValueRW.Look = gameplayMap.LookConst.ReadValue<Vector2>() * deltaTime;
                }
                else
                {
                    playerCommands.ValueRW.Look = gameplayMap.LookDelta.ReadValue<Vector2>();
                }
                playerCommands.ValueRW.JumpHeld = gameplayMap.Jump.IsPressed();
                if (gameplayMap.Jump.WasPressedThisFrame()) playerCommands.ValueRW.JumpPressed.Set();

                player.ValueRW.LastKnownCommandsTick = tick;
                player.ValueRW.LastKnownCommands = playerCommands.ValueRW;
            }
        }
    }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PlayerVariableStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerCommands>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            
        }
    }

    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct PlayerFixedStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerCommands>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (playerCommands, player, commandInterpolationDelay, entity) in SystemAPI
                         .Query<PlayerCommands, PlayerData, CommandDataInterpolationDelay>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<CharacterControl>(player.ControlledCharacter))
                {
                }
            }
        }
    }
}