using GameJam.Enemies;
using GameJam.Player;
using UnityEngine;

namespace GameJam.UI
{
    // Test-map only: top-right list of every enemy's state and suspicion, plus the player's movement noise.
    // Plain IMGUI on purpose — it's a debugging aid, not game UI.
    public class EnemyDebugOverlay : MonoBehaviour
    {
        private static readonly Color PatrolColor = new Color(0.4f, 1f, 0.5f);
        private static readonly Color SuspiciousColor = new Color(1f, 0.65f, 0.2f);
        private static readonly Color CombatColor = new Color(1f, 0.35f, 0.35f);

        private PlayerMovement _movement;
        private GUIStyle _style;

        private void Start()
        {
            _movement = FindFirstObjectByType<PlayerMovement>();
        }

        private void OnGUI()
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            const float width = 330f;
            float lineHeight = _style.fontSize + 6f;
            float height = (EnemyAI.All.Count + 3) * lineHeight + 12f;
            var area = new Rect(Screen.width - width - 16f, 16f, width, height);
            GUI.Box(area, GUIContent.none);

            float y = area.y + 6f;
            Line(area, ref y, lineHeight, "<b>AI 상태</b>  (S = 의심도)", Color.white);
            foreach (var enemy in EnemyAI.All)
            {
                Color color = enemy.State == AlertState.Combat ? CombatColor
                    : enemy.State == AlertState.Suspicious ? SuspiciousColor
                    : PatrolColor;
                Line(area, ref y, lineHeight, $"{enemy.name}  {enemy.State}  S={enemy.Suspicion:F0}", color);
            }

            string gait = _movement != null ? _movement.Gait.ToString() : "-";
            y += 4f;
            Line(area, ref y, lineHeight, $"플레이어 이동: {gait}", Color.white);
        }

        private void Line(Rect area, ref float y, float height, string text, Color color)
        {
            _style.normal.textColor = color;
            GUI.Label(new Rect(area.x + 10f, y, area.width - 20f, height), text, _style);
            y += height;
        }
    }
}
