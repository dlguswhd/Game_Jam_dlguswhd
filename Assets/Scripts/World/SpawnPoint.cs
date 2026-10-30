using UnityEngine;

namespace GameJam.World
{
    // Where the player appears when arriving in this scene. SceneFlow picks the one whose id matches
    // GameProgress.PendingSpawn, or the default one when the scene is started directly.
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id = "Start";
        [SerializeField] private bool isDefault;

        public string Id => id;
        public bool IsDefault => isDefault;

        private void OnDrawGizmos()
        {
            Gizmos.color = isDefault ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
        }
    }
}
