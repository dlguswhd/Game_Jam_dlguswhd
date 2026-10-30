using UnityEngine;

namespace GameJam.World
{
    // Moves this object to one of several spots, picked at random once per run (GameProgress.Choice), so the
    // same run always finds it in the same place but a new run may not.
    [DefaultExecutionOrder(-90)]
    public class RandomSpot : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private Vector3[] spots;
        [SerializeField] private float[] yaws;

        private void Awake()
        {
            if (spots == null || spots.Length == 0) return;
            int i = GameProgress.Choice(string.IsNullOrEmpty(key) ? name : key, spots.Length);
            float yaw = yaws != null && i < yaws.Length ? yaws[i] : transform.eulerAngles.y;
            transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
