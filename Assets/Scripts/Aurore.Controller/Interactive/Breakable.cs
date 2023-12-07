using System;
using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    [Serializable]
    public struct Breakable : IComponentData
    {
        public float Threshold;
        public float DestructTime;
        [HideInInspector] public bool Triggered;
        [HideInInspector] public float TimeSinceTrigger;

        public static Breakable GetDefault()
        {
            return new Breakable
            {
                Threshold = 0,
                DestructTime = 0,
                Triggered = false,
                TimeSinceTrigger = 0
            };
        }
    }
}