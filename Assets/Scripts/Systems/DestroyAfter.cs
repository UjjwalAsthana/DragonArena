using UnityEngine;

namespace DragonBattle
{
    /// <summary>Destroys this GameObject after a delay (used by one-shot particle effects).</summary>
    public class DestroyAfter : MonoBehaviour
    {
        public float seconds = 1f;

        private void Start() => Destroy(gameObject, seconds);
    }
}
