using UnityEngine;

namespace DragonBattle
{
    /// <summary>Torch-like flicker for point lights.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float amount = 0.35f;
        public float speed = 6f;

        private Light lightSource;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            lightSource = GetComponent<Light>();
            baseIntensity = lightSource.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            lightSource.intensity = baseIntensity * (1f - amount + n * amount * 2f);
        }
    }
}
