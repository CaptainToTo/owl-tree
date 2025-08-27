
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    public static class NetworkObjectAnalyzer
    {
        /// <summary>
        /// Use pre-solved id values to assign NetworkObject type ids.
        /// </summary>
        public static void AssignTypeIds(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {
            if (list.Length == 0) return;

            var ordered = list.OrderBy(c => (
                Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_AssignTypeId) ? "0" : "1"
                ) + c.Identifier.ValueText);

            foreach (ClassDeclarationSyntax c in ordered)
            {
                var fullName = Helpers.GetFullName(c.Identifier.ValueText, c);

                var curId = GeneratorState.HasType(fullName)
                    ? GeneratorState.GetTypeData(fullName).typeId 
                    : GeneratorState.NextTypeId();
                
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
                        var collision = GeneratorState.GetTypeData(curId);
                        Diagnostics.DuplicateTypeIds(context, c, curId, collision.name);
                        continue;
                    }

                }

                var usings = Helpers.GetAllUsings(c);

                var ns = Helpers.GetNamespace(c);
                string nsName = "";
                if (ns != null)
                {
                    usings = usings.Add(UsingDirective(ns.Name));
                    nsName = ns.Name.ToString();
                }
                else
                {
                    var fns = Helpers.GetFileNamespace(c);
                    if (fns != null)
                    {
                        usings = usings.Add(UsingDirective(fns.Name));
                        nsName = fns.Name.ToString();
                    }
                }

                var data = new GeneratorState.TypeData
                {
                    typeId = curId,
                    name = fullName,
                    ns = nsName,
                    usings = usings.Select(u => u.Name.ToString()).ToArray(),
                    rpcs = c.Members.OfType<MethodDeclarationSyntax>()
                        .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                        .Select(m => m.Identifier.ValueText).ToArray(),
                    projectId = GeneratorState.CurProjectId
                };

                GeneratorState.AddUsings(usings);
                if (!GeneratorState.HasTypeData(data))
                    GeneratorState.AddTypeData(fullName, data);
                
                if (GeneratorState.NextTypeId() <= curId)
                    GeneratorState.SetTypeId((byte)(curId + 1));
            }
        }

        /// <summary>
        /// Use pre-solved id values to assign RPC ids.
        /// </summary>
        public static void AssignRpcIds(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {
            if (list.Length == 0) return;

            // select all methods, filter for rpcs, and sort rpcs with assigned ids first
            var methods = list.SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>())
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                .OrderBy(m => (
                    Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId) ? "0" : "1"
                    ) + m.Identifier.ValueText);

            foreach (MethodDeclarationSyntax m in methods)
            {
                var fullName = Helpers.GetFullName(m.Identifier.ValueText, m);

                var curId = GeneratorState.HasRpc(fullName)
                    ? GeneratorState.GetRpcData(fullName).id
                    : GeneratorState.NextRpcId();

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

                if (!Helpers.IsEncodable(m.ParameterList, out var err, out var pErr, out var calleeId, out var callerId))
                {
                    if (err == 1)
                        Diagnostics.NonEncodableRpcParam(context, m, pErr);
                    else if (err == 2)
                        Diagnostics.NonClientIdRpcCallee(context, m, pErr);
                    else if (err == 3)
                        Diagnostics.NonClientIdRpcCaller(context, m, pErr);
                    continue;
                }

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

                if (GeneratorState.HasRpcId(fullName, curId))
                {
                    var collision = GeneratorState.GetRpc(curId);
                    Diagnostics.DuplicateRpcIds(context, m, curId, collision);
                    continue;
                }

                Helpers.GetRpcAttrArgs(Helpers.GetAttribute(m.AttributeLists, Helpers.AttrTk_Rpc),
                    out var caller, out var invokeOnCaller, out var useTcp
                );

                if (caller == GeneratorState.RpcPerms.ClientsToAuthority && calleeId != null)
                {
                    Diagnostics.UnnecessaryCalleeIdParam(context, m, calleeId);
                    continue;
                }

                var rpcData = new GeneratorState.RpcData()
                {
                    id = curId,
                    projectId = GeneratorState.CurProjectId,
                    name = m.Identifier.ValueText,
                    fullName = Helpers.GetFullName(m.Identifier.ValueText, m),
                    perms = caller,
                    invokeOnCaller = invokeOnCaller,
                    useTcp = useTcp,
                    parentClass = Helpers.GetParentClassName(m),
                    paramData = CreateParamData(m)
                };

                if (!GeneratorState.HasRpcData(rpcData))
                    GeneratorState.AddRpcData(fullName, rpcData);

                if (GeneratorState.NextRpcId() <= curId)
                    GeneratorState.SetRpcId(curId + 1);
            }
        }

        private static GeneratorState.ParamData[] CreateParamData(MethodDeclarationSyntax m)
        {
            var data = new GeneratorState.ParamData[m.ParameterList.Parameters.Count];

            var paramList = m.ParameterList.Parameters;
            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];
                data[i].name = param.Identifier.ValueText;
                data[i].type = param.Type.ToString();
                data[i].isRpcCallee = Helpers.HasAttribute(param.AttributeLists, Helpers.AttrTk_RpcCalleeId);
                data[i].isRpcCaller = Helpers.HasAttribute(param.AttributeLists, Helpers.AttrTk_RpcCallerId);
            }

            return data;
        }
    }
}