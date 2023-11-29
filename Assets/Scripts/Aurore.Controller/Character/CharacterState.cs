using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;

namespace Character
{
    public enum CharacterState
    {
        Uninitialized,
    
        GroundMove,
        Crouched,
        AirMove,
        Swimming,
        Climbing,
        GodMode
    }

    public interface ICharacterState
    {
        /// <summary>
        /// Invoked when transitioning into this state from another. This method is responsible for initializing or setting up the state upon entry
        /// </summary>
        /// <param name="previousState"> The state from which the character is transitioning </param>
        /// <param name="context"> A reference to the current update context </param>
        /// <param name="baseContext"> A reference to the kinematic update context </param>
        /// <param name="aspect"></param>
        void OnStateEnter(CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect);

        /// <summary>
        /// Called when the character is transitioning out of the current state. This method handles any cleanup or finalization before leaving the state
        /// </summary>
        /// <param name="nextState"> The state the character is transitioning to </param>
        /// <param name="context"> A reference to the current update context </param>
        /// <param name="baseContext"> A reference to the kinematic update context </param>
        /// <param name="aspect"></param>
        void OnStateExit(CharacterState nextState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect);

        /// <summary>
        /// Executed regularly to perform physics-based updates while the character is in this state.
        /// This typically includes applying forces, handling collisions, and other physics-related logic
        /// </summary>
        /// <param name="context"> A reference to the current update context </param>
        /// <param name="baseContext"> A reference to the kinematic update context </param>
        /// <param name="aspect"></param>
        void OnStatePhysicsUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect);

        /// <summary>
        /// Called to handle non-physics updates such as changing internal state variables, handling animations, or other logic that does not involve physics
        /// </summary>
        /// <param name="context"> A reference to the current update context </param>
        /// <param name="baseContext"> A reference to the kinematic update context </param>
        /// <param name="aspect"></param>
        void OnStateVariableUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect);

        /// <summary>
        /// Provides parameters necessary for camera control based on the current state of the character
        /// </summary>
        /// <param name="character"> The data structure representing the character </param>
        /// <param name="cameraTarget"> Output parameter specifying the target entity for the camera </param>
        /// <param name="calculateUpFromGravity"> Output parameter indicating whether to calculate the camera's up vector based on gravity </param>
        void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity);

        /// <summary>
        /// Converts player inputs into a movement vector. This is essential for translating user commands into character movement
        /// </summary>
        /// <param name="inputs"> The structure containing player input data </param>
        /// <param name="cameraRotation"> The current rotation of the camera, which can influence movement direction </param>
        /// <param name="moveVector"> Output parameter representing the calculated movement vector </param>
        void GetMoveVectorFromPlayerInput(in PlayerInputs inputs, quaternion cameraRotation, out float3 moveVector);
    }
}