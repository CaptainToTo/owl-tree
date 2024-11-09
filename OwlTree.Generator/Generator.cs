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
        
        static Dictionary<string, int> consts = new();

        public static bool TryGetConst(string constName, out int value)
        {
            return consts.TryGetValue(constName, out value);
        }

        static string GetConsts()
        {
            var str = new StringBuilder();
            foreach (var c in consts)
            {
                str.Append(c.Key).Append(" : ").Append(c.Value).Append("\n");
            }
            return str.ToString();
        }

        static Dictionary<string, int> enums = new();

        public static bool TryGetEnum(string name, out int enumValue)
        {
            if (enums.ContainsKey(name))
            {
                enumValue = enums[name];
                return true;
            }
            enumValue = 0;
            return false;
        }

        static string GetEnums()
        {
            var str = new StringBuilder();
            foreach (var e in enums)
            {
                str.Append(e.Key).Append(" : ").Append(e.Value).Append("\n");
            }
            return str.ToString();
        }

        public static bool TryGetConstOrEnum(MemberAccessExpressionSyntax access, out int value)
        {
            var name = Helpers.GetAccessorString(access);
            if (TryGetConst(name, out value))
            {
                return true;
            }
            if (TryGetEnum(name, out value))
            {
                return true;
            }
            return false;
        }

        static Dictionary<string, uint> rpcIds = new();


        // =====================================================
        
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
                        // return true;
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

            consts.Clear();

            // add built in first rpc id const
            consts.Add(Helpers.Tk_FirstId, (int)Helpers.FIRST_RPC_ID);
            consts.Add(Helpers.Tk_FirstIdWithClass, (int)Helpers.FIRST_RPC_ID);
            consts.Add(Helpers.Tk_FirstIdWithNamespace, (int)Helpers.FIRST_RPC_ID);

            var names = new List<string>();
            foreach (var field in list)
            {

                if (Helpers.IsConst(field) && Helpers.IsInt(field))
                {
                    names.Clear();
                    Helpers.TryGetInt(field, names, out var value);
                    foreach (var name in names)
                    {
                        consts.Add(name, value);
                    }
                }
                else
                {
                    var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "OT003",
                                "RPC Id Consts must be consts",
                                "RPC const '{0}' must be a const integer. {1}",
                                "Syntax",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            field.GetLocation(),
                            Helpers.GetFieldName(field), field.Declaration.Type.ToString());

                        context.ReportDiagnostic(diagnostic);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "const-out.txt", GetConsts());
        }

        private void SolveEnumValues(SourceProductionContext context, (Compilation Left, ImmutableArray<EnumDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            enums.Clear();
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
                                if (consts.ContainsKey(identifier.Identifier.ValueText))
                                    i = consts[identifier.Identifier.ValueText];
                            break;

                            case MemberAccessExpressionSyntax access:
                                var name = Helpers.GetAccessorString(access);
                                if (consts.ContainsKey(name))
                                    i = consts[name];
                            break;
                        }
                    }

                    foreach (var n in names)
                    {
                        enums[n + "." + m.Identifier.ValueText] = i;
                    }

                    i++;
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "enum-out.txt", GetEnums());
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

            var output = "Rpcs:\n";

            uint curId = Helpers.FIRST_RPC_ID;
            uint _curId = Helpers.FIRST_RPC_ID;

            foreach (MethodDeclarationSyntax m in methods)
            {
                bool isVirtual = m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.VirtualKeyword));

                if (!isVirtual)
                {
                    var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "OT001",
                                "RPC Must Be Virtual",
                                "RPC method '{0}' must be virtual for RPC protocol to be generated properly.",
                                "Syntax",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            m.GetLocation(),
                            m.Identifier.ValueText);

                        context.ReportDiagnostic(diagnostic);
                    continue;
                }

                bool isProcedure = m.ReturnType.GetFirstToken().IsKind(SyntaxKind.VoidKeyword);

                if (!isProcedure)
                {
                    var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "OT002",
                                "RPC Must Return Void",
                                "RPC method '{0}' cannot have a return type.",
                                "Syntax",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            m.GetLocation(),
                            m.Identifier.ValueText);

                        context.ReportDiagnostic(diagnostic);
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
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "OT004",
                                "Invalid Assign RPC Id Value",
                                "RPC method '{0}' can only have its RPC id assigned with a literal integer, a constant with the '{1}' attribute, or an enum value with the '{2}' attribute.",
                                "Syntax",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            attr.GetLocation(),
                            m.Identifier.ValueText, Helpers.AttrTk_RpcIdConst, Helpers.AttrTk_RpcIdEnum);

                        context.ReportDiagnostic(diagnostic);
                        continue;
                    }
                }

                if (rpcIds.ContainsValue(curId))
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "OT005",
                            "Duplicate RPC Ids",
                            "RPC method '{0}' cannot have the same id as another RPC.",
                            "Syntax",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        m.GetLocation(),
                        m.Identifier.ValueText, Helpers.AttrTk_RpcIdConst, Helpers.AttrTk_RpcIdEnum);

                    context.ReportDiagnostic(diagnostic);
                    continue;
                }

                rpcIds.Add(m.Identifier.ValueText, curId);

                output += m.Identifier.ValueText + " " + curId + "\n";
                if (_curId <= curId)
                    _curId = curId + 1;
            }

            File.WriteAllText(EnvConsts.ProjectPath + "rpc-out.txt", output);
        }
    }
}