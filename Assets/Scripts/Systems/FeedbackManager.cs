using UnityEngine;

namespace DragonBattle
{
    /// <summary>Spawns hit feedback (sparks, damage numbers, shockwave) and triggers camera shake.</summary>
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

        public GameObject hitSparkPrefab;
        public GameObject shockwavePrefab;
        public GameObject damagePopupPrefab;
        public CameraRig cameraRig;

        private void Awake() => Instance = this;

        public void ShowHit(Vector3 point, float amount)
        {
            if (hitSparkPrefab != null) Instantiate(hitSparkPrefab, point, Quaternion.identity);

            if (damagePopupPrefab != null)
            {
                Vector3 pos = point + Vector3.up * 0.8f + Random.insideUnitSphere * 0.35f;
                var popup = Instantiate(damagePopupPrefab, pos, Quaternion.identity).GetComponent<DamagePopup>();
                popup.Setup(amount);
            }

            Shake(Mathf.Clamp(amount * 0.012f, 0.05f, 0.35f));
        }

        public void SpawnShockwave(Vector3 position, float scale)
        {
            if (shockwavePrefab == null) return;
            var go = Instantiate(shockwavePrefab, new Vector3(position.x, 0.15f, position.z), Quaternion.identity);
            go.transform.localScale = Vector3.one * scale;
        }

        public void Shake(float magnitude)
        {
            if (cameraRig != null) cameraRig.Shake(magnitude);
        }
    }
}
