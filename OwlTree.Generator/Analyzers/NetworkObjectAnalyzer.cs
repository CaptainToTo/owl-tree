
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{
    public static class NetworkObjectAnalyzer
    {
        /// <summary>
        /// Use pre-solved id values to assign NetworkObject type ids.
        /// </summary>
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

                }

                GeneratorState.AddTypeId(Helpers.GetFullName(c.Identifier.ValueText, c), curId);

                if (_curId <= curId)
                    _curId = (byte)(curId + 1);
            }

            File.WriteAllText(EnvConsts.ProjectPath + "types-out.txt", GeneratorState.GetTypeIdsString());
        }

        /// <summary>
        /// Use pre-solved id values to assign RPC ids.
        /// </summary>
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