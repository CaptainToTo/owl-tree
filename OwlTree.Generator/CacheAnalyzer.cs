

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{
    /// <summary>
    /// Fills the generator state cache. Performs analyzer checks on project before source generation.
    /// </summary>
    public static class CacheAnalyzer
    {
        // IEncodables ==========================

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

        public static void AddPrimitives()
        {
            GeneratorState.AddEncodable(Helpers.Tk_Byte, false);

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

        public static void CacheEncodables(SourceProductionContext context, (Compilation Left, ImmutableArray<TypeDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

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

        // ======================================

        // Consts and Enums =====================

        public static void SolveConstAndEnumValues(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            if (list.Length == 0)
                return;
            
            var registry = list[0];

            for (int i = 1; i < list.Length; i++)
                Diagnostics.MultipleIdRegistries(context, list[i]);

            if (!Helpers.IsStatic(registry))
            {
                Diagnostics.NonStaticRegistry(context, registry);
                return;
            }

            var fields = registry.Members.OfType<FieldDeclarationSyntax>();
            SolveConstValues(context, fields);

            var enums = registry.Members.OfType<EnumDeclarationSyntax>();
            SolveEnumValues(context, enums);
        }

        private static void SolveConstValues(SourceProductionContext context, IEnumerable<FieldDeclarationSyntax> fields)
        {
            // add built in first rpc id const
            GeneratorState.AddConst(Helpers.Tk_FirstId, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithClass, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithNamespace, (int)Helpers.FIRST_RPC_ID);

            var names = new List<string>();
            foreach (var field in fields)
            {

                if (Helpers.IsConst(field) && Helpers.IsInt(field))
                {
                    names.Clear();
                    Helpers.GetAllNames(Helpers.GetFieldName(field), field, names);
                    var value = Helpers.GetInt(field);
                    foreach (var name in names)
                        GeneratorState.AddConst(name, value);
                }
                else
                {
                    Diagnostics.BadRpcIdConst(context, field);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "const-out.txt", GeneratorState.GetConstsString());
        }

        private static void SolveEnumValues(SourceProductionContext context, IEnumerable<EnumDeclarationSyntax> enums)
        {

            GeneratorState.ClearEnums();
            var names = new List<string>();
            foreach (var e in enums)
            {
                names.Clear();
                Helpers.GetAllNames(e.Identifier.ValueText, e, names);

                int i = 0;
                foreach (var m in e.Members)
                {
                    var val = m.EqualsValue;
                    if (val != null)
                    {
                        switch (val.Value)
                        {
                            case LiteralExpressionSyntax literal:
                                if (literal != null && literal.IsKind(SyntaxKind.NumericLiteralExpression))
                                    i = (int)literal.Token.Value;
                            break;

                            case IdentifierNameSyntax identifier:
                                if (GeneratorState.HasConst(identifier.Identifier.ValueText))
                                    i = GeneratorState.GetConst(identifier.Identifier.ValueText);
                            break;

                            case MemberAccessExpressionSyntax access:
                                var name = Helpers.GetAccessorString(access);
                                if (GeneratorState.HasConst(name))
                                    i = GeneratorState.GetConst(name);
                            break;
                        }
                    }

                    foreach (var n in names)
                    {
                        GeneratorState.AddEnum(n + "." + m.Identifier.ValueText, i);
                    }

                    i++;
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "enum-out.txt", GeneratorState.GetEnumsString());
        }

        // ======================================

        // Network Objects and RPCs =============

        public static void AssignTypeIds(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {
            var ordered = list.OrderBy(c => (
                Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_AssignTypeId) ? "0" : "1"
                ) + c.Identifier.ValueText);
            
            byte curId = Helpers.FIRST_NETWORK_TYPE_ID;
            byte _curId = Helpers.FIRST_NETWORK_TYPE_ID;

            foreach (ClassDeclarationSyntax c in ordered)
            {
                curId = _curId;
                var attr = Helpers.GetAttribute(c.AttributeLists, Helpers.AttrTk_AssignTypeId);
                if (attr != null)
                {
                    var assignedId = Helpers.GetAssignedId(attr);
                    if (assignedId != -1)
                    {
                        curId = (byte)assignedId;
                    }
                    else
                    {
                        Diagnostics.BadTypeIdAssignment(context, c, attr);
                        continue;
                    }

                    if (GeneratorState.HasTypeId(curId))
                    {
                        Diagnostics.DuplicateTypeIds(context, c, curId);
                        continue;
                    }

                    GeneratorState.AddTypeId(Helpers.GetFullName(c.Identifier.ValueText, c), curId);

                    if (_curId <= curId)
                        _curId = (byte)(curId + 1);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "types-out.txt", GeneratorState.GetTypeIdsString());
        }

        public static void AssignRpcIds(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {

            // select all methods, filter for rpcs, and sort rpcs with assigned ids first
            var methods = list.SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>())
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                .OrderBy(m => (
                    Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId) ? "0" : "1"
                    ) + m.Identifier.ValueText);

            uint curId = Helpers.FIRST_RPC_ID;
            uint _curId = Helpers.FIRST_RPC_ID;

            foreach (MethodDeclarationSyntax m in methods)
            {
                if (!Helpers.IsVirtual(m))
                {
                    Diagnostics.NonVirtualRpc(context, m);
                    continue;
                }

                if (!Helpers.IsProcedure(m))
                {
                    Diagnostics.NonVoidRpc(context, m);
                    continue;
                }

                if (Helpers.IsStatic(m))
                {
                    Diagnostics.StaticRpc(context, m);
                    continue;
                }

                if (!Helpers.IsEncodable(m.ParameterList, out var err))
                {
                    Diagnostics.NonEncodableRpcParam(context, m, err);
                    continue;
                }

                curId = _curId;
                var attr = Helpers.GetAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId);
                if (attr != null)
                {
                    var assignedId = Helpers.GetAssignedId(attr);
                    if (assignedId != -1)
                    {
                        curId = (uint)assignedId;
                    }
                    else
                    {
                        Diagnostics.BadRpcIdAssignment(context, m, attr);
                        continue;
                    }
                }

                if (GeneratorState.HasRpcId(curId))
                {
                    Diagnostics.DuplicateRpcIds(context, m, curId);
                    continue;
                }

                GeneratorState.AddRpcId(Helpers.GetFullName(m.Identifier.ValueText, m), curId);

                if (_curId <= curId)
                    _curId = curId + 1;
            }

            File.WriteAllText(EnvConsts.ProjectPath + "rpc-out.txt", GeneratorState.GetRpcIdsString());
        }
    }
}