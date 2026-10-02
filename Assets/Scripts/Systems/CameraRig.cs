using UnityEngine;

namespace DragonBattle
{
    /// <summary>
    /// 2.5D top-down camera (angled overhead like TFT / Dota Underlords).
    /// Follows the midpoint of both dragons and pulls back when they are far apart.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform targetA;
        public Transform targetB;

        [Header("Angle")]
        public float pitch = 58f;
        public float yaw = 0f;

        [Header("Framing")]
        public float minDistance = 15f;
        public float maxDistance = 21f;
        public float separationForMax = 20f;
        public float smoothTime = 0.25f;

        private Vector3 velocity;
        private float shake;

        public void Shake(float magnitude) => shake = Mathf.Max(shake, magnitude);

        private void LateUpdate()
        {
            if (targetA == null || targetB == null) return;

            Vector3 mid = (targetA.position + targetB.position) * 0.5f;
            float separation = Vector3.Distance(targetA.position, targetB.position);
            float distance = Mathf.Lerp(minDistance, maxDistance, Mathf.InverseLerp(0f, separationForMax, separation));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = mid - rotation * Vector3.forward * distance;

            Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = rotation;

            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.6f);
            transform.position = position + Random.insideUnitSphere * shake;
        }
    }
}
