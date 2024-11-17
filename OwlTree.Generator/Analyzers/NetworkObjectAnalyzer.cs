
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
                var fullName = Helpers.GetFullName(c.Identifier.ValueText, c);
                if (GeneratorState.HasType(fullName))
                    continue;

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
                        var collision = GeneratorState.GetType(curId);
                        Diagnostics.DuplicateTypeIds(context, c, curId, collision);
                        continue;
                    }

                }

                GeneratorState.AddTypeId(fullName, curId);

                GeneratorState.AddUsings(Helpers.GetAllUsings(c));

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
                var fullName = Helpers.GetFullName(m.Identifier.ValueText, m);
                if (GeneratorState.HasRpc(fullName))
                    continue;

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

                if (!Helpers.IsEncodable(m.ParameterList, out var err, out var pErr))
                {
                    if (err == 1)
                        Diagnostics.NonEncodableRpcParam(context, m, pErr);
                    else if (err == 2)
                        Diagnostics.NonClientIdRpcCallee(context, m, pErr);
                    else if (err == 3)
                        Diagnostics.NonClientIdRpcCaller(context, m, pErr);
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
                    var collision = GeneratorState.GetRpc(curId);
                    Diagnostics.DuplicateRpcIds(context, m, curId, collision);
                    continue;
                }

                Helpers.GetRpcAttrArgs(Helpers.GetAttribute(m.AttributeLists, Helpers.AttrTk_Rpc),
                    out var caller, out var invokeOnCaller, out var useTcp
                );

                var rpcData = new GeneratorState.RpcData()
                {
                    id = curId,
                    name = m.Identifier.ValueText,
                    caller = caller,
                    invokeOnCaller = invokeOnCaller,
                    useTcp = useTcp,
                    parentClass = Helpers.GetParentClassName(m),
                    paramData = CreateParamData(m)
                };

                GeneratorState.AddRpcData(fullName, rpcData);

                if (_curId <= curId)
                    _curId = curId + 1;
            }

            File.WriteAllText(EnvConsts.ProjectPath + "rpc-out.txt", GeneratorState.GetRpcIdsString());
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
                data[i].isRpcCallee = Helpers.HasAttribute(param.AttributeLists, Helpers.AttrTk_RpcCallee);
                data[i].isRpcCaller = Helpers.HasAttribute(param.AttributeLists, Helpers.AttrTk_RpcCaller);
            }

            return data;
        }
    }
}