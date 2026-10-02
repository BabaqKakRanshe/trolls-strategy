using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>
    /// Unity Gaming Services, started once for reports and analytics alike: whoever asks first starts them, a
    /// second caller waits for the same start, and a failed start is tried again on the next call.
    /// </summary>
    public static class CloudServices
    {
        private static Task s_start;

        /// <summary>The project is linked to Unity Cloud; without it there is no service to start.</summary>
        public static bool Linked => !string.IsNullOrEmpty(UnityEngine.Application.cloudProjectId);

        public static Task StartAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return Task.CompletedTask;
            if (s_start == null || s_start.IsFaulted || s_start.IsCanceled) s_start = UnityServices.InitializeAsync();
            return s_start;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => s_start = null;
    }
}
