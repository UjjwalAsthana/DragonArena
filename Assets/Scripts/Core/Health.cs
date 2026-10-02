using System;
using UnityEngine;

namespace DragonBattle
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 200f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>When true all damage is ignored (used while the dragon is airborne).</summary>
        public bool Invulnerable { get; set; }

        public event Action<float, Vector3> Damaged;
        public event Action Died;

        private void Awake() => Current = maxHealth;

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || Invulnerable || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, hitPoint);
            if (IsDead) Died?.Invoke();
        }
    }
}
