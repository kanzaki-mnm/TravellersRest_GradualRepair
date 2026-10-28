using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GradualRepair
{
    // Exact normalized IL matching, not a similarity score. Unknown/ambiguous code fails closed.
    internal static class NativeStructure
    {
        static bool Obfuscated(string name) => Regex.IsMatch(name, @"^[A-Z]{8,}$|^[gs]et_[A-Z]{8,}$|^<[A-Z]{8,}>k__BackingField$");
        internal static string Signature(MethodReference method) => method.DeclaringType.FullName + "|" + method.ReturnType.FullName + "|" + string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName)) + "|" + method.HasThis + "|" + method.GenericParameters.Count + "|" + method.CallingConvention;
        static string Operand(object value, MethodDefinition owner)
        {
            if (value is MethodReference method) return Obfuscated(method.Name) ? Signature(method) : method.FullName;
            if (value is FieldReference field) return field.DeclaringType.FullName + "|" + field.FieldType.FullName + "|" + (Obfuscated(field.Name) ? "" : field.Name);
            if (value is Instruction instruction) return "branch" + owner.Body.Instructions.IndexOf(instruction);
            if (value is Instruction[] targets) return "switch" + string.Join(",", targets.Select(i => owner.Body.Instructions.IndexOf(i)));
            if (value is VariableDefinition local) return "local" + local.Index;
            if (value is ParameterDefinition parameter) return "arg" + parameter.Index;
            if (value is IFormattable formatted) return formatted.ToString(null, CultureInfo.InvariantCulture);
            return value?.ToString() ?? "";
        }
        internal static string Fingerprint(MethodDefinition method)
        {
            if (!method.HasBody) return "";
            string body = string.Join("\n", method.Body.Instructions.Select(i => i.OpCode.Code + ":" + Operand(i.Operand, method)));
            body += "\nlocals:" + method.Body.InitLocals + ":" + string.Join(",", method.Body.Variables.Select(v => v.VariableType.FullName));
            foreach (var handler in method.Body.ExceptionHandlers)
                body += "\neh:" + handler.HandlerType + ":" + handler.CatchType?.FullName + ":" + method.Body.Instructions.IndexOf(handler.TryStart) + ":" + method.Body.Instructions.IndexOf(handler.TryEnd) + ":" + method.Body.Instructions.IndexOf(handler.HandlerStart) + ":" + method.Body.Instructions.IndexOf(handler.HandlerEnd) + ":" + method.Body.Instructions.IndexOf(handler.FilterStart);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", "");
        }
        internal static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(Types(t.NestedTypes)));
    }
    internal sealed class NativeAnchor
    {
        public string Type { get; set; }
        public string Signature { get; set; }
        public string Body { get; set; }
        public int Index { get; set; }
    }
    internal sealed class NativeContract
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Signature { get; set; }
        public string Body { get; set; }
        public string SetterBody { get; set; }
        public string ValueType { get; set; }
        public bool Static { get; set; }
        public List<NativeAnchor> Anchors = new List<NativeAnchor>();
    }
    internal sealed class NativeContractResolver
    {
        readonly ModuleDefinition module;
        readonly Dictionary<string, string> bodies = new Dictionary<string, string>();
        internal NativeContractResolver(ModuleDefinition module) { this.module = module; }
        string Body(MethodDefinition method)
        {
            if (!bodies.TryGetValue(method.FullName, out var body)) bodies[method.FullName] = body = NativeStructure.Fingerprint(method);
            return body;
        }
        internal string Resolve(NativeContract contract)
        {
            var type = module.GetType(contract.Key.Substring(0, contract.Key.LastIndexOf('.')));
            if (type == null) throw new MissingMemberException("Native type missing: " + contract.Key);
            IEnumerable<string> candidates;
            if (contract.Kind == "method") candidates = type.Methods.Where(m => NativeStructure.Signature(m) == contract.Signature && Body(m) == contract.Body).Select(m => m.Name).Distinct();
            else if (contract.Kind == "property") candidates = type.Properties.Where(p => p.PropertyType.FullName == contract.ValueType && p.GetMethod != null && p.GetMethod.IsStatic == contract.Static && NativeStructure.Signature(p.GetMethod) == contract.Signature && Body(p.GetMethod) == contract.Body && (contract.SetterBody == null || p.SetMethod != null && Body(p.SetMethod) == contract.SetterBody)).Select(p => p.Name);
            else candidates = type.Fields.Where(f => f.FieldType.FullName == contract.ValueType && f.IsStatic == contract.Static).Select(f => f.Name);
            var names = new HashSet<string>(candidates);
            // Field type alone is never sufficient. Correlate its use in verified method bodies.
            if (names.Count != 1 || contract.Kind == "field")
            {
                bool anchored = false;
                foreach (var anchor in contract.Anchors)
                {
                    var owner = module.GetType(anchor.Type);
                    if (owner == null) continue;
                    var hits = new HashSet<string>();
                    foreach (var method in owner.Methods.Where(m => m.HasBody && NativeStructure.Signature(m) == anchor.Signature && Body(m) == anchor.Body))
                    {
                        if (anchor.Index >= method.Body.Instructions.Count) continue;
                        var member = method.Body.Instructions[anchor.Index].Operand as MemberReference;
                        if (member?.DeclaringType.FullName == type.FullName) hits.Add(member.Name);
                    }
                    if (hits.Count == 0) continue;
                    anchored = true;
                    names.IntersectWith(hits);
                }
                if (contract.Kind == "field" && !anchored) names.Clear();
            }
            if (names.Count != 1) throw new MissingMemberException("Native structural contract is missing or ambiguous: " + contract.Key + " (candidates=" + names.Count + ")");
            return names.Single();
        }
    }
}
