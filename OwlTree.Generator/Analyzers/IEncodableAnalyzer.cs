
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    public static class IEncodableAnalyzer
    {
        // all encodable types provided by owl tree
        public static void AddBuiltIns()
        {
            GeneratorState.AddEncodable(Helpers.Tk_RpcId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_RpcId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_ClientId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_ClientId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_AppId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_AppId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkId, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Tick, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_Tick, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_NetworkBitSet, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkBitSet, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkDict, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkDict, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkList, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkList, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkString, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkString, true, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec2, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec2, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec3, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec3, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec4, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec4, false, 0);
        }

        // all base encodable types
        public static void AddPrimitives()
        {
            GeneratorState.AddEncodable(Helpers.Tk_Byte, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Bool, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_UShort, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Short, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_UInt, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Int, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_UInt32, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Int32, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_Float, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_ULong, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Long, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_UInt64, false, 0);
            GeneratorState.AddEncodable(Helpers.Tk_Int64, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_Double, false, 0);

            GeneratorState.AddEncodable(Helpers.Tk_String, false, 0);
        }

        /// <summary>
        /// Caches all encodable types, and performs analyzer checks on them.
        /// </summary>
        public static void CacheEncodables(SourceProductionContext context, (Compilation Left, ImmutableArray<TypeDeclarationSyntax> Right) tuple)
        {
            try
            {
                var (compilation, list) = tuple;

                CacheFinder.GetCache(compilation);

                if (!GeneratorState.HasEncodable(Helpers.Tk_Byte))
                    AddPrimitives();
                if (!GeneratorState.HasEncodable(Helpers.Tk_RpcId))
                    AddBuiltIns();

                GeneratorState.RemoveEncodables(GeneratorState.CurProjectId);

                if (list.Length == 0)
                    return;

                var names = new List<string>();

                foreach (var encodable in list)
                {
                    names.Clear();
                    Helpers.GetAllNames(encodable.Identifier.ValueText, encodable, names);
                    bool isVariable = Helpers.InheritsFrom(encodable, Helpers.Tk_IVariable);

                    if (!GeneratorState.HasEncodable(names.Last(), isVariable, GeneratorState.CurProjectId))
                    {
                        foreach (var name in names)
                            GeneratorState.AddEncodable(name, isVariable, GeneratorState.CurProjectId);
                        var ns = Helpers.GetNamespace(encodable);
                        if (ns != null)
                            GeneratorState.AddUsing(ns.Name.ToString());
                        else
                        {
                            var fns = Helpers.GetFileNamespace(encodable);
                            if (fns != null)
                                GeneratorState.AddUsing(fns.Name.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Diagnostics.GeneratorException(e);
            }
        }
    }
}