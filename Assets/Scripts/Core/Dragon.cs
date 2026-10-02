using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonBattle
{
    /// <summary>
    /// Shared dragon logic used by both the player and the AI: movement, knockback,
    /// the three abilities and their cooldowns. Input comes from PlayerController or EnemyAI.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(SphereCollider))]
    public class Dragon : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Dragon";
        public Color themeColor = Color.white;

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float turnSpeed = 540f;
        public float acceleration = 45f;
        public float knockbackDecay = 22f;
        public float radius = 1.1f;
        [Tooltip("Half size of the playable area (x,z). Safety clamp on top of the wall colliders.")]
        public Vector2 arenaHalfSize = new Vector2(10.5f, 7f);

        [Header("Abilities")]
        public Ability fire = new Ability { name = "Fire Breath", key = KeyCode.Alpha1, damage = 40f, cooldown = 4f, range = 8f };
        public Ability tail = new Ability { name = "Tail Whip", key = KeyCode.Alpha2, damage = 22f, cooldown = 2.5f, range = 2f };
        public Ability fly = new Ability { name = "Sky Strike", key = KeyCode.Alpha3, damage = 35f, cooldown = 8f, range = 2.6f };
        public float flySpeed = 11f;

        [Header("References")]
        public Dragon opponent;
        public DragonVisual visual;
        public ParticleSystem fireParticles;
        public Light fireLight;
        [Tooltip("Optional. If an Animator is assigned, triggers Fire/Tail/Fly/Hit/Die and float Speed are set when they exist.")]
        public Animator animator;

        public bool CanAct { get; set; }
        public bool IsBusy { get; private set; }
        public bool IsAirborne { get; private set; }
        public bool IsDead => health.IsDead;
        public Health Health => health;
        public Ability[] Abilities { get; private set; }

        private Rigidbody rb;
        private Health health;
        private SphereCollider col;

        private Vector3 moveInput;
        private Vector3 currentMove;
        private Vector3 knock;
        private bool useOverride;
        private Vector3 overrideVelocity;
        private bool aimLock;
        private float aimTurnSpeed;
        private readonly HashSet<string> animParams = new HashSet<string>();

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            col = GetComponent<SphereCollider>();
            Abilities = new[] { fire, tail, fly };

            if (animator != null)
                foreach (var p in animator.parameters) animParams.Add(p.name);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        // ---------------------------------------------------------------- input API

        public void Move(Vector3 direction)
        {
            direction.y = 0f;
            moveInput = Vector3.ClampMagnitude(direction, 1f);
        }

        public bool CanUse(Ability a) => CanAct && !IsBusy && !IsDead && a.Ready
                                         && opponent != null && !opponent.IsDead;

        public bool TryUse(Ability a)
        {
            if (!CanUse(a)) return false;

            a.readyAt = Time.time + a.cooldown;
            if (a == fire) StartCoroutine(FireRoutine());
            else if (a == tail) StartCoroutine(TailRoutine());
            else StartCoroutine(FlyRoutine());
            return true;
        }

        public float EdgeDistanceTo(Dragon other)
        {
            Vector3 d = other.transform.position - transform.position;
            d.y = 0f;
            return Mathf.Max(0f, d.magnitude - radius - other.radius);
        }

        // ---------------------------------------------------------------- physics

        private void FixedUpdate()
        {
            if (IsDead)
            {
                rb.velocity = Vector3.zero;
                return;
            }

            float dt = Time.fixedDeltaTime;

            // Rotation: face the opponent while casting, otherwise face the move direction.
            Vector3 face = Vector3.zero;
            float turn = turnSpeed;
            if (aimLock && opponent != null)
            {
                face = opponent.transform.position - transform.position;
                turn = aimTurnSpeed;
            }
            else if (CanAct && !IsBusy && moveInput.sqrMagnitude > 0.01f)
            {
                face = moveInput;
            }
            face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, turn * dt));
            }

            // Velocity = steering velocity + decaying knockback impulse.
            Vector3 desired = Vector3.zero;
            if (useOverride) desired = overrideVelocity;
            else if (CanAct && !IsBusy) desired = moveInput * moveSpeed;

            currentMove = Vector3.MoveTowards(currentMove, desired, acceleration * dt);
            knock = Vector3.MoveTowards(knock, Vector3.zero, knockbackDecay * dt);
            rb.velocity = currentMove + knock;

            Vector3 p = rb.position;
            p.x = Mathf.Clamp(p.x, -arenaHalfSize.x, arenaHalfSize.x);
            p.z = Mathf.Clamp(p.z, -arenaHalfSize.y, arenaHalfSize.y);
            p.y = 0f;
            rb.position = p;
        }

        private void Update()
        {
            float speed01 = Mathf.Clamp01(currentMove.magnitude / moveSpeed);
            if (visual != null) visual.speed01 = speed01;
            if (animator != null && animParams.Contains("Speed")) animator.SetFloat("Speed", speed01);
        }

        public void ApplyKnockback(Vector3 velocity)
        {
            velocity.y = 0f;
            knock = Vector3.ClampMagnitude(knock + velocity, 20f);
        }

        // ---------------------------------------------------------------- combat helpers

        private void Strike(float damage, float knockback)
        {
            Vector3 dir = opponent.transform.position - transform.position;
            dir.y = 0f;
            dir.Normalize();
            opponent.ReceiveHit(damage, dir * knockback, opponent.transform.position + Vector3.up * 2f);
        }

        public void ReceiveHit(float damage, Vector3 knockVelocity, Vector3 point)
        {
            if (IsDead || health.Invulnerable) return;
            health.TakeDamage(damage, point);
            ApplyKnockback(knockVelocity);
        }

        private bool OpponentInCone(float range, float halfAngle)
        {
            Vector3 to = opponent.transform.position - transform.position;
            to.y = 0f;
            float centre = to.magnitude;
            if (EdgeDistanceTo(opponent) > range) return false;
            // Always hit when almost touching, otherwise require the opponent in front of the mouth.
            return centre < radius + opponent.radius + 1f || Vector3.Angle(transform.forward, to) <= halfAngle;
        }

        private void OnDamaged(float amount, Vector3 point)
        {
            if (visual != null) visual.Flash();
            if (FeedbackManager.Instance != null) FeedbackManager.Instance.ShowHit(point, amount);
            AudioManager.Play(Sfx.Hit);
            Trigger("Hit");
        }

        private void OnDied()
        {
            StopAllCoroutines();
            if (fireParticles != null) fireParticles.Stop();
            if (fireLight != null) fireLight.enabled = false;
            if (opponent != null) Physics.IgnoreCollision(col, opponent.col, false);

            CanAct = false;
            IsBusy = true;
            IsAirborne = false;
            useOverride = false;
            aimLock = false;
            visual.PlayDeath();
            AudioManager.Play(Sfx.Roar, 1f, 0.2f);
            Trigger("Die");
        }

        private void Trigger(string trigger)
        {
            if (animator != null && animParams.Contains(trigger)) animator.SetTrigger(trigger);
        }

        // ---------------------------------------------------------------- abilities

        private IEnumerator FireRoutine()
        {
            IsBusy = true;
            aimLock = true;
            aimTurnSpeed = 360f;
            Trigger("Fire");

            visual.jawOpen = 1f;
            visual.windup = 1f;
            AudioManager.Play(Sfx.Roar);
            yield return new WaitForSeconds(0.35f);

            visual.windup = 0f;
            fireParticles.Play();
            fireLight.enabled = true;
            AudioManager.Play(Sfx.Fire);
            aimTurnSpeed = 90f; // slow tracking so the breath can be dodged

            const int ticks = 5;
            for (int i = 0; i < ticks; i++)
            {
                if (OpponentInCone(fire.range, 28f)) Strike(fire.damage / ticks, 3f);
                yield return new WaitForSeconds(0.2f);
            }

            fireParticles.Stop();
            fireLight.enabled = false;
            visual.jawOpen = 0f;
            aimLock = false;
            yield return new WaitForSeconds(0.35f);
            IsBusy = false;
        }

        private IEnumerator TailRoutine()
        {
            IsBusy = true;
            aimLock = true;
            aimTurnSpeed = 720f;
            Trigger("Tail");

            visual.tailWindup = 1f;
            yield return new WaitForSeconds(0.3f);

            aimLock = false;
            visual.tailWindup = 0f;
            visual.SpinTail(0.45f);
            AudioManager.Play(Sfx.TailWhoosh);
            yield return new WaitForSeconds(0.2f);

            if (EdgeDistanceTo(opponent) <= tail.range)
            {
                Strike(tail.damage, 16f);
                AudioManager.Play(Sfx.TailHit);
                if (FeedbackManager.Instance != null)
                {
                    FeedbackManager.Instance.Shake(0.3f);
                    FeedbackManager.Instance.SpawnShockwave(opponent.transform.position, 0.5f);
                }
            }

            yield return new WaitForSeconds(0.45f);
            IsBusy = false;
        }

        private IEnumerator FlyRoutine()
        {
            IsBusy = true;
            IsAirborne = true;
            health.Invulnerable = true;
            Physics.IgnoreCollision(col, opponent.col, true);
            Trigger("Fly");

            visual.flapSpeed = 22f;
            AudioManager.Play(Sfx.Flap);
            yield return visual.HeightTo(3.2f, 0.55f);

            // Hover and chase the opponent.
            visual.tilt = 20f;
            aimLock = true;
            aimTurnSpeed = 540f;
            useOverride = true;
            for (float t = 0f; t < 0.85f; t += Time.deltaTime)
            {
                Vector3 to = opponent.transform.position - transform.position;
                to.y = 0f;
                overrideVelocity = to.magnitude > 1.2f ? to.normalized * flySpeed : Vector3.zero;
                yield return null;
            }

            // Dive.
            overrideVelocity = Vector3.zero;
            visual.tilt = 55f;
            yield return new WaitForSeconds(0.12f);
            yield return visual.HeightTo(0f, 0.18f);

            // Landing impact.
            IsAirborne = false;
            health.Invulnerable = false;
            Physics.IgnoreCollision(col, opponent.col, false);
            visual.tilt = 0f;
            visual.flapSpeed = visual.idleFlap;

            if (EdgeDistanceTo(opponent) <= fly.range) Strike(fly.damage, 20f);
            AudioManager.Play(Sfx.Slam);
            if (FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.Shake(0.6f);
                FeedbackManager.Instance.SpawnShockwave(transform.position, 1.6f);
            }

            useOverride = false;
            aimLock = false;
            yield return new WaitForSeconds(0.5f);
            IsBusy = false;
        }
    }
}
