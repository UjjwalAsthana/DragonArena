using UnityEngine;

namespace DragonBattle
{
    /// <summary>Floating damage number: pops in, rises, fades out and always faces the camera.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DamagePopup : MonoBehaviour
    {
        public TextMesh text;
        public float lifetime = 0.9f;
        public float riseSpeed = 2.4f;

        private float age;
        private Color baseColor;
        private float baseScale = 1f;
        private Transform cam;

        private void Awake()
        {
            // Draw on top of everything, even when inside a dragon.
            var mr = GetComponent<MeshRenderer>();
            mr.material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            mr.sortingOrder = 100;
            baseColor = text.color;
            if (Camera.main != null) cam = Camera.main.transform;
        }

        public void Setup(float amount)
        {
            text.text = Mathf.RoundToInt(amount).ToString();
            if (amount >= 15f)
            {
                baseScale = 1.5f;
                baseColor = new Color(1f, 0.45f, 0.1f, 1f);
                text.color = baseColor;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / lifetime;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += Vector3.up * (riseSpeed * (1f - k) * Time.deltaTime);
            if (cam != null) transform.rotation = cam.rotation;

            float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 1f, (k - 0.15f) * 2f);
            transform.localScale = Vector3.one * (baseScale * Mathf.Max(1f, pop));

            var c = baseColor;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            text.color = c;
        }
    }
}
