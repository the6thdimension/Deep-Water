using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace POSEIDON.Reckoning.Tests
{
    /// <summary>
    /// Reads compiled IL and reports every reference to an API that breaks determinism. Works from
    /// the bytes the compiler produced, so aliases, <c>using static</c> and lambdas cannot hide a
    /// call, as they could from a text search. It uses plain reflection and needs no Mono.Cecil
    /// package.
    ///
    /// What it cannot see: anything done through reflection or function pointers (calli), and
    /// reference types named only in a signature or a local with no member touched. It also does
    /// not judge whether a Dictionary's order "decides an outcome"; it bans enumerating one outright,
    /// the strict form of standing constraint 1.
    ///
    /// Fails closed: a token it cannot resolve is reported as a violation, not skipped.
    /// </summary>
    internal static class BannedApiGuard
    {
        public readonly struct Report
        {
            public readonly int TypesScanned;
            public readonly int MethodsScanned;
            public readonly int ReferencesChecked;
            public readonly IReadOnlyList<string> Violations;

            public Report(int types, int methods, int references, IReadOnlyList<string> violations)
            {
                TypesScanned = types;
                MethodsScanned = methods;
                ReferencesChecked = references;
                Violations = violations;
            }
        }

        // Every member of these types is banned.
        private static readonly HashSet<string> BannedTypes = new HashSet<string>
        {
            "UnityEngine.Time",
            "UnityEngine.Random",
            "UnityEngine.Physics",
            "UnityEngine.Physics2D",
            "UnityEngine.MonoBehaviour",
            "System.Random",
            "System.Diagnostics.Stopwatch",
            "System.Threading.Thread",
            "System.Threading.ThreadPool",
            "System.Threading.Tasks.Task",
            "System.Threading.Tasks.Parallel",
        };

        // (declaring type, member) pairs.
        private static readonly HashSet<string> BannedMembers = new HashSet<string>
        {
            "System.DateTime.get_Now",
            "System.DateTime.get_UtcNow",
            "System.DateTime.get_Today",
            "System.DateTimeOffset.get_Now",
            "System.DateTimeOffset.get_UtcNow",
            "System.Environment.get_TickCount",
            "System.Environment.get_TickCount64",
            "System.Guid.NewGuid",
        };

        // Math whose results the standard does not pin to the last bit. Sqrt, Abs, Floor and the
        // like are exact and stay allowed.
        private static readonly HashSet<string> MathTypes = new HashSet<string> { "System.Math", "System.MathF", "UnityEngine.Mathf" };

        private static readonly HashSet<string> Transcendental = new HashSet<string>
        {
            "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2",
            "Sinh", "Cosh", "Tanh", "Asinh", "Acosh", "Atanh",
            "Exp", "Pow", "Log", "Log10", "Log2", "Cbrt",
        };

        // Enumerating these depends on insertion history and hash codes. Lookup stays allowed.
        private static readonly HashSet<string> HashCollections = new HashSet<string>
        {
            "System.Collections.Generic.Dictionary`2",
            "System.Collections.Generic.HashSet`1",
        };

        private static readonly HashSet<string> HashCollectionEnumeration = new HashSet<string> { "GetEnumerator", "get_Keys", "get_Values", "CopyTo" };

        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static Report Scan(Assembly assembly) => Scan(assembly.GetTypes());

        public static Report Scan(params Type[] types)
        {
            // Assembly.GetTypes includes nested types, so compiler-made closures and iterators are
            // scanned too. For an explicit list, add the nested types of what was passed.
            var all = new HashSet<Type>();
            foreach (var type in types) AddWithNested(type, all);

            var violations = new List<string>();
            int methods = 0, references = 0;

            foreach (var type in all.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                {
                    if (BannedTypes.Contains(baseType.FullName ?? string.Empty))
                        violations.Add($"{type.FullName} inherits from banned {baseType.FullName}");
                }

                var bodies = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared));
                foreach (var method in bodies)
                {
                    byte[] il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il == null) continue; // abstract, extern, interface
                    methods++;
                    references += ScanBody(type, method, il, violations);
                }
            }

            return new Report(all.Count, methods, references, violations);
        }

        private static void AddWithNested(Type type, HashSet<Type> into)
        {
            if (!into.Add(type)) return;
            foreach (var nested in type.GetNestedTypes(Declared)) AddWithNested(nested, into);
        }

        private static int ScanBody(Type type, MethodBase method, byte[] il, List<string> violations)
        {
            Type[] typeArguments = type.IsGenericType ? type.GetGenericArguments() : null;
            Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            int references = 0;

            int position = 0;
            while (position < il.Length)
            {
                OpCode opCode = ReadOpCode(il, ref position);
                switch (opCode.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        int token = BitConverter.ToInt32(il, position);
                        position += 4;
                        references++;
                        CheckToken(type, method, token, typeArguments, methodArguments, violations);
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    case OperandType.InlineSwitch:
                        int targets = BitConverter.ToInt32(il, position);
                        position += 4 + 4 * targets;
                        break;
                    case OperandType.InlineSig:
                        // calli: a call through a raw function pointer. Its target cannot be known
                        // from here, and C# has no reason to emit it in this package.
                        violations.Add($"{Describe(type, method)} uses calli, which this guard cannot see through");
                        position += 4;
                        break;
                    default: // InlineBrTarget, InlineI, InlineString, ShortInlineR
                        position += 4;
                        break;
                }
            }
            return references;
        }

        private static void CheckToken(Type type, MethodBase method, int token, Type[] typeArguments, Type[] methodArguments, List<string> violations)
        {
            MemberInfo member;
            try
            {
                member = method.Module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (Exception e) when (e is ArgumentException || e is TypeLoadException || e is BadImageFormatException)
            {
                // Fail closed, and keep what went wrong in the report.
                violations.Add($"{Describe(type, method)} has token {token:X8} that could not be resolved: {e.GetType().Name}: {e.Message}");
                return;
            }

            string reason = BannedReason(member);
            if (reason != null) violations.Add($"{Describe(type, method)} references {reason}");
        }

        /// <summary>The banned API this member is, or null if it is allowed.</summary>
        private static string BannedReason(MemberInfo member)
        {
            // A token can name a type outright (typeof, a cast, new of a generic argument).
            Type declaring = member as Type ?? member.DeclaringType;
            if (declaring == null) return null;

            string typeName = TypeKey(declaring);
            string memberName = member is Type ? null : member.Name;
            string full = memberName == null ? typeName : $"{typeName}.{memberName}";

            if (BannedTypes.Contains(typeName)) return full;
            if (memberName == null) return null;
            if (BannedMembers.Contains(full)) return full;
            if (MathTypes.Contains(typeName) && Transcendental.Contains(memberName)) return full;
            if (typeName == "UnityEngine.Object" && memberName.StartsWith("Find", StringComparison.Ordinal)) return full;
            if (HashCollections.Contains(typeName) && HashCollectionEnumeration.Contains(memberName)) return full;
            return null;
        }

        /// <summary>The name without generic arguments: Dictionary`2, however it is instantiated.</summary>
        private static string TypeKey(Type type)
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition) type = type.GetGenericTypeDefinition();
            return type.FullName ?? type.Name;
        }

        private static string Describe(Type type, MethodBase method) => $"{type.FullName}.{method.Name}";

        // IL opcodes are one byte, or two when the first is 0xFE. The tables come from
        // System.Reflection.Emit, so operand sizes are never retyped by hand.
        private static readonly OpCode[] OneByte = new OpCode[256];
        private static readonly OpCode[] TwoByte = new OpCode[256];

        static BannedApiGuard()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is OpCode opCode)) continue;
                ushort value = unchecked((ushort)opCode.Value);
                if (value < 0x100) OneByte[value] = opCode;
                else TwoByte[value & 0xFF] = opCode;
            }
        }

        private static OpCode ReadOpCode(byte[] il, ref int position)
        {
            byte first = il[position++];
            return first == 0xFE ? TwoByte[il[position++]] : OneByte[first];
        }
    }
}
