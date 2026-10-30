using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Converts the KINEMATION generic (SKM_Operator) clips into humanoid muscle clips,
    // so any humanoid character (e.g. the gang members) can play them.
    public static class HumanoidClipBaker
    {
        private const string HumanoidRigPath =
            "Assets/KINEMATION/TacticalShooterPack/Meshes/Character/Operator/SKM_Operator_Humanoid.fbx";
        private const string AnimRoot = "Assets/KINEMATION/TacticalShooterPack/Animations/";
        public const string OutputFolder = "Assets/Animations/Enemy";
        private const float SampleRate = 30f;

        private struct Job
        {
            public string source;
            public string output;
            public bool loop;
            public bool inPlace;
        }

        private static readonly Job[] Jobs =
        {
            // SKM_Operator@Walking after StraightWalkBaker (straight feet, stance foot planted).
            new Job { source = "Assets/Animations/Legs_Walking_Straight.anim", output = "Enemy_Walk", loop = true, inPlace = true },

            new Job { source = AnimRoot + "AK105/Character/A_FP_AK105_Idle.FBX", output = "Rifle_Idle", loop = true },
            new Job { source = AnimRoot + "AK105/Character/A_FP_AK105_Reload_Tac.FBX", output = "Rifle_Reload" },

            new Job { source = AnimRoot + "Herrington_11-87/Character/A_FP_Herrington_11-87_Idle.FBX", output = "Shotgun_Idle", loop = true },
            new Job { source = AnimRoot + "Herrington_11-87/Character/A_FP_Herrington_11-87_Fire.FBX", output = "Shotgun_Fire" },
            new Job { source = AnimRoot + "Herrington_11-87/Character/A_FP_Herrington_11-87_Reload_Tac_Start.FBX", output = "Shotgun_Reload_Start" },
            new Job { source = AnimRoot + "Herrington_11-87/Character/A_FP_Herrington_11-87_Reload_Loop.FBX", output = "Shotgun_Reload_Loop", loop = true },
            new Job { source = AnimRoot + "Herrington_11-87/Character/A_FP_Herrington_11-87_Reload_End.FBX", output = "Shotgun_Reload_End" },

            new Job { source = AnimRoot + "WK-11_Viper/Character/A_FP_WK-11_Viper_Idle_Pose.FBX", output = "Pistol_Idle", loop = true },
            new Job { source = AnimRoot + "WK-11_Viper/Character/A_FP_WK-11_Viper_Fire.FBX", output = "Pistol_Fire" },
            new Job { source = AnimRoot + "WK-11_Viper/Character/A_FP_WK-11_Viper_Reload_Tactical.FBX", output = "Pistol_Reload" },
        };

        [MenuItem("Tools/Game Jam/Bake Enemy Humanoid Clips")]
        public static void BakeAll()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidRigPath);
            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(HumanoidRigPath))
                if (asset is Avatar a) avatar = a;

            if (rigPrefab == null || avatar == null || !avatar.isHuman)
            {
                Debug.LogError($"Humanoid rig/avatar missing at {HumanoidRigPath}");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Animations", "Enemy");

            foreach (var job in Jobs)
            {
                var source = LoadClip(job.source);
                if (source == null)
                {
                    Debug.LogWarning($"Missing clip: {job.source}");
                    continue;
                }

                Bake(source, rigPrefab, avatar, $"{OutputFolder}/{job.output}.anim", job.loop, job.inPlace);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {Jobs.Length} humanoid enemy clips into {OutputFolder}");
        }

        public static AnimationClip Bake(AnimationClip source, GameObject rigPrefab, Avatar avatar, string outputPath,
            bool loop, bool inPlace)
        {
            var rig = Object.Instantiate(rigPrefab);
            rig.hideFlags = HideFlags.HideAndDontSave;

            // With a humanoid Animator on the rig, SampleAnimation ignores the generic transform curves.
            var rigAnimator = rig.GetComponent<Animator>();
            if (rigAnimator != null) Object.DestroyImmediate(rigAnimator);

            try
            {
                var handler = new HumanPoseHandler(avatar, rig.transform);
                var pose = new HumanPose();

                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * SampleRate) + 1);
                int muscleCount = HumanTrait.MuscleCount;
                var muscleKeys = new Keyframe[muscleCount][];
                for (int m = 0; m < muscleCount; m++) muscleKeys[m] = new Keyframe[frames];
                var rootT = new[] { new Keyframe[frames], new Keyframe[frames], new Keyframe[frames] };
                var rootQ = new[] { new Keyframe[frames], new Keyframe[frames], new Keyframe[frames], new Keyframe[frames] };

                Vector3 firstBody = Vector3.zero;
                Quaternion prevQ = Quaternion.identity;

                for (int i = 0; i < frames; i++)
                {
                    float time = frames == 1 ? 0f : source.length * i / (frames - 1);
                    source.SampleAnimation(rig, time);
                    handler.GetHumanPose(ref pose);

                    if (i == 0) firstBody = pose.bodyPosition;
                    Vector3 body = pose.bodyPosition;
                    if (inPlace)
                    {
                        body.x = firstBody.x;
                        body.z = firstBody.z;
                    }

                    Quaternion q = pose.bodyRotation;
                    if (i > 0 && Quaternion.Dot(prevQ, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    prevQ = q;

                    for (int m = 0; m < muscleCount; m++) muscleKeys[m][i] = new Keyframe(time, pose.muscles[m]);
                    rootT[0][i] = new Keyframe(time, body.x);
                    rootT[1][i] = new Keyframe(time, body.y);
                    rootT[2][i] = new Keyframe(time, body.z);
                    rootQ[0][i] = new Keyframe(time, q.x);
                    rootQ[1][i] = new Keyframe(time, q.y);
                    rootQ[2][i] = new Keyframe(time, q.z);
                    rootQ[3][i] = new Keyframe(time, q.w);
                }

                var clip = new AnimationClip { frameRate = SampleRate };
                for (int m = 0; m < muscleCount; m++)
                    SetCurve(clip, ToClipPropertyName(HumanTrait.MuscleName[m]), muscleKeys[m]);

                string[] t = { "x", "y", "z" };
                string[] r = { "x", "y", "z", "w" };
                for (int c = 0; c < 3; c++) SetCurve(clip, "RootT." + t[c], rootT[c]);
                for (int c = 0; c < 4; c++) SetCurve(clip, "RootQ." + r[c], rootQ[c]);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    return existing;
                }

                AssetDatabase.CreateAsset(clip, outputPath);
                return clip;
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static void SetCurve(AnimationClip clip, string property, Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }

        // HumanTrait names fingers as "Left Thumb 1 Stretched"; clips expect "LeftHand.Thumb.1 Stretched".
        private static readonly Dictionary<string, string> FingerPrefixes = new Dictionary<string, string>
        {
            { "Left Thumb ", "LeftHand.Thumb." }, { "Left Index ", "LeftHand.Index." },
            { "Left Middle ", "LeftHand.Middle." }, { "Left Ring ", "LeftHand.Ring." },
            { "Left Little ", "LeftHand.Little." },
            { "Right Thumb ", "RightHand.Thumb." }, { "Right Index ", "RightHand.Index." },
            { "Right Middle ", "RightHand.Middle." }, { "Right Ring ", "RightHand.Ring." },
            { "Right Little ", "RightHand.Little." },
        };

        private static string ToClipPropertyName(string muscle)
        {
            foreach (var pair in FingerPrefixes)
            {
                if (!muscle.StartsWith(pair.Key)) continue;
                string rest = muscle.Substring(pair.Key.Length); // "1 Stretched" or "Spread"
                int space = rest.IndexOf(' ');
                return space > 0
                    ? pair.Value + rest.Substring(0, space) + rest.Substring(space)
                    : pair.Value + rest;
            }
            return muscle;
        }

        private static AnimationClip LoadClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            return null;
        }
    }
}
