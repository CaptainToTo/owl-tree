using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{

    /// <summary>
    /// Compiler step for OwlTree applications.
    /// </summary>
    [Generator]
    public class OwlTreeGenerator : IIncrementalGenerator
    {
        // public static T Create<T>(T target) where T : NetworkObject
        // {
        //     var proxy = Create<T, RpcInterceptor>();
        //     var interceptor = proxy as RpcInterceptor;
        //     interceptor._instance = target;
        //     return proxy;
        // }

        // // the proxied object
        // private NetworkObject _instance = null;

        // // run RPC encoding & send procedure instead of actual method
        // protected override object Invoke(MethodInfo targetMethod, object[] args)
        // {
        //     // check if the proxied method is a RPC
        //     if (RpcAttribute.IsRpc(targetMethod))
        //     {
        //         bool run = RpcAttribute.OnInvoke(targetMethod, _instance, args);
        //         // run the RPC body on the caller client if marked to do so
        //         if (run)
        //         {
        //             return targetMethod.Invoke(_instance, args);
        //         }
        //         return null;
        //     }
        //     // run a non-RPC method like normal
        //     return targetMethod.Invoke(_instance, args);
        // }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // pre-solve const values
            var constProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is FieldDeclarationSyntax c && Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_RpcIdConst),
                transform: (ctx, _) => (FieldDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var constCompilation = context.CompilationProvider.Combine(constProvider.Collect());

            context.RegisterSourceOutput(constCompilation, SolveConstValues);

            // pre-solve enum values
            var enumProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax e && Helpers.HasAttribute(e.AttributeLists, Helpers.AttrTk_RpcIdEnum),
                transform: static (ctx, _) => (EnumDeclarationSyntax)ctx.Node
            ).Where(m => m is not null);

            var enumCompilation = context.CompilationProvider.Combine(enumProvider.Collect());

            context.RegisterSourceOutput(enumCompilation, SolveEnumValues);

            // filter for network objects
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>  
                {
                    if (node is ClassDeclarationSyntax cls)
                    {
                        return cls.BaseList?.Types.Any(t => t.Type is IdentifierNameSyntax idn && idn.Identifier.ValueText == Helpers.Tk_NetworkObject) ?? false;
                    }
                    return false;
                },
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(m => m is not null);

            var compilation = context.CompilationProvider.Combine(provider.Collect());

            context.RegisterSourceOutput(compilation, Execute);
        }

        private void SolveConstValues(SourceProductionContext context, (Compilation Left, ImmutableArray<FieldDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            GeneratorState.ClearConsts();

            // add built in first rpc id const
            GeneratorState.AddConst(Helpers.Tk_FirstId, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithClass, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithNamespace, (int)Helpers.FIRST_RPC_ID);

            var names = new List<string>();
            foreach (var field in list)
            {

                if (Helpers.IsConst(field) && Helpers.IsInt(field))
                {
                    names.Clear();
                    Helpers.GetAllNames(Helpers.GetFieldName(field), field, names);
                    var value = Helpers.GetInt(field);
                    foreach (var name in names)
                    {
                        GeneratorState.AddConst(name, value);
                    }
                }
                else
                {
                    Diagnostics.BadRpcIdConst(context, field);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "const-out.txt", GeneratorState.GetConstsString());
        }

        private void SolveEnumValues(SourceProductionContext context, (Compilation Left, ImmutableArray<EnumDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            GeneratorState.ClearEnums();
            var names = new List<string>();
            foreach (var e in list)
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

        private void Execute(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            // filter for rpcs, and sort rpcs with assigned ids first
            var methods = list.SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>())
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                .OrderBy(m => (
                    Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId) ? "0" : "1"
                    ) + m.Identifier.ValueText);

            uint curId = Helpers.FIRST_RPC_ID;
            uint _curId = Helpers.FIRST_RPC_ID;
            GeneratorState.ClearRpcIds();

            foreach (MethodDeclarationSyntax m in methods)
            {
                bool isVirtual = m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.VirtualKeyword));

                if (!isVirtual)
                {
                    Diagnostics.NonVirtualRpc(context, m);
                    continue;
                }

                bool isProcedure = m.ReturnType.GetFirstToken().IsKind(SyntaxKind.VoidKeyword);

                if (!isProcedure)
                {
                    Diagnostics.NonVoidRpc(context, m);
                    continue;
                }

                curId = _curId;
                var attr = Helpers.GetAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId);
                if (attr != null)
                {
                    var assignedId = Helpers.GetAssignedRpcId(attr);
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