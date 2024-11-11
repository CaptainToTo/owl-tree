using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{

    /// <summary>
    /// Compiler step for OwlTree applications.
    /// </summary>
    [Generator]
    public class OwlTreeGenerator : IIncrementalGenerator
    {        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // reset generator state
            GeneratorState.ClearEncodables();
            GeneratorState.ClearConsts();
            GeneratorState.ClearEnums();
            GeneratorState.ClearTypeIds();
            GeneratorState.ClearRpcIds();
            // refill encodables with built in encodable types
            CacheAnalyzer.AddPrimitives();
            CacheAnalyzer.AddBuiltIns();

            // cache IEncodable types
            var encodableProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_IEncodable),
                transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var encodableCompilation = context.CompilationProvider.Combine(encodableProvider.Collect());

            context.RegisterSourceOutput(encodableCompilation, CacheAnalyzer.CacheEncodables);
            File.WriteAllText(EnvConsts.ProjectPath + "encodable-out.txt", GeneratorState.GetEncodablesString());

            // pre-solve const and enum values
            var registryProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_IdRegistry),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var registryCompilation = context.CompilationProvider.Combine(registryProvider.Collect());

            context.RegisterSourceOutput(registryCompilation, CacheAnalyzer.SolveConstAndEnumValues);

            // generate network object proxies
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_NetworkObject),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(m => m is not null);

            var compilation = context.CompilationProvider.Combine(provider.Collect());

            context.RegisterSourceOutput(compilation, GenerateProxies);
        }

        private void GenerateProxies(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            CacheAnalyzer.AssignTypeIds(context, list);
            CacheAnalyzer.AssignRpcIds(context, list);
            
            foreach (var c in list)
            {
                var proxy = CreateProxy(c);
                File.WriteAllText(EnvConsts.ProjectPath + c.Identifier.ValueText + ".txt", proxy.ToString());
            }
        }

        public CompilationUnitSyntax CreateProxy(ClassDeclarationSyntax c)
        {
            return CompilationUnit()
            .WithUsings(
                SingletonList<UsingDirectiveSyntax>(
                    UsingDirective(
                        IdentifierName("OwlTree"))))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    ClassDeclaration(c.Identifier.ValueText + Helpers.Tk_ProxySuffix)
                    .WithBaseList(
                        BaseList(
                            SingletonSeparatedList<BaseTypeSyntax>(
                                SimpleBaseType(
                                    IdentifierName(c.Identifier.ValueText)
                                ))))
                    .WithMembers(
                        CreateRpcProxies(c))))
            .NormalizeWhitespace();
        }

        static List<MethodDeclarationSyntax> proxyBuilderStage = new();

        private SyntaxList<MemberDeclarationSyntax> CreateRpcProxies(ClassDeclarationSyntax c)
        {

            var methods = c.Members.OfType<MethodDeclarationSyntax>()
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc));

            proxyBuilderStage.Clear();

            foreach (var m in methods)
            {
                if (!GeneratorState.TryGetRpcId(Helpers.GetFullName(m.Identifier.ValueText, m), out var id))
                {
                    continue;
                }

                var proxy = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier(m.Identifier.ValueText))
                    .WithModifiers(
                        TokenList(
                            new[]{
                                Token(Helpers.GetMethodScope(m)),
                                Token(SyntaxKind.OverrideKeyword)
                            }))
                    .WithParameterList(m.ParameterList)
                    .WithBody(m.ParameterList.Parameters.Count == 0 ? CreateProxyBodyNoParams(m, id) : CreateProxyBody(m, id));
                
                proxyBuilderStage.Add(proxy);
            }

            var proxyList = List<MemberDeclarationSyntax>(proxyBuilderStage);

            return proxyList;
        }

        /*
        creates:
        object[] args = new[]{a, b, c, d};
        bool run = RpcAttribute.OnInvoke(id, this, args);

        if (run)
        {
            base.MyRpc(a, b, c, d);
        }
        return;
        */
        private BlockSyntax CreateProxyBody(MethodDeclarationSyntax m, uint id)
        {
            return Block(
                // object[] args = new[]{a, b, c, d};
                LocalDeclarationStatement(
                    VariableDeclaration(
                        ArrayType(
                            PredefinedType(
                                Token(SyntaxKind.ObjectKeyword)))
                        .WithRankSpecifiers(
                            SingletonList<ArrayRankSpecifierSyntax>(
                                ArrayRankSpecifier(
                                    SingletonSeparatedList<ExpressionSyntax>(
                                        OmittedArraySizeExpression())))))
                    .WithVariables(
                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                            VariableDeclarator(
                                Identifier("args"))
                            .WithInitializer(
                                EqualsValueClause(
                                    ImplicitArrayCreationExpression(
                                        InitializerExpression(
                                            SyntaxKind.ArrayInitializerExpression,
                                            SeparatedList<ExpressionSyntax>(
                                                CreateArgArray(m) )))))))),
                // bool run = RpcAttribute.OnInvoke(id, this, args);
                LocalDeclarationStatement(
                    VariableDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.BoolKeyword)))
                    .WithVariables(
                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                            VariableDeclarator(
                                Identifier("run"))
                            .WithInitializer(
                                EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("RpcAttribute"),
                                            IdentifierName("OnInvoke")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SeparatedList<ArgumentSyntax>(
                                                new SyntaxNodeOrToken[]{
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.NumericLiteralExpression,
                                                            Literal(id))),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        ThisExpression()),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        IdentifierName("args"))})))))))),
                //  if (run)
                //      base.MyRpc(a, b, c, d);
                IfStatement(
                    IdentifierName("run"),
                    Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        BaseExpression(),
                                        IdentifierName(m.Identifier.ValueText)))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList<ArgumentSyntax>(
                                            CreateParamArray(m) ))))))),
                ReturnStatement());
        }

        private SyntaxNodeOrToken[] CreateArgArray(MethodDeclarationSyntax m)
        {
            var arr = new SyntaxNodeOrToken[(m.ParameterList.Parameters.Count * 2) - 1];
            
            for (int i = 0; i < arr.Length; i++)
            {
                if (i % 2 == 0)
                {
                    if (Helpers.HasAttribute(m.ParameterList.Parameters[i / 2].AttributeLists, Helpers.AttrTk_RpcCaller))
                    {
                        arr[i] = MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.Tk_Connection),
                                    IdentifierName(Helpers.Tk_LocalId));
                    }
                    else
                    {
                        arr[i] = IdentifierName(m.ParameterList.Parameters[i / 2].Identifier.ValueText);
                    }
                }
                else
                    arr[i] = Token(SyntaxKind.CommaToken);
            }

            return arr;
        }

        private SyntaxNodeOrToken[] CreateParamArray(MethodDeclarationSyntax m)
        {
            var arr = new SyntaxNodeOrToken[(m.ParameterList.Parameters.Count * 2) - 1];
            
            for (int i = 0; i < arr.Length; i++)
            {
                if (i % 2 == 0)
                    if (Helpers.HasAttribute(m.ParameterList.Parameters[i / 2].AttributeLists, Helpers.AttrTk_RpcCaller))
                    {
                        arr[i] = Argument(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.Tk_Connection),
                                    IdentifierName(Helpers.Tk_LocalId)));
                    }
                    else
                    {
                        arr[i] = Argument(IdentifierName(m.ParameterList.Parameters[i / 2].Identifier.ValueText));
                    }
                else
                    arr[i] = Token(SyntaxKind.CommaToken);
            }

            return arr;
        }

        /*
        creates:
        object[] args = new object[0];
        bool run = RpcAttribute.OnInvoke(id, this, args);

        if (run)
        {
            base.MyRpc();
        }
        return;
        */
        private BlockSyntax CreateProxyBodyNoParams(MethodDeclarationSyntax m, uint id)
        {
            return Block(
                // object[] args = new object[0];
                LocalDeclarationStatement(
                    VariableDeclaration(
                        ArrayType(
                            PredefinedType(
                                Token(SyntaxKind.ObjectKeyword)))
                        .WithRankSpecifiers(
                            SingletonList<ArrayRankSpecifierSyntax>(
                                ArrayRankSpecifier(
                                    SingletonSeparatedList<ExpressionSyntax>(
                                        OmittedArraySizeExpression())))))
                    .WithVariables(
                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                            VariableDeclarator(
                                Identifier("args"))
                            .WithInitializer(
                                EqualsValueClause(
                                    ArrayCreationExpression(
                                        ArrayType(
                                            PredefinedType(
                                                Token(SyntaxKind.ObjectKeyword)))
                                        .WithRankSpecifiers(
                                            SingletonList<ArrayRankSpecifierSyntax>(
                                                ArrayRankSpecifier(
                                                    SingletonSeparatedList<ExpressionSyntax>(
                                                        LiteralExpression(
                                                            SyntaxKind.NumericLiteralExpression,
                                                            Literal(0)))))))))))),
                // bool run = RpcAttribute.OnInvoke(id, this, args);
                LocalDeclarationStatement(
                    VariableDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.BoolKeyword)))
                    .WithVariables(
                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                            VariableDeclarator(
                                Identifier("run"))
                            .WithInitializer(
                                EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("RpcAttribute"),
                                            IdentifierName("OnInvoke")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SeparatedList<ArgumentSyntax>(
                                                new SyntaxNodeOrToken[]{
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.NumericLiteralExpression,
                                                            Literal(id))),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        ThisExpression()),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        IdentifierName("args"))})))))))),
                //  if (run)
                //      base.MyRpc();
                IfStatement(
                    IdentifierName("run"),
                    Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        BaseExpression(),
                                        IdentifierName(m.Identifier.ValueText))))))),
                ReturnStatement());
        }
    }
}