
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;

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
                                    IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c))
                                ))))
                    .WithMembers(
                        CreateRpcProxies(c))))
                    .WithAttributeLists(
                        SingletonList<AttributeListSyntax>(
                            AttributeList(
                                SingletonSeparatedList<AttributeSyntax>(
                                    Attribute(
                                        IdentifierName(Helpers.AttrTk_CompilerGenerated))))))
            .NormalizeWhitespace();
        }

        private static SyntaxList<UsingDirectiveSyntax> GetUsings(ClassDeclarationSyntax c)
        {
            var name = Helpers.GetNamespaceName(c);
            var usings = Helpers.GetAllUsings(c);
            if (name != null)
                usings = usings.Add(UsingDirective(IdentifierName(name)));
            
            if (!Helpers.IsUsing(usings, Helpers.Tk_System))
                usings = usings.Add(UsingDirective(IdentifierName(Helpers.Tk_System)));
            
            if (!Helpers.IsUsing(usings, Helpers.Tk_OwlTree))
                usings = usings.Add(UsingDirective(IdentifierName(Helpers.Tk_OwlTree)));
            
            if (!Helpers.IsUsing(usings, Helpers.Tk_CompilerServices))
                usings = usings.Add(UsingDirective(IdentifierName(Helpers.Tk_CompilerServices)));
            
            return usings;
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
                    .WithBody(CreateProxyBody(m, data.id));
                
                proxyBuilderStage.Add(proxy);
            }

            proxyBuilderStage.Add(CreateGetType(c));
            proxyBuilderStage.Add(CreateGetProxyType(c));

            var proxyList = List<MemberDeclarationSyntax>(proxyBuilderStage);

            return proxyList;
        }

        private static MethodDeclarationSyntax CreateGetProxyType(ClassDeclarationSyntax c)
        {
            return MethodDeclaration(
                IdentifierName("Type"),
                Identifier("GetProxyType"))
            .WithModifiers(
                TokenList(
                    new []{
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.OverrideKeyword)}))
            .WithBody(
                Block(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            TypeOfExpression(
                                IdentifierName(GetProxyName(c)))))));
        }

        private static MethodDeclarationSyntax CreateGetType(ClassDeclarationSyntax c)
        {
            return MethodDeclaration(
                IdentifierName("Type"),
                Identifier("GetType"))
            .WithModifiers(
                TokenList(
                    new []{
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.OverrideKeyword)}))
            .WithBody(
                Block(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            TypeOfExpression(
                                IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c)))))));
        }

        private static BlockSyntax CreateProxyBody(MethodDeclarationSyntax m, uint id)
        {
            return Block(
                //if (!IsActive)
                //  throw new InvalidOperationException("Attempted to call RPC " + Connection.Protocols.GetRpcName(RpcId) + "on an inactive NetworkObject with id:" + Id.ToString());
                IfStatement(
                    PrefixUnaryExpression(
                        SyntaxKind.LogicalNotExpression,
                        IdentifierName(Helpers.MTk_IsActive)),
                    ThrowStatement(
                        ObjectCreationExpression(
                            IdentifierName("InvalidOperationException"))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        BinaryExpression(
                                            SyntaxKind.AddExpression,
                                            BinaryExpression(
                                                SyntaxKind.AddExpression,
                                                BinaryExpression(
                                                    SyntaxKind.AddExpression,
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal("Attempted to call RPC ")),
                                                    InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                IdentifierName(Helpers.MTk_Connection),
                                                                IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                                            IdentifierName(Helpers.Tk_GetRpcName)))
                                                    .WithArgumentList(
                                                        ArgumentList(
                                                            SingletonSeparatedList<ArgumentSyntax>(
                                                                Argument(
                                                                    LiteralExpression(
                                                                        SyntaxKind.NumericLiteralExpression,
                                                                        Literal(id))))))),
                                                LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression,
                                                    Literal("on an inactive NetworkObject with id:"))),
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName(Helpers.MTk_Id),
                                                    IdentifierName("ToString")))))))))),
                //if (Connection == null)
                //  throw new InvalidOperationException("RPCs can only be called on an active connection.");
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName(Helpers.MTk_Connection),
                        LiteralExpression(
                            SyntaxKind.NullLiteralExpression)),
                    ThrowStatement(
                        ObjectCreationExpression(
                            IdentifierName("InvalidOperationException"))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal("RPCs can only be called on an active connection.")))))))),
                // if (!i_ReceivingRpc != RpcId && Connection.Protocols.CanCallRpc(Connection.NetRole, RpcId))
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            IdentifierName(Helpers.MTk_ReceivingRpc),
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(id))),
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.MTk_Connection),
                                    IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                IdentifierName(Helpers.MTk_CanCallRpc)))
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName(Helpers.MTk_Connection),
                                                IdentifierName(Helpers.MTk_NetRole))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.NumericLiteralExpression,
                                                Literal(id)))})))),
                    Block(
                        // object[] args = new object[]{args... , (replace RpcCaller w/ LocalId)};
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
                                    Identifier(Helpers.ArgTk_Args))
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
                                                            OmittedArraySizeExpression())))))
                                        .WithInitializer(
                                            InitializerExpression(
                                                SyntaxKind.ArrayInitializerExpression,
                                                SeparatedList<ExpressionSyntax>(CreateArgArray(m))))))))),
                        // int calleeArg = Connection.Protocols.GetRpcCalleeParam(RpcId);
                        LocalDeclarationStatement(
                        VariableDeclaration(
                            PredefinedType(
                                Token(SyntaxKind.IntKeyword)))
                        .WithVariables(
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(
                                    Identifier("calleeArg"))
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName(Helpers.MTk_Connection),
                                                    IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                                IdentifierName(Helpers.Tk_GetRpcCalleeParam)))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SingletonSeparatedList<ArgumentSyntax>(
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.NumericLiteralExpression,
                                                            Literal(id))))))))))),
                        // i_OnRpcCall.Invoke(
                        //     calleeArg == -1 ? ClientId.None : (ClientId)args[calleeArg],
                        //     new RpcId(RpcId),
                        //     Id,
                        //     Connection.Protocols.GetSendProtocol(1),
                        //     args
                        // );
                        ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.MTk_OnRpcCall),
                                    IdentifierName("Invoke")))
                            .WithArgumentList(
                                ArgumentList(
                                    SeparatedList<ArgumentSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            Argument(
                                                ConditionalExpression(
                                                    BinaryExpression(
                                                        SyntaxKind.EqualsExpression,
                                                        IdentifierName("calleeArg"),
                                                        PrefixUnaryExpression(
                                                            SyntaxKind.UnaryMinusExpression,
                                                            LiteralExpression(
                                                                SyntaxKind.NumericLiteralExpression,
                                                                Literal(1)))),
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName(Helpers.Tk_ClientId),
                                                        IdentifierName(Helpers.MTk_None)),
                                                    CastExpression(
                                                        IdentifierName(Helpers.Tk_ClientId),
                                                        ElementAccessExpression(
                                                            IdentifierName(Helpers.ArgTk_Args))
                                                        .WithArgumentList(
                                                            BracketedArgumentList(
                                                                SingletonSeparatedList<ArgumentSyntax>(
                                                                    Argument(
                                                                        IdentifierName("calleeArg")))))))),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                ObjectCreationExpression(
                                                    IdentifierName(Helpers.Tk_RpcId))
                                                .WithArgumentList(
                                                    ArgumentList(
                                                        SingletonSeparatedList<ArgumentSyntax>(
                                                            Argument(
                                                                LiteralExpression(
                                                                    SyntaxKind.NumericLiteralExpression,
                                                                    Literal(id))))))),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                IdentifierName(Helpers.MTk_Id)),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName(Helpers.MTk_Connection),
                                                            IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                                        IdentifierName(Helpers.Tk_GetSendProtocol)))
                                                .WithArgumentList(
                                                    ArgumentList(
                                                        SingletonSeparatedList<ArgumentSyntax>(
                                                            Argument(
                                                                LiteralExpression(
                                                                    SyntaxKind.NumericLiteralExpression,
                                                                    Literal(id))))))),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                IdentifierName(Helpers.ArgTk_Args))})))),
                        // if (Connection.Protocols.IsInvokeOnCaller(RpcId))
                        //     base.RpcName(args... , (replace RpcCaller w/ LocalId));
                        IfStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(Helpers.MTk_Connection),
                                        IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                    IdentifierName(Helpers.Tk_IsInvokeOnCaller)))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList<ArgumentSyntax>(
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.NumericLiteralExpression,
                                                Literal(id)))))),
                            ExpressionStatement(
                                m.ParameterList.Parameters.Count == 0 ? 
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            BaseExpression(),
                                            IdentifierName(m.Identifier.ValueText)))
                                :
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            BaseExpression(),
                                            IdentifierName(m.Identifier.ValueText)))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SeparatedList<ArgumentSyntax>(CreateParamArray(m))))))))
                // else if (i_ReceivingRpc == RpcId)
                //     base.RpcName(args...); <- rpc caller not replaced
                .WithElse(
                ElseClause(
                    IfStatement(
                        BinaryExpression(
                            SyntaxKind.EqualsExpression,
                            IdentifierName(Helpers.MTk_ReceivingRpc),
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(id))),
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
                                            SeparatedList<ArgumentSyntax>(CreateParamArray(m, false))))))))
                // else
                    // throw new InvalidOperationException("This connection does not have permission to call RPC " + 
                    // Connection.Protocols.GetRpcName(RpcId) + " on NetworkObject " + Id.ToString());
                .WithElse(
                ElseClause(
                    Block(
                        SingletonList<StatementSyntax>(
                            ThrowStatement(
                                ObjectCreationExpression(
                                    IdentifierName("InvalidOperationException"))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                InterpolatedStringExpression(
                                                    Token(SyntaxKind.InterpolatedStringStartToken))
                                                .WithContents(
                                                    List<InterpolatedStringContentSyntax>(
                                                        new InterpolatedStringContentSyntax[]{
                                                            InterpolatedStringText()
                                                            .WithTextToken(
                                                                Token(
                                                                    TriviaList(),
                                                                    SyntaxKind.InterpolatedStringTextToken,
                                                                    "This connection does not have permission to call RPC ",
                                                                    "This connection does not have permission to call RPC ",
                                                                    TriviaList())),
                                                            Interpolation(
                                                                InvocationExpression(
                                                                    MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        MemberAccessExpression(
                                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                                            IdentifierName(Helpers.MTk_Connection),
                                                                            IdentifierName(Helpers.MTk_ConnectionProtocols)),
                                                                        IdentifierName(Helpers.Tk_GetRpcName)))
                                                                .WithArgumentList(
                                                                    ArgumentList(
                                                                        SingletonSeparatedList<ArgumentSyntax>(
                                                                            Argument(
                                                                                LiteralExpression(
                                                                                    SyntaxKind.NumericLiteralExpression,
                                                                                    Literal(id))))))),
                                                            InterpolatedStringText()
                                                            .WithTextToken(
                                                                Token(
                                                                    TriviaList(),
                                                                    SyntaxKind.InterpolatedStringTextToken,
                                                                    " on NetworkObject ",
                                                                    " on NetworkObject ",
                                                                    TriviaList())),
                                                            Interpolation(
                                                                IdentifierName(Helpers.MTk_Id)),
                                                            InterpolatedStringText()
                                                            .WithTextToken(
                                                                Token(
                                                                    TriviaList(),
                                                                    SyntaxKind.InterpolatedStringTextToken,
                                                                    ".",
                                                                    ".",
                                                                    TriviaList()))}))))))))))))));
        }

        // builds args array variable
        private static SyntaxNodeOrToken[] CreateArgArray(MethodDeclarationSyntax m, bool replaceCaller = true)
        {
            var arr = new SyntaxNodeOrToken[m.ParameterList.Parameters.Count == 0 ? 0 : (m.ParameterList.Parameters.Count * 2) - 1];
            
            for (int i = 0; i < arr.Length; i++)
            {
                if (i % 2 == 0)
                {
                    if (replaceCaller && Helpers.HasAttribute(m.ParameterList.Parameters[i / 2].AttributeLists, Helpers.AttrTk_RpcCaller))
                    {
                        arr[i] = MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.MTk_Connection),
                                    IdentifierName(Helpers.MTk_LocalId));
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

        // syntax array for passing arguments to the next method
        private static SyntaxNodeOrToken[] CreateParamArray(MethodDeclarationSyntax m, bool replaceCaller = true)
        {
            var arr = new SyntaxNodeOrToken[m.ParameterList.Parameters.Count == 0 ? 0 : (m.ParameterList.Parameters.Count * 2) - 1];
            
            for (int i = 0; i < arr.Length; i++)
            {
                if (i % 2 == 0)
                    if (replaceCaller && Helpers.HasAttribute(m.ParameterList.Parameters[i / 2].AttributeLists, Helpers.AttrTk_RpcCaller))
                    {
                        arr[i] = Argument(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.MTk_Connection),
                                    IdentifierName(Helpers.MTk_LocalId)));
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
    }
}