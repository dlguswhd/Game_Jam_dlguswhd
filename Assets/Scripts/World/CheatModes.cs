using UnityEngine;

namespace GameJam.World
{
    // Test modes toggled from the Tab mode menu (ModeMenu). Static so they carry over between scenes;
    // cleared at the start of every Play session.
    public static class CheatModes
    {
        // Firing never uses up the magazine.
        public static bool InfiniteAmmo;
        // The player takes no damage (bullets, claymores).
        public static bool GodMode;
        // Enemies can't see or hear the player, and claymores don't detect them.
        public static bool Ghost;
        // No camera kick, weapon recoil animation or recoil screen shake.
        public static bool NoRecoil;

        // Player footstep / gunshot audio (on by default). Only the sound: enemies still hear the noise.
        public static bool FootstepSound = true;
        public static bool GunSound = true;

        // Attachments on the player's AK105 (re-applied when a scene loads).
        public static bool Suppressor;
        public static bool HoloSight;
        public static bool VerticalGrip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            InfiniteAmmo = GodMode = Ghost = NoRecoil = false;
            Suppressor = HoloSight = VerticalGrip = false;
            FootstepSound = GunSound = true;
        }
    }
}
