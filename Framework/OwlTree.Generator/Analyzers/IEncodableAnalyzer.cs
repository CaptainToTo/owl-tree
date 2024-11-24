
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{
    public static class IEncodableAnalyzer
    {
        // all encodable types provided by owl tree
        public static void AddBuiltIns()
        {
            GeneratorState.AddEncodable(Helpers.Tk_RpcId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_RpcId, false);
            GeneratorState.AddEncodable(Helpers.Tk_ClientId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_ClientId, false);
            GeneratorState.AddEncodable(Helpers.Tk_AppId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_AppId, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkId, false);

            GeneratorState.AddEncodable(Helpers.Tk_NetworkBitSet, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkBitSet, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkDict, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkDict, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkList, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkList, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkString, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkString, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec2, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec2, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec3, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec3, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec4, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec4, false);
        }

        // all base encodable types
        public static void AddPrimitives()
        {
            GeneratorState.AddEncodable(Helpers.Tk_Byte, false);
            GeneratorState.AddEncodable(Helpers.Tk_Bool, false);

            GeneratorState.AddEncodable(Helpers.Tk_UShort, false);
            GeneratorState.AddEncodable(Helpers.Tk_Short, false);

            GeneratorState.AddEncodable(Helpers.Tk_UInt, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int, false);
            GeneratorState.AddEncodable(Helpers.Tk_UInt32, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int32, false);

            GeneratorState.AddEncodable(Helpers.Tk_Float, false);

            GeneratorState.AddEncodable(Helpers.Tk_ULong, false);
            GeneratorState.AddEncodable(Helpers.Tk_Long, false);
            GeneratorState.AddEncodable(Helpers.Tk_UInt64, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int64, false);

            GeneratorState.AddEncodable(Helpers.Tk_Double, false);

            GeneratorState.AddEncodable(Helpers.Tk_String, false);
        }

        /// <summary>
        /// Caches all encodable types, and performs analyzer checks on them.
        /// </summary>
        public static void CacheEncodables(SourceProductionContext context, (Compilation Left, ImmutableArray<TypeDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            GeneratorState.ClearEncodables();
            AddPrimitives();
            AddBuiltIns();

            if (list.Length == 0)
                return;
            
            var names = new List<string>();

            foreach (var encodable in list)
            {
                names.Clear();
                Helpers.GetAllNames(encodable.Identifier.ValueText, encodable, names);
                bool isVariable = Helpers.InheritsFrom(encodable, Helpers.Tk_IVariable);

                if (!GeneratorState.HasEncodable(names.Last()))
                {
                    foreach (var name in names)
                        GeneratorState.AddEncodable(name, isVariable);
                }
            }
        }
    }
}