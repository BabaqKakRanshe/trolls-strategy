using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using UnityEngine;
using UnityEngine.UnityConsent;

namespace TrollStrategy.Support
{
    /// <summary>
    /// Unity Analytics: events show in the project's Unity Dashboard under Analytics. The service only takes an
    /// event whose name and fields are declared there (docs/analytics.md lists them). Consent goes through the
    /// engine's end-user consent state, which the service follows.
    /// </summary>
    public sealed class UnityAnalyticsBackend : IAnalyticsBackend
    {
        public string PrivacyUrl => "https://unity.com/legal/game-player-and-app-user-privacy-policy";

        public async Task<bool> StartAsync()
        {
            if (!CloudServices.Linked)
            {
                Debug.LogWarning("[Support] The project is not linked to Unity Cloud; no analytics.");
                return false;
            }
            await CloudServices.StartAsync();
            return true;
        }

        public void SetConsent(bool granted)
        {
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);
        }

        public void Record(string name, IReadOnlyList<KeyValuePair<string, object>> fields)
        {
            var e = new CustomEvent(name);
            if (fields != null)
                foreach (var field in fields)
                    e.Add(field.Key, field.Value);
            AnalyticsService.Instance.RecordEvent(e);
        }

        public void Flush() => AnalyticsService.Instance.Flush();
    }
}
