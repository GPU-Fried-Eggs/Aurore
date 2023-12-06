using System;
using Unity.Entities;
using UnityEngine;

namespace Character
{
    [Serializable]
    public struct CharacterAnimation : IComponentData
    {
        [HideInInspector] public FastAnimatorParameter ClipIndexParameter;
        [HideInInspector] public FastAnimatorParameter SpeedMultiplierParameter;

        [HideInInspector] public int IdleClip;
        [HideInInspector] public int RunClip;
        [HideInInspector] public int SprintClip;
        [HideInInspector] public int InAirClip;
        [HideInInspector] public int CrouchIdleClip;
        [HideInInspector] public int CrouchMoveClip;
        [HideInInspector] public int ClimbingMoveClip;
        [HideInInspector] public int SwimmingIdleClip;
        [HideInInspector] public int SwimmingMoveClip;

        [HideInInspector] public CharacterState LastAnimationCharacterState;
    }
}