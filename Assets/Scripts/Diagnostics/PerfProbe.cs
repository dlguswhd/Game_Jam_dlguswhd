using System.Collections.Generic;
using System.Text;
using Unity.Profiling;

namespace GameJam.Diagnostics
{
    // Development aid: samples a few profiler counters over many frames and reports averages / worst frames.
    // Start() → play for a while → Report(). Used from the editor to find what costs frame time.
    public static class PerfProbe
    {
        // Our own hot paths, wrapped in ProfilerMarkers with these names.
        public const string EnemyUpdate = "GameJam.EnemyAI.Update";
        public const string EnemyRepath = "GameJam.EnemyAI.Repath";
        public const string EnemyPerception = "GameJam.EnemyAI.Perception";
        public const string EnemyMovement = "GameJam.EnemyAI.Movement";
        public const string EnemyFiring = "GameJam.EnemyAI.Firing";
        public const string GunfireAlert = "GameJam.Noise.Gunfire";
        public const string NoiseEmit = "GameJam.Noise.Emit";
        public const string DoorUpdate = "GameJam.Door.Update";

        private static readonly (ProfilerCategory category, string name)[] Counters =
        {
            (ProfilerCategory.Internal, "Main Thread"),
            (ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate"),
            (ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            (ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd"),
            (ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate"),
            (ProfilerCategory.Render, "PostLateUpdate.FinishFrameRendering"),
            (ProfilerCategory.Memory, "GC Allocated In Frame"),
            (ProfilerCategory.Scripts, EnemyUpdate),
            (ProfilerCategory.Scripts, EnemyRepath),
            (ProfilerCategory.Scripts, EnemyPerception),
            (ProfilerCategory.Scripts, EnemyMovement),
            (ProfilerCategory.Scripts, EnemyFiring),
            (ProfilerCategory.Scripts, GunfireAlert),
            (ProfilerCategory.Scripts, NoiseEmit),
            (ProfilerCategory.Scripts, DoorUpdate),
        };

        private static readonly List<ProfilerRecorder> Recorders = new List<ProfilerRecorder>();

        public static void Start()
        {
            Stop();
            foreach (var (category, name) in Counters)
                Recorders.Add(ProfilerRecorder.StartNew(category, name, 600));
        }

        public static void Stop()
        {
            foreach (var r in Recorders) r.Dispose();
            Recorders.Clear();
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Recorders.Count; i++)
            {
                var r = Recorders[i];
                if (!r.Valid || r.Count == 0)
                {
                    sb.AppendLine($"{Counters[i].name}: (no data)");
                    continue;
                }

                var samples = new List<ProfilerRecorderSample>(r.Capacity);
                r.CopyTo(samples);
                var values = new List<long>(samples.Count);
                foreach (var s in samples) values.Add(s.Value);
                values.Sort();

                // Median and 95th percentile: the frames where an editor command runs (and allocates) would
                // otherwise dominate an average.
                bool bytes = r.UnitType == ProfilerMarkerDataUnit.Bytes;
                double scale = bytes ? 1.0 / 1024.0 : 1e-6;
                string unit = bytes ? "KB" : "ms";
                double median = values[values.Count / 2] * scale;
                double p95 = values[(int)(values.Count * 0.95f)] * scale;
                double max = values[values.Count - 1] * scale;
                sb.AppendLine($"{Counters[i].name}: median {median:F2} {unit}, p95 {p95:F2} {unit}, max {max:F2} {unit} ({values.Count} frames)");
            }
            return sb.ToString();
        }
    }
}
