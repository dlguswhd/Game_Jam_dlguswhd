using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Enemies
{
    // Debug only: a small unlit ball above the head showing the AI state.
    // Green = Patrol, orange = Suspicious, red = Combat. Toggle with EnemyAI → Show State Indicator.
    public class EnemyStateIndicator : MonoBehaviour
    {
        private static readonly Color PatrolColor = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color SuspiciousColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color CombatColor = new Color(1f, 0.15f, 0.15f);

        private const float Size = 0.18f;
        private const float HeightAboveHead = 0.35f;

        private EnemyAI _ai;
        private Transform _head;
        private Transform _ball;
        private Material _material;

        private void Start()
        {
            _ai = GetComponent<EnemyAI>();
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) _head = animator.GetBoneTransform(HumanBodyBones.Head);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "StateIndicator";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * Size;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            var renderer = ball.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Not parented to the enemy, so it never inherits the death animation or gets hit by rays.
            _ball = ball.transform;
        }

        private void LateUpdate()
        {
            if (_ai == null || _ai.IsDead)
            {
                if (_ball != null) Destroy(_ball.gameObject);
                enabled = false;
                return;
            }

            Vector3 anchor = _head != null ? _head.position : transform.position + Vector3.up * 1.7f;
            _ball.position = anchor + Vector3.up * HeightAboveHead;

            Color color = _ai.State == AlertState.Combat ? CombatColor
                : _ai.State == AlertState.Suspicious ? SuspiciousColor
                : PatrolColor;
            _material.SetColor("_BaseColor", color);
            _material.color = color;
        }

        private void OnDestroy()
        {
            if (_ball != null) Destroy(_ball.gameObject);
            if (_material != null) Destroy(_material);
        }
    }
}
