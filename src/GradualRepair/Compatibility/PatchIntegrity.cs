using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
namespace GradualRepair
{
    internal static class PatchIntegrity
    {
        internal static void Verify(MethodInfo target, string owner, MethodInfo expected, string role)
        {
            var info = Harmony.GetPatchInfo(target);
            var patches = info == null ? null : role == "prefix" ? info.Prefixes : role == "postfix" ? info.Postfixes : role == "finalizer" ? info.Finalizers : null;
            if (patches == null || !patches.Any(p => p.owner == owner && p.PatchMethod == expected))
                throw new InvalidOperationException("Required owned " + role + " missing: " + target.DeclaringType.FullName + "." + target.Name + " -> " + expected.Name);
        }
    }
}
