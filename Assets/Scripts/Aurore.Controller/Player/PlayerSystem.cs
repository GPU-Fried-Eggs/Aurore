using Camera;
using Character;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        
            RequireForUpdate<FixedTickSystem.Singleton>();
            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
        }

        protected override void OnUpdate()
        {
            var fixedTick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

            foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<PlayerInputs>, PlayerData>())
            {
                playerInputs.ValueRW.Move = Vector2.ClampMagnitude(m_ActionsMap.Move.ReadValue<Vector2>(), 1f);
                if (math.lengthsq(m_ActionsMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(m_ActionsMap.LookDelta.ReadValue<Vector2>()))
                {
                    playerInputs.ValueRW.Look = m_ActionsMap.LookConst.ReadValue<Vector2>() * SystemAPI.Time.DeltaTime;
                }
                else
                {
                    playerInputs.ValueRW.Look = m_ActionsMap.LookDelta.ReadValue<Vector2>();
                }
                playerInputs.ValueRW.CameraZoom = m_ActionsMap.CameraZoom.ReadValue<float>();
                playerInputs.ValueRW.SprintHeld = m_ActionsMap.Sprint.IsPressed();
                playerInputs.ValueRW.CrouchHeld = m_ActionsMap.Crouch.IsPressed();
                playerInputs.ValueRW.JumpHeld = m_ActionsMap.Jump.IsPressed();

                if (m_ActionsMap.Jump.WasPressedThisFrame())
                    playerInputs.ValueRW.JumpPressed.Set(fixedTick);
                if (m_ActionsMap.Crouch.WasPressedThisFrame())
                    playerInputs.ValueRW.CrouchPressed.Set(fixedTick);
                if (m_ActionsMap.Climb.WasPressedThisFrame())
                    playerInputs.ValueRW.ClimbPressed.Set(fixedTick);
                if (m_ActionsMap.GodMode.WasPressedThisFrame())
                    playerInputs.ValueRW.GodModePressed.Set(fixedTick);
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
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
            foreach (var (playerInputs, player) in SystemAPI
                         .Query<PlayerInputs, PlayerData>()
                         .WithAll<Simulate>())
            {
                if (SystemAPI.HasComponent<OrbitCameraControl>(player.ControlledCamera))
                {
                    var cameraControl = SystemAPI.GetComponent<OrbitCameraControl>(player.ControlledCamera);
                
                    cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
                    cameraControl.LookDegreesDelta = playerInputs.Look;
                    cameraControl.ZoomDelta = playerInputs.CameraZoom;
                
                    SystemAPI.SetComponent(player.ControlledCamera, cameraControl);
                }
            }
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct PlayerFixedStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FixedTickSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerData, PlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fixedTick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

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
                    characterControl.CrouchHeld = playerInputs.ValueRW.CrouchHeld;
                    characterControl.SprintHeld = playerInputs.ValueRW.SprintHeld;

                    characterControl.JumpPressed = playerInputs.ValueRW.JumpPressed.IsSet(fixedTick);
                    characterControl.CrouchPressed = playerInputs.ValueRW.CrouchPressed.IsSet(fixedTick);
                    characterControl.ClimbPressed = playerInputs.ValueRW.ClimbPressed.IsSet(fixedTick);
                    characterControl.GodModePressed = playerInputs.ValueRW.GodModePressed.IsSet(fixedTick); 

                    SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
                }
            }
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct FixedTickSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public uint Tick;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Singleton>())
            { 
                var singletonEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(singletonEntity, new Singleton());
            }

            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.Tick++;
        } 
    }
}