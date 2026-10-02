using UnityEngine;

namespace DragonBattle
{
    public enum Sfx { Roar, Fire, TailWhoosh, TailHit, Flap, Slam, Hit, Win, Beep, Go }

    /// <summary>Plays sound effects from a small AudioSource pool, with random pitch so repeats vary.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Clips")]
        public AudioClip roar;
        public AudioClip fireBreath;
        public AudioClip tailWhoosh;
        public AudioClip tailHit;
        public AudioClip flap;
        public AudioClip slam;
        public AudioClip hit;
        public AudioClip win;
        public AudioClip beep;
        public AudioClip go;
        public AudioClip music;

        [Range(0f, 1f)] public float musicVolume = 0.25f;

        private AudioSource[] pool;
        private int next;

        private void Awake()
        {
            Instance = this;

            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }

            if (music != null)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = music;
                src.loop = true;
                src.volume = musicVolume;
                src.spatialBlend = 0f;
                src.Play();
            }
        }

        public static void Play(Sfx sfx, float volume = 1f, float pitchVariance = 0.08f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume, pitchVariance);
        }

        private void PlayInternal(Sfx sfx, float volume, float pitchVariance)
        {
            AudioClip clip = sfx switch
            {
                Sfx.Roar => roar,
                Sfx.Fire => fireBreath,
                Sfx.TailWhoosh => tailWhoosh,
                Sfx.TailHit => tailHit,
                Sfx.Flap => flap,
                Sfx.Slam => slam,
                Sfx.Hit => hit,
                Sfx.Win => win,
                Sfx.Beep => beep,
                _ => go,
            };
            if (clip == null) return;

            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.PlayOneShot(clip, volume);
        }
    }
}
