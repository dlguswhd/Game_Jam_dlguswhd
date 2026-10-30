using GameJam.World;
using UnityEngine;
using UnityEngine.AI;

namespace GameJam.Enemies
{
    // A sound the player made. It reaches an enemy if it can travel there within its radius by the shortest of
    //  - straight through open air (nothing solid in between),
    //  - along the walkable route (NavMesh path through doorways and stairs), each closed door on the way
    //    counting as extra distance because it muffles the sound,
    //  - straight through walls and floors, heavily muffled (distance × WallMuffling).
    // So footsteps don't carry through a wall or ceiling, but a loud gunshot still does.
    public static class Noise
    {
        // Extra distance a closed door adds to the route (m).
        public static float ClosedDoorPenalty = 3f;
        // Straight-line distance multiplier for sound passing through solid walls / floors.
        public static float WallMuffling = 3f;

        private static readonly NavMeshPath Path = new NavMeshPath();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static void Emit(Vector3 position, float radius, float suspicion)
        {
            for (int i = EnemyAI.All.Count - 1; i >= 0; i--)
            {
                var enemy = EnemyAI.All[i];
                // A route is never shorter than the straight line, so far enemies are skipped cheaply.
                if ((enemy.transform.position - position).sqrMagnitude > radius * radius) continue;
                if (TravelDistance(position, enemy) > radius) continue;
                enemy.HearNoise(position, suspicion);
            }
        }

        // An enemy's gunshot: every other enemy it carries to joins the fight, heading for where the shooter
        // is aiming (the player), not for the shooter.
        public static void EmitGunfire(EnemyAI shooter, float radius, Vector3 target)
        {
            Vector3 origin = shooter.transform.position;
            for (int i = EnemyAI.All.Count - 1; i >= 0; i--)
            {
                var enemy = EnemyAI.All[i];
                if (enemy == shooter) continue;
                if ((enemy.transform.position - origin).sqrMagnitude > radius * radius) continue;
                if (TravelDistance(origin, enemy) > radius) continue;
                enemy.HearGunfire(target);
            }
        }

        private static float TravelDistance(Vector3 source, EnemyAI enemy)
        {
            Vector3 ear = enemy.transform.position + Vector3.up * 1.6f;
            Vector3 origin = source + Vector3.up * 1f;
            float straight = Vector3.Distance(origin, ear);
            if (IsOpenAir(origin, ear)) return straight;

            float throughWalls = straight * WallMuffling;
            if (!NavMesh.SamplePosition(source, out NavMeshHit from, 2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit to, 2f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, Path) ||
                Path.status != NavMeshPathStatus.PathComplete)
                return throughWalls;

            float distance = 0f;
            Vector3[] corners = Path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                distance += Vector3.Distance(corners[i - 1], corners[i]);
                distance += ClosedDoorsOn(corners[i - 1], corners[i]) * ClosedDoorPenalty;
            }

#if UNITY_EDITOR
            for (int i = 1; i < corners.Length; i++) Debug.DrawLine(corners[i - 1], corners[i], Color.cyan, 0.5f);
#endif
            return Mathf.Min(distance, throughWalls);
        }

        // Nothing but characters (player, enemies, their hitboxes) between the sound and the ear.
        private static bool IsOpenAir(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, Hits, delta.magnitude, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider.GetComponentInParent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }

        // Closed doors whose doorway this route segment passes through.
        private static int ClosedDoorsOn(Vector3 a, Vector3 b)
        {
            int count = 0;
            foreach (var door in Door.All)
            {
                if (door.IsOpen) continue;
                Vector3 doorway = door.transform.position;
                if (Mathf.Abs(doorway.y - Mathf.Min(a.y, b.y)) > 1.5f) continue;

                Vector2 p = new Vector2(doorway.x, doorway.z);
                Vector2 s = new Vector2(a.x, a.z), e = new Vector2(b.x, b.z);
                Vector2 se = e - s;
                float t = se.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - s, se) / se.sqrMagnitude) : 0f;
                if ((s + se * t - p).magnitude < 0.8f) count++;
            }
            return count;
        }
    }
}
