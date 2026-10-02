using UnityEngine;
using UnityEngine.UI;

namespace DragonBattle
{
    /// <summary>Small health bar floating above a dragon; always faces the camera.</summary>
    public class WorldHealthBar : MonoBehaviour
    {
        public Health health;
        public Image fill;
        public Image trail;

        private Transform cam;

        private void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }

            transform.rotation = cam.rotation;
            float p = health.Current / health.Max;
            fill.fillAmount = p;
            trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, p, Time.deltaTime * 0.6f);
        }
    }
}
