using System.Collections.Generic;
using GameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.World
{
    // Run state that survives scene loads: items carried, enemies already killed, and which spawn point the
    // next scene should put the player on. A checkpoint is taken on every scene change, so dying reloads the
    // current scene exactly as it was when the player walked in.
    //
    // It also owns scene loading. A scene exit can ask for its target to be preloaded in the background while the
    // player walks up to it (a full load took ~10 s in the editor), so going through the door is near instant.
    public static class GameProgress
    {
        public static readonly HashSet<string> Items = new HashSet<string>();
        private static readonly HashSet<string> DeadEnemies = new HashSet<string>();
        private static readonly Dictionary<string, int> Choices = new Dictionary<string, int>();

        private static readonly HashSet<string> CheckpointItems = new HashSet<string>();
        private static readonly HashSet<string> CheckpointDead = new HashSet<string>();

        private static AsyncOperation _preload;
        private static string _preloadScene;
        // A preload can't be cancelled; if something else must load meanwhile, the preload is let through and this
        // scene is loaded straight after it.
        private static string _redirectScene;

        // Spawn point id for the scene being loaded; null = the scene's default spawn.
        public static string PendingSpawn { get; private set; }

        // Statics would otherwise leak between Play sessions when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ResetAll();
            _preload = null;
            _preloadScene = null;
            _redirectScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void ResetAll()
        {
            Items.Clear();
            DeadEnemies.Clear();
            Choices.Clear();
            PendingSpawn = null;
            Checkpoint();
        }

        // Start loading a scene in the background without switching to it. Low priority, so it doesn't eat into
        // gameplay frames.
        public static void Preload(string scene)
        {
            if (_preload != null) return;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            _preloadScene = scene;
            _preload = SceneManager.LoadSceneAsync(scene);
            _preload.allowSceneActivation = false;
            _preload.completed += _ =>
            {
                _preload = null;
                _preloadScene = null;
                Application.backgroundLoadingPriority = ThreadPriority.Normal;
            };
        }

        public static void TravelTo(string scene, string spawnId)
        {
            PendingSpawn = spawnId;
            Checkpoint();
            Load(scene);
        }

        // After a death: forget what happened since entering this scene and load it again.
        public static void RestartFromCheckpoint()
        {
            Items.Clear();
            Items.UnionWith(CheckpointItems);
            DeadEnemies.Clear();
            DeadEnemies.UnionWith(CheckpointDead);
            Load(SceneManager.GetActiveScene().name);
        }

        public const string TitleScene = "Title";
        public const string FirstLevelScene = "Bridge_Village";

        // From the briefing: a fresh run at the start of the first level.
        public static void StartRun()
        {
            ResetAll();
            Load(FirstLevelScene);
        }

        // After the mission is complete (or to quit a run): wipe it and go back to the title screen.
        public static void ReturnToTitle()
        {
            ResetAll();
            Load(TitleScene);
        }

        // A random pick made once per run (e.g. where the keycard lies), so dying or coming back doesn't move it.
        public static int Choice(string key, int count)
        {
            if (!Choices.TryGetValue(key, out int index) || index >= count)
            {
                index = Random.Range(0, count);
                Choices[key] = index;
            }
            return index;
        }

        public static void MarkEnemyDead(string scene, string enemy) => DeadEnemies.Add(scene + "/" + enemy);
        public static bool IsEnemyDead(string scene, string enemy) => DeadEnemies.Contains(scene + "/" + enemy);

        private static void Load(string scene)
        {
            ScreenFade.Cover();
            if (_preload == null)
            {
                SceneManager.LoadScene(scene);
                return;
            }

            // Finish the pending preload as fast as possible; if it isn't the scene we want, go there right after.
            if (!IsSameScene(_preloadScene, scene)) _redirectScene = scene;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            _preload.allowSceneActivation = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_redirectScene == null) return;
            string redirect = _redirectScene;
            _redirectScene = null;
            SceneManager.LoadScene(redirect);
        }

        // Scenes are referred to by name or by path ("Assets/Scenes/X.unity").
        private static bool IsSameScene(string a, string b) =>
            System.IO.Path.GetFileNameWithoutExtension(a) == System.IO.Path.GetFileNameWithoutExtension(b);

        private static void Checkpoint()
        {
            CheckpointItems.Clear();
            CheckpointItems.UnionWith(Items);
            CheckpointDead.Clear();
            CheckpointDead.UnionWith(DeadEnemies);
        }
    }
}
