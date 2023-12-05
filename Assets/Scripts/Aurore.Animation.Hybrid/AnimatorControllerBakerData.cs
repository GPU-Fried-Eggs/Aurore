using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;

namespace RTP
{
    public struct State : IEquatable<int>, IDisposable
    {
        public int HashCode;
        public FixedStringName Name;
        public float Speed;
        public FixedStringName SpeedMultiplierParameter;
        public UnsafeList<Transition> Transitions;
        public FixedStringName TimeParameter;
        public float CycleOffset;
        public FixedStringName CycleOffsetParameter;
        public Motion Motion;

        public bool Equals(int o) => o == HashCode;

        public void Dispose()
        {
            foreach (var a in Transitions) a.Dispose();
            Transitions.Dispose();
            Motion.Dispose();
        }
    }

    public struct ChildMotion : IDisposable
    {
        public Motion Motion;
        public float Threshold;
        public float TimeScale;
        public FixedStringName DirectBlendParameterName;
        public float2 Position2D;

        public void Dispose() => Motion.Dispose();
    }

    public struct Motion : IDisposable
    {
        public FixedStringName Name;
        public MotionBlob.Type Type;
        public int AnimationIndex;
        public BlendTree BlendTree;

        public void Dispose() => BlendTree.Dispose();
    }

    public struct BlendTree : IDisposable
    {
        public FixedStringName Name;
        public FixedStringName BlendParameterName;
        public FixedStringName BlendParameterYName;
        public bool NormalizeBlendValues;
        public UnsafeList<ChildMotion> Motions;

        public void Dispose()
        {
            foreach (var a in Motions) a.Dispose();

            Motions.Dispose();
        }
    }

    public struct Transition : IDisposable
    {
        public FixedStringName Name;
        public float Duration;
        public float ExitTime;
        public float Offset;
        public bool HasExitTime;
        public bool HasFixedDuration;
        public bool SoloFlag;
        public bool MuteFlag;
        public bool CanTransitionToSelf;
        public int TargetStateHash;
        public UnsafeList<Condition> Conditions;

        public void Dispose() => Conditions.Dispose();
    }

    public struct Condition
    {
        public FixedStringName Name;
        public FixedStringName ParamName;
        public ParameterValue Threshold;
        public AnimatorConditionMode ConditionMode;
    }

    public struct Layer : IDisposable
    {
        public FixedStringName Name;
        public int DefaultStateIndex;
        public float Weight;
        public AnimationBlendingMode BlendMode;
        public UnsafeList<Transition> AnyStateTransitions;
        public UnsafeList<State> States;
        public AvatarMask AvatarMask;

        public void Dispose()
        {
            foreach (var a in AnyStateTransitions) a.Dispose();
            foreach (var a in States) a.Dispose();

            AnyStateTransitions.Dispose();
            States.Dispose();
            AvatarMask.Dispose();
        }
    }

    public struct Parameter : IEquatable<FixedStringName>
    {
        public FixedStringName Name;
        public ParameterValue DefaultValue;
        public ControllerParameterType Type;

        public bool Equals(FixedStringName o) => o == Name;
    }

    public struct Controller : IDisposable
    {
        public FixedStringName Name;
        public UnsafeList<Layer> Layers;
        public UnsafeList<Parameter> Parameters;
        public UnsafeList<AnimationClip> AnimationClips;

        public void Dispose()
        {
            foreach (var a in Layers) a.Dispose();
            foreach (var a in AnimationClips) a.Dispose();

            Layers.Dispose();
            Parameters.Dispose();
            AnimationClips.Dispose();
        }
    }

    public struct AvatarMask : IDisposable
    {
        public FixedStringName Name;
        public Hash128 Hash;
        public NativeList<FixedStringName> IncludedBonePaths;
        public uint HumanBodyPartsAvatarMask;

        public void Dispose() => IncludedBonePaths.Dispose();
    }
}