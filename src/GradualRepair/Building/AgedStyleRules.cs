using System;
using System.Collections.Generic;
namespace GradualRepair
{
    public static class AgedStyleRules
    {
        // Build into a replacement dictionary first: failed imports cannot partly
        // overwrite state. Explicit false is a deliberate normal-material choice.
        public static int ImportDefaults(RepairState state, IEnumerable<string> defaults)
        {
            if (state.AgedMaterialsInitialized) return 0;
            var next = new Dictionary<string, bool>(state.AgedDecorations); int added = 0;
            foreach (var key in defaults) if (!next.ContainsKey(key)) { next.Add(key, true); added++; }
            if (next.Count > 200000) throw new InvalidOperationException("Too many initial material cells");
            state.AgedDecorations = next; state.AgedMaterialsInitialized = true;
            return added;
        }
    }
}
