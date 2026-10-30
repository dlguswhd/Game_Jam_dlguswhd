using UnityEngine;

namespace GameJam.World
{
    // Player options from the settings screen, saved in PlayerPrefs.
    public static class GameSettings
    {
        private const string SensitivityKey = "settings.sensitivity";
        private const string VolumeKey = "settings.volume";

        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        private static bool _loaded;
        private static float _sensitivity = 1f;
        private static float _volume = 0.8f;

        // Multiplies all mouse look (aiming multipliers still apply on top).
        public static float Sensitivity
        {
            get { Load(); return _sensitivity; }
            set
            {
                Load();
                _sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
            }
        }

        // Master volume (AudioListener.volume).
        public static float Volume
        {
            get { Load(); return _volume; }
            set
            {
                Load();
                _volume = Mathf.Clamp01(value);
                AudioListener.volume = _volume;
                PlayerPrefs.SetFloat(VolumeKey, _volume);
            }
        }

        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            _loaded = false;
            Load();
            AudioListener.volume = _volume;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            _volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        }
    }
}
