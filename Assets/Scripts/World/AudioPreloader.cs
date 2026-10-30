using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameJam.World
{
    // The pack's gunshot/reload clips are "Decompress On Load" without "Preload Audio Data": each clip is read and
    // decompressed the first time it plays, a ~16 ms hitch per clip (measured), i.e. mid-firefight. This loads every
    // clip referenced by the scene's characters and weapons up front instead.
    public static class AudioPreloader
    {
        private static readonly HashSet<object> Visited = new HashSet<object>();

        public static int PreloadScene()
        {
            Visited.Clear();
            int count = 0;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                count += Collect(behaviour, 0);
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                count += Load(source.clip);
            Visited.Clear();
            return count;
        }

        // Walks the serialized fields of a component (and the ScriptableObjects it references, e.g. weapon
        // settings) looking for AudioClips. Depth-limited so it never wanders through the whole object graph.
        private static int Collect(object target, int depth)
        {
            if (target == null || depth > 2 || !Visited.Add(target)) return 0;
            int count = 0;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject); type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    var value = field.GetValue(target);
                    switch (value)
                    {
                        case AudioClip clip:
                            count += Load(clip);
                            break;
                        case IEnumerable<AudioClip> clips:
                            foreach (var c in clips) count += Load(c);
                            break;
                        case ScriptableObject asset:
                            count += Collect(asset, depth + 1);
                            break;
                        case IEnumerable list when !(value is string) && !(value is Transform):
                            foreach (var item in list)
                                if (item is ScriptableObject so) count += Collect(so, depth + 1);
                            break;
                    }
                }
            }
            return count;
        }

        private static int Load(AudioClip clip)
        {
            if (clip == null || clip.loadState == AudioDataLoadState.Loaded) return 0;
            clip.LoadAudioData();
            return 1;
        }
    }
}
