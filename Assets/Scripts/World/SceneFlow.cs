using System.Collections;
using GameJam.Combat;
using GameJam.Enemies;
using GameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.World
{
    // One per level scene. On load it puts the player on the right spawn point, removes enemies killed on an
    // earlier visit and preloads the scene's sounds; while playing it records new kills in GameProgress.
    [DefaultExecutionOrder(-100)]
    public class SceneFlow : MonoBehaviour
    {
        private string _scene;

        private void Awake()
        {
            _scene = SceneManager.GetActiveScene().name;
            PlacePlayer();
            PauseMenu.Create();

            foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (GameProgress.IsEnemyDead(_scene, enemy.name))
                {
                    // Deactivate first so it never runs a frame before being destroyed.
                    enemy.gameObject.SetActive(false);
                    Destroy(enemy.gameObject);
                    continue;
                }

                string enemyName = enemy.name;
                enemy.GetComponent<Health>().Died += _ => GameProgress.MarkEnemyDead(_scene, enemyName);
            }
        }

        // One frame in, once TacticalShooterPlayer.Start has spawned the player's weapons, so their clips count too.
        private IEnumerator Start()
        {
            yield return null;
            AudioPreloader.PreloadScene();
            // Sounds are in memory and the first frame is out: lift the black screen from the scene change.
            ScreenFade.Reveal();
        }

        private void PlacePlayer()
        {
            SpawnPoint chosen = null;
            foreach (var spawn in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            {
                if (spawn.Id == GameProgress.PendingSpawn) { chosen = spawn; break; }
                if (spawn.IsDefault && chosen == null) chosen = spawn;
            }
            if (chosen == null) return;

            var player = GameObject.Find("Player");
            if (player == null) return;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(chosen.transform.position, chosen.transform.rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
