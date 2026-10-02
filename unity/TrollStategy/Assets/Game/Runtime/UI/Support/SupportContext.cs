using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TrollStrategy.Support;

namespace TrollStrategy.UI
{
    /// <summary>What the support corner reads and calls; tests pass stand-ins for the report.</summary>
    public sealed class SupportContext
    {
        public SupportContext(BuildInfo build, FrameRateMeter frames,
            Func<IReadOnlyList<KeyValuePair<string, string>>> rows,
            Func<Action<float>, Task<ReportOutcome>> sendReport, Telemetry telemetry = null)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            SendReport = sendReport ?? throw new ArgumentNullException(nameof(sendReport));
            Telemetry = telemetry;
        }

        public BuildInfo Build { get; }
        public FrameRateMeter Frames { get; }
        /// <summary>The tech panel's rows as they are now.</summary>
        public Func<IReadOnlyList<KeyValuePair<string, string>>> Rows { get; }
        /// <summary>Gathers and sends a report; the action hears upload progress from 0 to 1.</summary>
        public Func<Action<float>, Task<ReportOutcome>> SendReport { get; }
        /// <summary>The play statistics the player can turn off; null hides the switch.</summary>
        public Telemetry Telemetry { get; }
    }
}
