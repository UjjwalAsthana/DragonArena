using UnityEngine;

namespace DragonBattle
{
    /// <summary>
    /// Player input: WASD to move, 1 / 2 / 3 for the abilities (keys come from the Ability data).
    /// A pressed ability is buffered for a short time so it fires the moment the dragon is free.
    /// </summary>
    [RequireComponent(typeof(Dragon))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float bufferTime = 0.3f;

        private Dragon dragon;
        private Ability buffered;
        private float bufferedAt;

        private void Awake() => dragon = GetComponent<Dragon>();

        /// <summary>Used by the on-screen ability icons (same behaviour as pressing the key).</summary>
        public void UseAbility(int index)
        {
            if (dragon.IsDead || index < 0 || index >= dragon.Abilities.Length) return;
            Trigger(dragon.Abilities[index]);
        }

        private void Trigger(Ability ability)
        {
            if (!dragon.TryUse(ability) && ability.Ready)
            {
                buffered = ability;
                bufferedAt = Time.time;
            }
        }

        private void Update()
        {
            if (dragon.IsDead) return;

            var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            dragon.Move(move);

            foreach (var ability in dragon.Abilities)
            {
                if (Input.GetKeyDown(ability.key)) Trigger(ability);
            }

            if (buffered != null)
            {
                if (Time.time - bufferedAt > bufferTime) buffered = null;
                else if (dragon.TryUse(buffered)) buffered = null;
            }
        }
    }
}
