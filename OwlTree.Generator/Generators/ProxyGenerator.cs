
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OwlTree.Generator
{

    /// <summary>
    /// Creates proxies for network objects. RPC "injected" code here.
    /// </summary>
    public static class ProxyGenerator
    {
        /// <summary>
        /// Creates the proxy class name for the given class.
        /// </summary>
        public static string GetProxyName(ClassDeclarationSyntax c)
        {
            return c.Identifier.ValueText + Helpers.Tk_ProxySuffix;
        }

        public static CompilationUnitSyntax CreateProxy(ClassDeclarationSyntax c)
        {

            return CompilationUnit()
            .WithUsings(GetUsings(c))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    ClassDeclaration(GetProxyName(c))
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

        private static SyntaxList<UsingDirectiveSyntax> GetUsings(ClassDeclarationSyntax c)
        {
            var name = Helpers.GetNamespaceName(c);
            if (name != null)
            {
                return List<UsingDirectiveSyntax>(
                    new UsingDirectiveSyntax[]{
                        UsingDirective(
                            IdentifierName(Helpers.Tk_OwlTree)),
                        UsingDirective(
                            IdentifierName(name))});
            }
            
            return SingletonList<UsingDirectiveSyntax>(
                    UsingDirective(
                        IdentifierName(Helpers.Tk_OwlTree)));

        }

        static List<MethodDeclarationSyntax> proxyBuilderStage = new();

        private static SyntaxList<MemberDeclarationSyntax> CreateRpcProxies(ClassDeclarationSyntax c)
        {

            var methods = c.Members.OfType<MethodDeclarationSyntax>()
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc));

            proxyBuilderStage.Clear();

            foreach (var m in methods)
            {
                if (!GeneratorState.TryGetRpcData(Helpers.GetFullName(m.Identifier.ValueText, m), out var data))
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
                    .WithBody(m.ParameterList.Parameters.Count == 0 ? CreateProxyBodyNoParams(m, data.id) : CreateProxyBody(m, data.id));
                
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
        private static BlockSyntax CreateProxyBody(MethodDeclarationSyntax m, uint id)
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

        private static SyntaxNodeOrToken[] CreateArgArray(MethodDeclarationSyntax m)
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

        private static SyntaxNodeOrToken[] CreateParamArray(MethodDeclarationSyntax m)
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
        private static BlockSyntax CreateProxyBodyNoParams(MethodDeclarationSyntax m, uint id)
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