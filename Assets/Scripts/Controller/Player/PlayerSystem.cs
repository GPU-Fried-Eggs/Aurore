using Camera;
using Character;
using Controller.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Player
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class PlayerInputsSystem : SystemBase
    {
        private PlayerInputActions.GameplayMapActions m_DefaultActionsMap;

        protected override void OnCreate()
        {
            var inputActions = new PlayerInputActions();
            inputActions.Enable();
            inputActions.GameplayMap.Enable();
            m_DefaultActionsMap = inputActions.GameplayMap;

            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
            RequireForUpdate<NetworkTime>();   
            RequireForUpdate<NetworkId>();   
        }

        protected override void OnUpdate()
        {
            foreach (var (playerInputs, player, ghostOwner, entity) in SystemAPI
                         .Query<RefRW<PlayerInputs>, PlayerData, GhostOwner>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                playerInputs.ValueRW.Move = Vector2.ClampMagnitude(m_DefaultActionsMap.Move.ReadValue<Vector2>(), 1f);
                if (math.lengthsq(m_DefaultActionsMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(m_DefaultActionsMap.LookDelta.ReadValue<Vector2>()))
                {
                    playerInputs.ValueRW.Look = m_DefaultActionsMap.LookConst.ReadValue<Vector2>() * SystemAPI.Time.DeltaTime;
                }
                else
                {
                    playerInputs.ValueRW.Look = m_DefaultActionsMap.LookDelta.ReadValue<Vector2>();
                }
                playerInputs.ValueRW.CameraZoom = m_DefaultActionsMap.CameraZoom.ReadValue<float>();
                playerInputs.ValueRW.SprintHeld = m_DefaultActionsMap.Sprint.IsPressed();
                playerInputs.ValueRW.JumpHeld = m_DefaultActionsMap.Jump.IsPressed();

                if (m_DefaultActionsMap.Jump.WasPressedThisFrame())
                    playerInputs.ValueRW.JumpPressed.Set();
                if (m_DefaultActionsMap.GodMode.WasPressedThisFrame())
                    playerInputs.ValueRW.GodModePressed.Set();
            }
        }
    }

    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(CharacterVariableUpdateSystem))]
    [BurstCompile]
    public partial struct PlayerVariableStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (playerInputs, player) in SystemAPI.Query<PlayerInputs, PlayerData>().WithAll<Simulate>())
            {
                if (SystemAPI.HasComponent<OrbitCameraControl>(player.ControlledCamera))
                {
                    var cameraControl = SystemAPI.GetComponent<OrbitCameraControl>(player.ControlledCamera);
                
                    cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
                    cameraControl.Look = playerInputs.Look;
                    cameraControl.Zoom = playerInputs.CameraZoom;
                
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