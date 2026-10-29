using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GradualRepair
{
    internal static class CompiledContracts
    {
        // Direct calls use CLR member references, rather than a second reflection lookup.
        // Validate those very references against installed metadata before any feature runs.
        internal static int Audit(string pluginPath, string gameDirectory)
        {
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(gameDirectory);
                resolver.AddSearchDirectory(Path.GetDirectoryName(pluginPath));
                using (var plugin = AssemblyDefinition.ReadAssembly(pluginPath, new ReaderParameters { AssemblyResolver = resolver }))
                {
                    int count = 0;
                    foreach (var type in NativeStructure.Types(plugin.MainModule.Types))
                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (!(instruction.Operand is MemberReference member)) continue;
                        var scope = member.DeclaringType?.Scope?.Name;
                        if (scope != "Assembly-CSharp" && scope != "Assembly-CSharp.dll" && scope != "Assembly-CSharp-firstpass" && scope != "Assembly-CSharp-firstpass.dll") continue;
                        if (member is MethodReference call)
                        {
                            var resolved = call.Resolve();
                            if (resolved == null || resolved.HasThis != call.HasThis || resolved.ReturnType.FullName != call.ReturnType.FullName
                                || !resolved.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(call.Parameters.Select(p => p.ParameterType.FullName))
                                || resolved.GenericParameters.Count != call.GenericParameters.Count)
                                throw new MissingMethodException("Compiled game contract: " + call.FullName);
                        }
                        else if (member is FieldReference field)
                        {
                            var resolved = field.Resolve();
                            bool staticAccess = instruction.OpCode.Code == Code.Ldsfld || instruction.OpCode.Code == Code.Stsfld || instruction.OpCode.Code == Code.Ldsflda;
                            if (resolved == null || resolved.FieldType.FullName != field.FieldType.FullName || (instruction.OpCode.Code != Code.Ldtoken && resolved.IsStatic != staticAccess))
                                throw new MissingFieldException("Compiled game contract: " + field.FullName);
                        }
                        else if (member is TypeReference reference && reference.Resolve() == null) throw new TypeLoadException(reference.FullName);
                        count++;
                    }
                    return count;
                }
            }
        }
    }
}
