using Camera;
using Character;
using Controller.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Utilities;

namespace Player
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerInputsSystem : SystemBase
    {
        private PlayerInputActions.GameplayMapActions m_ActionsMap;

        protected override void OnCreate()
        {
            var inputActions = new PlayerInputActions();
            inputActions.Enable();
            inputActions.GameplayMap.Enable();
            m_ActionsMap = inputActions.GameplayMap;

            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<PlayerInputs>, PlayerData>())
            {
                playerInputs.ValueRW.Move = Vector2.ClampMagnitude(m_ActionsMap.Move.ReadValue<Vector2>(), 1f);
                if (math.lengthsq(m_ActionsMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(m_ActionsMap.LookDelta.ReadValue<Vector2>()))
                {
                    var inputDelta = m_ActionsMap.LookConst.ReadValue<Vector2>() * SystemAPI.Time.DeltaTime;
                    NetworkInputUtilities.AddInputDelta(ref playerInputs.ValueRW.Look.x, inputDelta.x);
                    NetworkInputUtilities.AddInputDelta(ref playerInputs.ValueRW.Look.y, inputDelta.y);
                }
                else
                {
                    var inputDelta = m_ActionsMap.LookDelta.ReadValue<Vector2>();
                    NetworkInputUtilities.AddInputDelta(ref playerInputs.ValueRW.Look.x, inputDelta.x);
                    NetworkInputUtilities.AddInputDelta(ref playerInputs.ValueRW.Look.y, inputDelta.y);
                }
                playerInputs.ValueRW.CameraZoom = m_ActionsMap.CameraZoom.ReadValue<float>();
                playerInputs.ValueRW.SprintHeld = m_ActionsMap.Sprint.IsPressed();
                playerInputs.ValueRW.JumpHeld = m_ActionsMap.Jump.IsPressed();

                playerInputs.ValueRW.JumpPressed = default;
                if (m_ActionsMap.Jump.WasPressedThisFrame())
                    playerInputs.ValueRW.JumpPressed.Set();
                playerInputs.ValueRW.GodModePressed = default;
                if (m_ActionsMap.GodMode.WasPressedThisFrame())
                    playerInputs.ValueRW.GodModePressed.Set();
            }
        }
    }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(CharacterVariableUpdateSystem))]
    [UpdateAfter(typeof(PredictedFixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PlayerVariableStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NetworkInputUtilities.GetCurrentAndPreviousTick(SystemAPI.GetSingleton<NetworkTime>(), out var currentTick, out var previousTick);

            foreach (var (playerInputsBuffer, player) in SystemAPI
                         .Query<DynamicBuffer<InputBufferData<PlayerInputs>>, PlayerData>()
                         .WithAll<Simulate>())
            {
                NetworkInputUtilities.GetCurrentAndPreviousTickInputs(playerInputsBuffer, currentTick, previousTick,
                    out var currentTickInputs, out var previousTickInputs);

                if (SystemAPI.HasComponent<OrbitCameraControl>(player.ControlledCamera))
                {
                    var cameraControl = SystemAPI.GetComponent<OrbitCameraControl>(player.ControlledCamera);
                
                    cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
                    cameraControl.LookDegreesDelta.x = NetworkInputUtilities.GetInputDelta(currentTickInputs.Look.x, previousTickInputs.Look.x);
                    cameraControl.LookDegreesDelta.y = NetworkInputUtilities.GetInputDelta(currentTickInputs.Look.y, previousTickInputs.Look.y);
                    cameraControl.ZoomDelta = NetworkInputUtilities.GetInputDelta(currentTickInputs.CameraZoom, previousTickInputs.CameraZoom);
                
                    SystemAPI.SetComponent(player.ControlledCamera, cameraControl);
                }
            }
        }
    }

    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct PlayerFixedStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (playerInputs, player, entity) in SystemAPI
                         .Query<RefRW<PlayerInputs>, PlayerData>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<CharacterControl>(player.ControlledCharacter) &&
                    SystemAPI.HasComponent<CharacterStateMachine>(player.ControlledCharacter))
                {
                    var characterControl = SystemAPI.GetComponent<CharacterControl>(player.ControlledCharacter);
                    var stateMachine = SystemAPI.GetComponent<CharacterStateMachine>(player.ControlledCharacter);

                    // Get camera rotation data, since our movement is relative to it
                    var cameraRotation = SystemAPI.HasComponent<LocalTransform>(player.ControlledCamera)
                        ? SystemAPI.GetComponent<LocalTransform>(player.ControlledCamera).Rotation
                        : quaternion.identity;

                    stateMachine.GetMoveVectorFromPlayerInput(stateMachine.CurrentState, in playerInputs.ValueRO,
                        cameraRotation, out characterControl.MoveVector);

                    characterControl.JumpHeld = playerInputs.ValueRW.JumpHeld;
                    characterControl.SprintHeld = playerInputs.ValueRW.SprintHeld;

                    characterControl.JumpPressed = playerInputs.ValueRW.JumpPressed.IsSet;
                    characterControl.GodModePressed = playerInputs.ValueRW.GodModePressed.IsSet;

                    SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
                }
            }
        }
    }
}
