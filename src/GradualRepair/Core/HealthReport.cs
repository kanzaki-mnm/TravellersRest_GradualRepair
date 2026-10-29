using System;
using System.Collections.Generic;
using System.Linq;
namespace GradualRepair
{
    internal sealed class HealthReport
    {
        string[] snapshot = { "Pending", "Initializing compatibility diagnostics", "Required references and patch installation have not completed." };
        readonly Dictionary<string, string> failures = new Dictionary<string, string>();
        readonly Dictionary<string, string> warnings = new Dictionary<string, string>();
        string scope = "Startup inspection has not completed. Runtime scene/UI/input/inventory/dialogue and actual repair/save-load behavior are unverified. Diagnosis does not call game functions or access saves.";
        bool complete, disabled;
        internal string[] Read() => (string[])snapshot.Clone();
        internal void Complete(string details) { scope = details; complete = true; Publish(); }
        internal void Disable() { disabled = true; Publish(); }
        internal void Fail(string feature, string reason) { failures[feature] = Limit(reason); Publish(); }
        internal void Warn(string feature, string reason) { warnings[feature] = Limit(reason); Publish(); }
        static string Limit(string text) => (text ?? "Unknown error").Length > 1200 ? text.Substring(0, 1200) : text ?? "Unknown error";
        void Publish()
        {
            string status = failures.Count > 0 ? "Failed" : disabled ? "Disabled" : !complete ? "Pending" : warnings.Count > 0 ? "Warning" : "Healthy";
            string summary = status == "Failed" ? "Required checks or runtime operations failed; affected operations stopped"
                : status == "Disabled" ? "Disabled by configuration" : status == "Pending" ? "Compatibility initialization pending"
                : status == "Warning" ? "Checks passed with compatibility fallback or runtime warnings" : "Startup references and required patches verified";
            string details = scope + "\n" + string.Join("\n", failures.Select(p => "FAILED " + p.Key + ": " + p.Value))
                + "\n" + string.Join("\n", warnings.Select(p => "WARNING " + p.Key + ": " + p.Value));
            snapshot = new[] { status, summary, details.Length > 15500 ? details.Substring(0, 15500) : details };
        }
    }
}
