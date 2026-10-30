using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Bakes a legs-only copy of a walk clip where each foot travels along a straight line.
    // The source clip keeps its hip twist on the root bone; once the legs are masked onto a still
    // pelvis that twist is lost and the feet swing out in a V. This rolls each thigh in/out per frame
    // so the foot keeps a constant sideways offset from the pelvis.
    public static class StraightWalkBaker
    {
        private const string SourceClipPath = "Assets/SKM_Operator@Walking.fbx";
        private const string OutputClipPath = "Assets/Animations/Legs_Walking_Straight.anim";
        private const float SampleRate = 30f;

        private static readonly string[] Legs = { "l", "r" };

        [MenuItem("Tools/Game Jam/Bake Straight Walk Legs")]
        public static void BakeDefault()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceClipPath);
            var clip = LoadClip(SourceClipPath);
            if (model == null || clip == null)
            {
                Debug.LogError($"Couldn't load walk clip at {SourceClipPath}");
                return;
            }

            Bake(clip, model, OutputClipPath);
        }

        public static AnimationClip Bake(AnimationClip source, GameObject rigPrefab, string outputPath)
        {
            var rig = Object.Instantiate(rigPrefab);
            rig.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                Transform root = rig.transform.Find("root");
                Transform pelvis = root.Find("pelvis");
                Quaternion rootBind = root.localRotation;
                Quaternion pelvisBind = pelvis.localRotation;

                var legBones = new List<Transform>();
                foreach (var side in Legs) legBones.AddRange(pelvis.Find("thigh_" + side).GetComponentsInChildren<Transform>(true));

                int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * SampleRate) + 1);
                var keys = new Dictionary<Transform, Keyframe[][]>();
                foreach (var bone in legBones) keys[bone] = new[] { new Keyframe[frames], new Keyframe[frames], new Keyframe[frames], new Keyframe[frames] };

                // Average toe-out angle per leg, weighted by how flat the foot is (a raised heel has no useful yaw).
                var toeYaw = new Dictionary<string, float>();
                foreach (var side in Legs)
                {
                    float sum = 0f, weight = 0f;
                    for (int i = 0; i < frames; i++)
                    {
                        SamplePose(source, rig, root, pelvis, rootBind, pelvisBind, i, frames);
                        Vector3 toe = ToeDirection(rig.transform, pelvis, side);
                        float w = new Vector2(toe.x, toe.z).magnitude;
                        if (toe.z <= 0f) continue;
                        sum += Mathf.Atan2(toe.x, toe.z) * Mathf.Rad2Deg * w;
                        weight += w;
                    }
                    toeYaw[side] = weight > 0f ? sum / weight : 0f;
                }

                // Average sideways foot offset over the cycle = the straight line each foot should follow.
                var targetX = new Dictionary<string, float>();
                foreach (var side in Legs)
                {
                    float sum = 0f;
                    for (int i = 0; i < frames; i++)
                    {
                        SamplePose(source, rig, root, pelvis, rootBind, pelvisBind, i, frames);
                        TurnLeg(rig.transform, pelvis, side, -toeYaw[side]);
                        sum += FootOffset(rig.transform, pelvis, side).x;
                    }
                    targetX[side] = sum / frames;
                }

                var previous = new Dictionary<Transform, Quaternion>();
                for (int i = 0; i < frames; i++)
                {
                    float time = SamplePose(source, rig, root, pelvis, rootBind, pelvisBind, i, frames);
                    foreach (var side in Legs)
                    {
                        TurnLeg(rig.transform, pelvis, side, -toeYaw[side]);
                        StraightenLeg(rig.transform, pelvis, side, targetX[side]);
                    }
                    foreach (var bone in legBones)
                    {
                        Quaternion q = bone.localRotation;
                        // Keep quaternions on the same hemisphere so the curves interpolate the short way.
                        if (previous.TryGetValue(bone, out var prev) && Quaternion.Dot(prev, q) < 0f)
                            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        previous[bone] = q;

                        var k = keys[bone];
                        k[0][i] = new Keyframe(time, q.x);
                        k[1][i] = new Keyframe(time, q.y);
                        k[2][i] = new Keyframe(time, q.z);
                        k[3][i] = new Keyframe(time, q.w);
                    }
                }

                var clip = new AnimationClip { frameRate = SampleRate, name = System.IO.Path.GetFileNameWithoutExtension(outputPath) };
                string[] components = { "x", "y", "z", "w" };
                foreach (var bone in legBones)
                {
                    string path = AnimationUtility.CalculateTransformPath(bone, rig.transform);
                    for (int c = 0; c < 4; c++)
                    {
                        var curve = new AnimationCurve(keys[bone][c]);
                        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
                        clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + components[c], curve);
                    }
                }
                clip.EnsureQuaternionContinuity();

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    clip = existing;
                }
                else
                {
                    string folder = System.IO.Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                    AssetDatabase.CreateAsset(clip, outputPath);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Baked straight walk legs to {outputPath} ({frames} frames)");
                return clip;
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static float SamplePose(AnimationClip source, GameObject rig, Transform root, Transform pelvis,
            Quaternion rootBind, Quaternion pelvisBind, int frame, int frames)
        {
            float time = source.length * frame / (frames - 1);
            source.SampleAnimation(rig, time);
            // In game only the legs come from this clip; root and pelvis stay in the idle pose.
            root.localRotation = rootBind;
            pelvis.localRotation = pelvisBind;
            return time;
        }

        private static Vector3 FootOffset(Transform space, Transform pelvis, string side)
        {
            Transform foot = pelvis.Find($"thigh_{side}/calf_{side}/foot_{side}");
            return space.InverseTransformPoint(foot.position) - space.InverseTransformPoint(pelvis.position);
        }

        private static Vector3 ToeDirection(Transform space, Transform pelvis, string side)
        {
            Transform foot = pelvis.Find($"thigh_{side}/calf_{side}/foot_{side}");
            Transform ball = foot.Find("ball_" + side);
            return space.InverseTransformDirection(ball.position - foot.position);
        }

        // Turns the whole leg around the character's up axis (removes toe-out).
        private static void TurnLeg(Transform space, Transform pelvis, string side, float degrees)
        {
            Transform thigh = pelvis.Find("thigh_" + side);
            thigh.rotation = Quaternion.AngleAxis(degrees, space.up) * thigh.rotation;
        }

        // Rolls the thigh around the character's forward axis so the foot lands on targetX.
        private static void StraightenLeg(Transform space, Transform pelvis, string side, float targetX)
        {
            Transform thigh = pelvis.Find("thigh_" + side);
            Transform foot = thigh.Find($"calf_{side}/foot_{side}");

            Vector3 hip = space.InverseTransformPoint(thigh.position);
            Vector3 toFoot = space.InverseTransformPoint(foot.position) - hip;
            float hipX = hip.x - space.InverseTransformPoint(pelvis.position).x;

            float radius = new Vector2(toFoot.x, toFoot.y).magnitude;
            if (radius < 0.0001f) return;

            float desiredX = Mathf.Clamp(targetX - hipX, -radius, radius);
            float current = Mathf.Atan2(toFoot.x, -toFoot.y);
            float desired = Mathf.Asin(desiredX / radius);

            Vector3 axis = space.TransformDirection(Vector3.forward);
            // A positive roll around +forward swings a downward-pointing leg toward +x.
            thigh.rotation = Quaternion.AngleAxis((desired - current) * Mathf.Rad2Deg, axis) * thigh.rotation;
        }

        private static AnimationClip LoadClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            }
            return null;
        }
    }
}
