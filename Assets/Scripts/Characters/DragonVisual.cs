using System.Collections;
using UnityEngine;

namespace DragonBattle
{
    /// <summary>
    /// Procedural animation for the primitive-built dragon (walk, idle breathing, wing flap,
    /// tail wag, jaw, tail spin, flight height, hit flash, death).
    /// Dragon.cs only sets the public "input" values; this class turns them into motion.
    /// </summary>
    public class DragonVisual : MonoBehaviour
    {
        [Header("Rig (assigned by the scene builder)")]
        public Transform spinRoot;
        public Transform rig;
        public Transform head;
        public Transform jaw;
        public Transform wingL;
        public Transform wingR;
        public Transform[] tail;
        public Transform[] legs; // front-left, front-right, back-left, back-right
        public TrailRenderer tailTrail;
        public Renderer[] renderers;

        [Header("Animated model (Animator-driven dragon)")]
        [Tooltip("When rig is empty, this model is lifted by flightHeight and the Animator does the rest.")]
        public Transform modelRoot;

        [Header("Animation inputs (set by Dragon)")]
        public float speed01;
        public float flightHeight;
        public float flapSpeed = 1.6f;
        public float jawOpen;
        public float windup;
        public float tailWindup;
        public float tilt;

        public float idleFlap = 1.6f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private float time, walkPhase, flapPhase, curFlap, curJaw, curWindup, curTail, curTilt;
        private float punch, deathRoll, deathSink;
        private bool dead;
        private MaterialPropertyBlock flashBlock;
        private Coroutine flashRoutine;

        private void Awake()
        {
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor(BaseColor, new Color(2.4f, 2.4f, 2.4f, 1f));
            curFlap = flapSpeed = idleFlap;
        }

        private void LateUpdate()
        {
            if (rig == null)
            {
                if (modelRoot != null) modelRoot.localPosition = new Vector3(0f, flightHeight, 0f);
                return;
            }

            float dt = Time.deltaTime;
            time += dt;

            float air = Mathf.Clamp01(flightHeight / 2.5f);
            walkPhase += dt * 10f * speed01;
            curFlap = Mathf.Lerp(curFlap, flapSpeed, dt * 6f);
            flapPhase += dt * curFlap;
            curJaw = Mathf.Lerp(curJaw, jawOpen, dt * 14f);
            curWindup = Mathf.Lerp(curWindup, windup, dt * 10f);
            curTail = Mathf.Lerp(curTail, tailWindup, dt * 12f);
            curTilt = Mathf.Lerp(curTilt, tilt, dt * 8f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 1.1f);

            // Body bob, breathing, flight height, tilt, death roll.
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.15f * speed01 + Mathf.Sin(time * 2f) * 0.03f;
            rig.localPosition = new Vector3(0f, flightHeight + bob + deathSink, 0f);
            rig.localRotation = Quaternion.Euler(curTilt, 0f, deathRoll);
            rig.localScale = Vector3.one * (1f + punch + Mathf.Sin(time * 2f) * 0.012f);

            // Legs: diagonal pairs swing together, tuck up in the air.
            for (int i = 0; i < legs.Length; i++)
            {
                float phase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float swing = Mathf.Sin(walkPhase + phase) * 35f * speed01;
                legs[i].localRotation = Quaternion.Euler(swing + air * 45f, 0f, 0f);
            }

            // Wings: folded when idle, big flaps when flying, spread during the fire windup.
            float flapBlend = Mathf.InverseLerp(idleFlap, 18f, curFlap);
            float flap = Mathf.Sin(flapPhase);
            float folded = -8f + flap * 3f + curWindup * 45f;
            float flying = 12f + flap * 48f;
            float wing = dead ? -50f : Mathf.Lerp(folded, flying, flapBlend);
            // Folded wings sweep back along the body; they swing out when flapping or during the fire windup.
            float sweep = dead ? 75f : 70f * (1f - flapBlend) * (1f - curWindup);
            wingR.localRotation = Quaternion.Euler(0f, sweep, wing);
            wingL.localRotation = Quaternion.Euler(0f, -sweep, -wing);

            // Tail: lazy wave, wider when moving, curls back for the tail attack windup.
            for (int i = 0; i < tail.Length; i++)
            {
                float wave = Mathf.Sin(time * 2.5f - i * 0.7f) * (5f + 10f * speed01);
                tail[i].localRotation = Quaternion.Euler(-2f, wave + curTail * 20f, 0f);
            }

            // Head and jaw.
            head.localRotation = Quaternion.Euler(-curWindup * 28f + Mathf.Sin(time * 1.5f) * 2f, 0f, 0f);
            jaw.localRotation = Quaternion.Euler(curJaw * 38f, 0f, 0f);
        }

        // ---------------------------------------------------------------- one-shot animations

        public IEnumerator HeightTo(float target, float duration)
        {
            float start = flightHeight;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                flightHeight = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            flightHeight = target;
        }

        public void SpinTail(float duration)
        {
            if (spinRoot != null) StartCoroutine(SpinRoutine(duration));
        }

        private IEnumerator SpinRoutine(float duration)
        {
            if (tailTrail != null) tailTrail.emitting = true;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                spinRoot.localRotation = Quaternion.Euler(0f, 360f * Mathf.SmoothStep(0f, 1f, t / duration), 0f);
                yield return null;
            }
            spinRoot.localRotation = Quaternion.identity;
            if (tailTrail != null) tailTrail.emitting = false;
        }

        public void Flash()
        {
            punch = 0.1f;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            foreach (var r in renderers) r.SetPropertyBlock(flashBlock);
            yield return new WaitForSeconds(0.08f);
            foreach (var r in renderers) r.SetPropertyBlock(null);
            flashRoutine = null;
        }

        public void PlayDeath()
        {
            dead = true;
            speed01 = 0f;
            jawOpen = 0.5f;
            flapSpeed = idleFlap;
            tilt = 0f;
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            float startHeight = flightHeight;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                deathRoll = Mathf.Lerp(0f, 85f, k);
                deathSink = Mathf.Lerp(0f, -0.55f, k);
                flightHeight = Mathf.Lerp(startHeight, 0f, k);
                yield return null;
            }
            deathRoll = 85f;
            deathSink = -0.55f;
        }
    }
}
