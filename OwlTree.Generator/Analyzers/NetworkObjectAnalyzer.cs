
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{

    public class InherenceTreeNode
    {
        public readonly string name;
        public readonly string baseClass;
        public readonly ClassDeclarationSyntax syntax;
        private List<string> _children = null;

        public InherenceTreeNode(string name, string baseClass, ClassDeclarationSyntax syntax)
        {
            this.name = name;
            this.baseClass = baseClass;
            this.syntax = syntax;
        }

        public void AddChild(string child)
        {
            if (_children == null)
                _children = new List<string>();
            _children.Add(child);
        }

        public IEnumerable<string> Children() => _children ?? Enumerable.Empty<string>();
    }

    public static class NetworkObjectAnalyzer
    {
        public static IEnumerable<InherenceTreeNode> TraverseTree(Dictionary<string, InherenceTreeNode> tree)
        {
            var root = tree[Helpers.Tk_FullNetworkObject];

            var q = new Queue<InherenceTreeNode>();
            foreach (var c in root.Children().OrderBy(c => c))
                q.Enqueue(tree[c]);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                yield return cur;

                foreach (var c in cur.Children().OrderBy(c => c))
                    q.Enqueue(tree[c]);
            }
        }

        public static Dictionary<string, InherenceTreeNode> BuildInheritanceTree(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {
            var dict = new Dictionary<string, InherenceTreeNode>();

            var q = new Queue<(string name, string super, ClassDeclarationSyntax syntax)>();
            q.Enqueue((Helpers.Tk_FullNetworkObject, null, null));

            var workingList = new List<ClassDeclarationSyntax>(list);

            while (q.Count > 0)
            {
                while (q.Count > 0)
                {
                    var pair = q.Dequeue();
                    dict.Add(pair.name, new InherenceTreeNode(pair.name, pair.super, pair.syntax));
                    if (!string.IsNullOrEmpty(pair.super))
                        dict[pair.super].AddChild(pair.name);
                }

                for (int i = 0; i < workingList.Count; i++)
                {
                    var c = workingList[i];
                    var name = Helpers.GetFullName(c.Identifier.ValueText, c);
                    var supers = Helpers.GetPossibleFullTypeNames(c).Where(s => dict.ContainsKey(s));

                    if (supers != null && supers.Count() > 0)
                    {
                        if (Helpers.IsGenericType(c))
                        {
                            Diagnostics.GenericNetworkObjectType(context, c);
                            continue;
                        }

                        var super = supers.First();
                        q.Enqueue((name, super, c));
                        workingList.RemoveAt(i);
                        i--;
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Use pre-solved id values to assign NetworkObject type ids.
        /// </summary>
        public static void AssignTypeIds(SourceProductionContext context, Dictionary<string, InherenceTreeNode> tree)
        {
            GeneratorState.SweepTypeIds();
            var recycledIds = GeneratorState.RemoveTypes(GeneratorState.CurProjectId);
            int curRecycled = 0;

            if (tree.Count == 1) return; // if no types, will only contain OwlTree.NetworkObject

            foreach (InherenceTreeNode c in TraverseTree(tree))
            {
                byte curId = 0;
                bool recycled = false;
                if (curRecycled < recycledIds.Length)
                {
                    curId = recycledIds[curRecycled];
                    curRecycled++;
                    recycled = true;
                }
                else if (GeneratorState.HasType(c.name))
                {
                    curId = GeneratorState.GetTypeData(c.name).typeId;
                }
                else
                {
                    curId = GeneratorState.NextTypeId();
                }

                if (GeneratorState.HasTypeId(curId))
                {
                    var collision = GeneratorState.GetTypeData(curId);
                    Diagnostics.DuplicateTypeIds(context, c.syntax, curId, collision.name);
                    continue;
                }

                var usings = Helpers.GetAllUsings(c.syntax);

                var ns = Helpers.GetNamespace(c.syntax);
                string nsName = "";
                if (ns != null)
                {
                    usings = usings.Add(UsingDirective(ns.Name));
                    nsName = ns.Name.ToString();
                }
                else
                {
                    var fns = Helpers.GetFileNamespace(c.syntax);
                    if (fns != null)
                    {
                        usings = usings.Add(UsingDirective(fns.Name));
                        nsName = fns.Name.ToString();
                    }
                }

                string[] inherited = null;
                if (c.baseClass != Helpers.Tk_FullNetworkObject)
                {
                    var baseData = GeneratorState.GetTypeData(c.baseClass);
                    inherited = baseData.rpcs.Select(r => baseData.name + "." + r).Concat(baseData.inheritedRpcs).ToArray();
                }
                else
                {
                    inherited = new string[0];
                }

                var data = new GeneratorState.TypeData
                {
                    typeId = curId,
                    name = c.name,
                    baseClass = c.baseClass,
                    ns = nsName,
                    usings = usings.Select(u => u.Name.ToString()).ToArray(),
                    rpcs = c.syntax.Members.OfType<MethodDeclarationSyntax>()
                        .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                        .Select(m => m.Identifier.ValueText).ToArray(),
                    inheritedRpcs = inherited,
                    projectId = GeneratorState.CurProjectId
                };

                GeneratorState.AddUsings(usings);
                if (!GeneratorState.HasTypeData(data))
                    GeneratorState.AddTypeData(c.name, data);

                if (!recycled)
                    GeneratorState.IncrementTypeId();
            }


        }

        /// <summary>
        /// Use pre-solved id values to assign RPC ids.
        /// </summary>
        public static void AssignRpcIds(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> list)
        {
            GeneratorState.SweepRpcIds();
            var recycledIds = GeneratorState.RemoveRpcs(GeneratorState.CurProjectId);
            int curRecycled = 0;

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

                uint curId = 0;
                bool recycled = false;
                if (curRecycled < recycledIds.Length)
                {
                    curId = recycledIds[curRecycled];
                    curRecycled++;
                    recycled = true;
                }
                else if (GeneratorState.HasRpc(fullName))
                {
                    curId = GeneratorState.GetRpcData(fullName).id;
                }
                else
                {
                    curId = GeneratorState.NextRpcId();
                }

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

                if (!Helpers.ValidateParams(m.ParameterList, out var err, out var pErr, out var calleeId, out var callerId))
                {
                    if (err == 1)
                        Diagnostics.NonEncodableRpcParam(context, m, pErr);
                    else if (err == 2)
                        Diagnostics.NonClientIdRpcCallee(context, m, pErr);
                    else if (err == 3)
                        Diagnostics.NonClientIdRpcCaller(context, m, pErr);
                    else if (err == 4)
                        Diagnostics.NonDefaultRpcCaller(context, m, pErr);
                    continue;
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
                    fullName = fullName,
                    perms = caller,
                    invokeOnCaller = invokeOnCaller,
                    useTcp = useTcp,
                    parentClass = Helpers.GetParentClassName(m),
                    paramData = CreateParamData(m)
                };

                if (!GeneratorState.HasRpcData(rpcData))
                    GeneratorState.AddRpcData(fullName, rpcData);

                if (!recycled)
                    GeneratorState.IncrementRpcId();
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