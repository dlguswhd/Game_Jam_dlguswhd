using GameJam.Combat;
using GameJam.Enemies;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Builds the three gang member enemies (rifle / shotgun / pistol) from the baked humanoid clips.
    public static class EnemySetupTool
    {
        private const string ClipFolder = HumanoidClipBaker.OutputFolder;
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string Weapons = "Assets/KINEMATION/TacticalShooterPack/Prefabs/Weapons/";
        private const string Models = "Assets/Gang members - characters pack/Prefabs/";
        private const string OperatorHumanoid =
            "Assets/KINEMATION/TacticalShooterPack/Meshes/Character/Operator/SKM_Operator_Humanoid.fbx";

        private class Config
        {
            public EnemyWeaponClass weaponClass;
            public string model;
            public string weaponPrefab;
            // Hand bone poses in weapon space, measured from the player (SKM_Operator) holding each gun.
            public Vector3 rightGripPosition;
            public Quaternion rightGripRotation;
            public Vector3 leftGripPosition;
            public Quaternion leftGripRotation;
            public float sightDistance;
            public string idle, fire, reloadStart, reloadLoop, reloadEnd;
            public float health;
            public float damage;
            public int pellets;
            public float spread;
            public float fireInterval;
            public Vector2Int burst;
            public Vector2 burstPause;
            public int magazine;
            public float reloadTime;
            public float attackRange;
            public float range;
        }

        private static readonly Config[] Configs =
        {
            new Config
            {
                weaponClass = EnemyWeaponClass.Rifle, model = "Man_1", weaponPrefab = "W_AK105",
                rightGripPosition = new Vector3(0.0153f, -0.1134f, -0.2002f),
                rightGripRotation = new Quaternion(-0.55787f, -0.55359f, -0.27819f, 0.55221f),
                leftGripPosition = new Vector3(-0.0325f, -0.0531f, 0.1100f),
                leftGripRotation = new Quaternion(0.35263f, 0.66748f, -0.35105f, 0.55398f),
                sightDistance = 0.1f,
                idle = "Rifle_Idle", fire = null, reloadStart = "Rifle_Reload",
                health = 100f, damage = 7f, pellets = 1, spread = 2.5f, fireInterval = 60f / 650f,
                burst = new Vector2Int(3, 6), burstPause = new Vector2(0.7f, 1.3f), magazine = 30,
                reloadTime = 3.3f, attackRange = 30f, range = 80f
            },
            new Config
            {
                weaponClass = EnemyWeaponClass.Shotgun, model = "Man_2", weaponPrefab = "W_Herrington_11-87_Police",
                rightGripPosition = new Vector3(0.0460f, -0.0362f, -0.3173f),
                rightGripRotation = new Quaternion(-0.43330f, -0.53331f, -0.48390f, 0.54190f),
                leftGripPosition = new Vector3(-0.0359f, -0.0393f, 0.0714f),
                leftGripRotation = new Quaternion(-0.30135f, -0.68634f, 0.31206f, -0.58373f),
                sightDistance = 0.1f,
                idle = "Shotgun_Idle", fire = "Shotgun_Fire", reloadStart = "Shotgun_Reload_Start",
                reloadLoop = "Shotgun_Reload_Loop", reloadEnd = "Shotgun_Reload_End",
                health = 130f, damage = 9f, pellets = 8, spread = 5f, fireInterval = 0.9f,
                burst = new Vector2Int(1, 1), burstPause = new Vector2(0.9f, 1.4f), magazine = 6,
                reloadTime = 0f, attackRange = 14f, range = 25f
            },
            new Config
            {
                weaponClass = EnemyWeaponClass.Pistol, model = "Man_3", weaponPrefab = "W_WK-11_Viper",
                rightGripPosition = new Vector3(0.0131f, -0.0730f, -0.1102f),
                rightGripRotation = new Quaternion(-0.51039f, -0.59421f, -0.26680f, 0.56146f),
                leftGripPosition = new Vector3(-0.0367f, -0.1042f, -0.0868f),
                leftGripRotation = new Quaternion(-0.21719f, -0.61705f, 0.51386f, -0.55500f),
                sightDistance = 0.4f,
                idle = "Pistol_Idle", fire = "Pistol_Fire", reloadStart = "Pistol_Reload",
                health = 80f, damage = 12f, pellets = 1, spread = 2f, fireInterval = 0.45f,
                burst = new Vector2Int(2, 3), burstPause = new Vector2(0.8f, 1.5f), magazine = 26,
                reloadTime = 3.4f, attackRange = 22f, range = 60f
            },
        };

        [MenuItem("Tools/Game Jam/Build Enemy Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);

            var mask = CreateUpperBodyMask();
            foreach (var config in Configs)
            {
                var controller = BuildController(config, mask);
                BuildPrefab(config, controller);
            }

            AssetDatabase.SaveAssets();

            // The controllers are recreated above; without a reimport the saved prefabs can keep a
            // dangling (null) controller reference until the next asset refresh.
            foreach (var config in Configs)
            {
                AssetDatabase.ImportAsset($"{PrefabFolder}/Enemy_{config.weaponClass}.prefab",
                    ImportAssetOptions.ForceUpdate);
            }

            Debug.Log("Enemy prefabs built in " + PrefabFolder);
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            string path = ClipFolder + "/Mask_EnemyUpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part != AvatarMaskBodyPart.Root && part != AvatarMaskBodyPart.LeftLeg &&
                             part != AvatarMaskBodyPart.RightLeg && part != AvatarMaskBodyPart.LeftFootIK &&
                             part != AvatarMaskBodyPart.RightFootIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorController BuildController(Config config, AvatarMask upperMask)
        {
            string path = $"{ClipFolder}/AC_Enemy_{config.weaponClass}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
                { name = "WalkDir", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ReloadLoop", AnimatorControllerParameterType.Bool);

            AnimationClip idle = Clip(config.idle);
            AnimationClip walk = Clip("Enemy_Walk");

            // Base layer: legs. Idle pose blends into the walk cycle by Speed.
            var baseMachine = controller.layers[0].stateMachine;
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "WalkDir";
            baseMachine.defaultState = locomotion;

            // Death motions, started from EnemyAI with a cross-fade (no transitions needed).
            foreach (var death in DeathClips)
            {
                var state = baseMachine.AddState(death.Key);
                state.motion = LoadModelClip(death.Value);
            }

            // Upper body: always holding the weapon, with fire and reload on top.
            var upperMachine = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(upperMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "UpperBody", stateMachine = upperMachine, avatarMask = upperMask,
                defaultWeight = 1f, iKPass = true, blendingMode = AnimatorLayerBlendingMode.Override
            });

            var hold = upperMachine.AddState("Hold");
            hold.motion = idle;
            upperMachine.defaultState = hold;

            if (config.fire != null)
            {
                var fire = upperMachine.AddState("Fire");
                fire.motion = Clip(config.fire);
                var toFire = upperMachine.AddAnyStateTransition(fire);
                toFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
                toFire.duration = 0.05f;
                toFire.canTransitionToSelf = true;
                ExitTo(fire, hold, 0.9f, 0.15f);
            }

            var reloadStart = upperMachine.AddState("ReloadStart");
            reloadStart.motion = Clip(config.reloadStart);
            var toReload = upperMachine.AddAnyStateTransition(reloadStart);
            toReload.AddCondition(AnimatorConditionMode.If, 0f, "Reload");
            toReload.duration = 0.15f;
            toReload.canTransitionToSelf = false;

            if (config.reloadLoop != null)
            {
                var loop = upperMachine.AddState("ReloadLoop");
                loop.motion = Clip(config.reloadLoop);
                var end = upperMachine.AddState("ReloadEnd");
                end.motion = Clip(config.reloadEnd);

                var startToLoop = ExitTo(reloadStart, loop, 1f, 0.05f);
                startToLoop.AddCondition(AnimatorConditionMode.If, 0f, "ReloadLoop");
                var startToEnd = ExitTo(reloadStart, end, 1f, 0.05f);
                startToEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "ReloadLoop");
                var loopToEnd = ExitTo(loop, end, 0.95f, 0.05f);
                loopToEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "ReloadLoop");
                ExitTo(end, hold, 0.9f, 0.2f);
            }
            else
            {
                ExitTo(reloadStart, hold, 0.95f, 0.2f);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorStateTransition ExitTo(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
            return t;
        }

        private static void BuildPrefab(Config config, AnimatorController controller)
        {
            var root = new GameObject($"Enemy_{config.weaponClass}");
            try
            {
                var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + config.model + ".prefab");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                model.AddComponent<EnemyAnimatorIK>();

                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + config.weaponPrefab + ".prefab");
                var weaponObject = AttachWeapon(config, model, weaponPrefab);

                var controllerComp = root.AddComponent<CharacterController>();
                // Stops at the neck so shots to the head reach the head hitbox instead of the capsule.
                controllerComp.center = new Vector3(0f, 0.78f, 0f);
                controllerComp.height = 1.56f;
                controllerComp.radius = 0.3f;
                controllerComp.skinWidth = 0.02f;

                var health = root.AddComponent<Health>();
                SetField(health, "maxHealth", config.health);

                var audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 3f;
                audio.maxDistance = 80f;

                var ai = root.AddComponent<EnemyAI>();
                var so = new SerializedObject(ai);
                so.FindProperty("weapon").objectReferenceValue = weaponObject.GetComponentInChildren<TacticalShooterWeapon>(true);
                so.FindProperty("weaponClass").enumValueIndex = (int)config.weaponClass;
                so.FindProperty("damage").floatValue = config.damage;
                so.FindProperty("pellets").intValue = config.pellets;
                so.FindProperty("spreadAngle").floatValue = config.spread;
                so.FindProperty("fireInterval").floatValue = config.fireInterval;
                so.FindProperty("burstSize").vector2IntValue = config.burst;
                so.FindProperty("burstPause").vector2Value = config.burstPause;
                so.FindProperty("magazineSize").intValue = config.magazine;
                so.FindProperty("reloadTime").floatValue = config.reloadTime;
                so.FindProperty("attackRange").floatValue = config.attackRange;
                so.FindProperty("range").floatValue = config.range;
                AssignStepSounds(so);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Headshots kill outright (HitZone.instantKill).
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    var hitbox = new GameObject("HeadHitbox");
                    hitbox.transform.SetParent(head, false);
                    hitbox.transform.position = head.position + Vector3.up * 0.08f;
                    hitbox.transform.rotation = Quaternion.identity;
                    var sphere = hitbox.AddComponent<SphereCollider>();
                    sphere.radius = 0.13f / Mathf.Max(0.0001f, hitbox.transform.lossyScale.x);
                    var zone = hitbox.AddComponent<HitZone>();
                    SetField(zone, "damageMultiplier", 2.5f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // KINEMATION aims the gun procedurally, so the clip's gun bone isn't a usable grip pose.
        // The weapon sits under the model; EnemyAnimatorIK places it each frame and solves the arms onto
        // the grips. The grips were measured on SKM_Operator, so the hand rotations are converted to this
        // character's hand bone axes by putting both rigs in the same humanoid pose and comparing hands.
        private static GameObject AttachWeapon(Config config, GameObject model, GameObject weaponPrefab)
        {
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, model.transform);
            weapon.transform.localPosition = new Vector3(0.2f, 1.3f, 0.3f);
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = Vector3.one;

            Quaternion rightAxes = HandAxisConversion(model, HumanBodyBones.RightHand, config.idle);
            Quaternion leftAxes = HandAxisConversion(model, HumanBodyBones.LeftHand, config.idle);

            var ik = model.GetComponent<EnemyAnimatorIK>();
            var so = new SerializedObject(ik);
            so.FindProperty("weaponRoot").objectReferenceValue = weapon.transform;
            so.FindProperty("rightGripPosition").vector3Value = config.rightGripPosition;
            so.FindProperty("rightGripRotation").quaternionValue = config.rightGripRotation * rightAxes;
            so.FindProperty("leftGripPosition").vector3Value = config.leftGripPosition;
            so.FindProperty("leftGripRotation").quaternionValue = config.leftGripRotation * leftAxes;
            so.FindProperty("sightDistance").floatValue = config.sightDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
            return weapon;
        }

        // Rotation that maps an SKM_Operator hand bone orientation onto this model's hand bone.
        private static Quaternion HandAxisConversion(GameObject model, HumanBodyBones bone, string poseClip)
        {
            var operatorRig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OperatorHumanoid));
            var copy = Object.Instantiate(model);
            try
            {
                operatorRig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                AnimationClip pose = Clip(poseClip);
                pose.SampleAnimation(operatorRig, 0f);
                pose.SampleAnimation(copy, 0f);

                Quaternion operatorHand = operatorRig.GetComponent<Animator>().GetBoneTransform(bone).rotation;
                Quaternion modelHand = copy.GetComponent<Animator>().GetBoneTransform(bone).rotation;
                return Quaternion.Inverse(operatorHand) * modelHand;
            }
            finally
            {
                Object.DestroyImmediate(operatorRig);
                Object.DestroyImmediate(copy);
            }
        }

        // State name -> model file holding the (humanoid, Man_1 avatar) death clip.
        private static readonly System.Collections.Generic.KeyValuePair<string, string>[] DeathClips =
        {
            new System.Collections.Generic.KeyValuePair<string, string>(EnemyAI.DeathState1, "Assets/Death_1.fbx"),
            new System.Collections.Generic.KeyValuePair<string, string>(EnemyAI.DeathState2, "Assets/Death_2.fbx"),
            new System.Collections.Generic.KeyValuePair<string, string>(EnemyAI.HeadshotDeathState, "Assets/Headshot_death.fbx"),
        };

        private static AnimationClip LoadModelClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            Debug.LogWarning($"No animation clip found in {path}");
            return null;
        }

        private static AnimationClip Clip(string name)
        {
            return string.IsNullOrEmpty(name)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{name}.anim");
        }

        private const string StepSoundFolder = "Assets/KINEMATION/TacticalShooterPack/SFX/General/";

        // Same concrete walk / run clips the player uses.
        private static void AssignStepSounds(SerializedObject ai)
        {
            FillClips(ai.FindProperty("walkStepSounds"), "Walk/S_Walk_Concrete_");
            FillClips(ai.FindProperty("runStepSounds"), "Run/S_Run_Concrete_");
        }

        private static void FillClips(SerializedProperty list, string prefix)
        {
            list.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"{StepSoundFolder}{prefix}{i + 1}.WAV");
            }
        }

        // Adds the step sounds to the existing prefabs without rebuilding them (keeps tuned inspector values).
        [MenuItem("Tools/Game Jam/Assign Enemy Step Sounds")]
        public static void AssignStepSoundsToPrefabs()
        {
            foreach (var weaponClass in new[] { "Rifle", "Shotgun", "Pistol" })
            {
                string path = $"{PrefabFolder}/Enemy_{weaponClass}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                var so = new SerializedObject(root.GetComponent<EnemyAI>());
                AssignStepSounds(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("Enemy step sounds assigned.");
        }

        private static void SetField(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
