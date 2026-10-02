using System;
using UnityEngine;

namespace DragonBattle
{
    /// <summary>Data + cooldown state for one dragon ability.</summary>
    [Serializable]
    public class Ability
    {
        public string name = "Ability";
        public KeyCode key = KeyCode.Alpha1;
        [Tooltip("Total damage dealt by one use.")] public float damage = 20f;
        [Tooltip("Seconds before the ability can be used again.")] public float cooldown = 3f;
        [Tooltip("Reach measured edge-to-edge (metres).")] public float range = 3f;
        public Color color = Color.white;

        [NonSerialized] public float readyAt;

        public bool Ready => Time.time >= readyAt;
        public float Remaining => Mathf.Max(0f, readyAt - Time.time);
    }
}
