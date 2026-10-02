using UnityEngine;

namespace DragonBattle
{
    public enum AIState { Idle, Chase, Attack }

    /// <summary>
    /// Simple state machine for the enemy dragon.
    ///   Idle   - short pause (at the start and after every attack).
    ///   Chase  - run towards the player until in attack distance.
    ///   Attack - pick an ability by distance; if nothing is ready, circle the player.
    /// The AI goes through Dragon.TryUse, so it obeys the same cooldowns as the player.
    /// </summary>
    [RequireComponent(typeof(Dragon))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float reactionTime = 1f;
        [SerializeField] private float attackDistance = 7f;
        [SerializeField] private float thinkInterval = 0.25f;
        [SerializeField] private Vector2 pauseAfterAttack = new Vector2(0.6f, 1.3f);
        [SerializeField, Range(0f, 1f)] private float flyChance = 0.6f;

        public AIState state = AIState.Idle;

        private Dragon dragon;
        private float stateTimer;
        private float nextThink;
        private float orbitSign = 1f;
        private float nextOrbitFlip;

        private void Awake() => dragon = GetComponent<Dragon>();

        private void Start() => SetState(AIState.Idle, reactionTime);

        private void SetState(AIState next, float timer = 0f)
        {
            state = next;
            stateTimer = timer;
        }

        private void Update()
        {
            var target = dragon.opponent;
            if (!dragon.CanAct || dragon.IsDead || target == null || target.IsDead || dragon.IsBusy)
            {
                dragon.Move(Vector3.zero);
                return;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector3 dir = toTarget.normalized;

            switch (state)
            {
                case AIState.Idle:
                    dragon.Move(Vector3.zero);
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) SetState(AIState.Chase);
                    break;

                case AIState.Chase:
                    dragon.Move(dir);
                    if (distance <= attackDistance) SetState(AIState.Attack);
                    break;

                case AIState.Attack:
                    UpdateAttack(dir, distance);
                    break;
            }
        }

        private void UpdateAttack(Vector3 dir, float distance)
        {
            if (distance > attackDistance + 2.5f)
            {
                SetState(AIState.Chase);
                return;
            }

            // Circle the player while waiting for abilities to come off cooldown.
            if (Time.time >= nextOrbitFlip)
            {
                orbitSign = Random.value < 0.5f ? -1f : 1f;
                nextOrbitFlip = Time.time + Random.Range(1.5f, 3f);
            }
            Vector3 side = Vector3.Cross(Vector3.up, dir) * orbitSign;
            Vector3 radial = distance < 3.5f ? -dir : Vector3.zero;
            dragon.Move(side * 0.8f + radial);

            if (Time.time < nextThink) return;
            nextThink = Time.time + thinkInterval;

            Ability choice = ChooseAbility(distance);
            if (choice != null && dragon.TryUse(choice))
                SetState(AIState.Idle, Random.Range(pauseAfterAttack.x, pauseAfterAttack.y));
        }

        /// <summary>Distance based ability choice: tail when close, fire at range, fly to close the gap.</summary>
        private Ability ChooseAbility(float centreDistance)
        {
            float edge = Mathf.Max(0f, centreDistance - dragon.radius - dragon.opponent.radius);

            if (edge <= dragon.tail.range - 0.2f && dragon.CanUse(dragon.tail)) return dragon.tail;

            bool flyReady = dragon.CanUse(dragon.fly);
            if (flyReady && (edge > 5f || edge < 2.5f) && Random.value < flyChance) return dragon.fly;

            if (edge <= dragon.fire.range - 1f && dragon.CanUse(dragon.fire)) return dragon.fire;
            return null;
        }
    }
}
