
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    public static class RpcProtocolsGenerator
    {
        private static List<SwitchSectionSyntax> _getProtocol = new();
        private static List<SwitchSectionSyntax> _getRpcCaller = new();
        private static List<SwitchSectionSyntax> _getRpcCallerParam = new();
        private static List<SwitchSectionSyntax> _getRpcCalleeParam = new();
        private static List<SwitchSectionSyntax> _getRpcName = new();
        private static List<SwitchSectionSyntax> _getRpcParamName = new();
        private static List<SwitchSectionSyntax> _getSendProtocol = new();
        private static List<SwitchSectionSyntax> _isInvokeOnCaller = new();
        private static List<SwitchSectionSyntax> _invokeRpc = new();
        private static List<ExpressionSyntax> _idArray = new();

        public static void Reset()
        {
            _getProtocol.Clear();
            _getRpcCaller.Clear();
            _getRpcCallerParam.Clear();
            _getRpcCalleeParam.Clear();
            _getRpcName.Clear();
            _getRpcParamName.Clear();
            _getSendProtocol.Clear();
            _isInvokeOnCaller.Clear();
            _invokeRpc.Clear();
            _idArray.Clear();
        }

        private static void AddDefaults()
        {
            _getProtocol.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.NullLiteralExpression)))));
            
            _getRpcCaller.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.Tk_RpcCaller),
                                    IdentifierName(Helpers.Tk_AnyCaller))))));
            
            _getRpcCalleeParam.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                PrefixUnaryExpression(
                                    SyntaxKind.UnaryMinusExpression,
                                    LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        Literal(1)))))));
            
            _getRpcCallerParam.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                PrefixUnaryExpression(
                                    SyntaxKind.UnaryMinusExpression,
                                    LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        Literal(1)))))));
            
            _getRpcName.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(""))))));

            _getRpcParamName.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(""))))));
            
            _getSendProtocol.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(Helpers.Tk_Protocol),
                                    IdentifierName(Helpers.Tk_TcpProtocol))))));
            
            _isInvokeOnCaller.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.FalseLiteralExpression)))));
            
            _invokeRpc.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement())));
        }

        public static CompilationUnitSyntax GetRpcProtocols()
        {
            AddDefaults();
            return CompilationUnit()
            .WithUsings(List(GeneratorState.GetUsings()))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    ClassDeclaration(Helpers.Tk_ProjectProtocols)
                    .WithModifiers(
                        TokenList(
                            Token(SyntaxKind.PublicKeyword)))
                    .WithBaseList(
                        BaseList(
                            SingletonSeparatedList<BaseTypeSyntax>(
                                SimpleBaseType(
                                    IdentifierName(Helpers.Tk_RpcProtocols)))))
                    .WithMembers(
                        List<MemberDeclarationSyntax>(
                            new MemberDeclarationSyntax[]{
                                MethodDeclaration(
                                    ArrayType(
                                        PredefinedType(
                                            Token(SyntaxKind.UIntKeyword)))
                                    .WithRankSpecifiers(
                                        SingletonList<ArrayRankSpecifierSyntax>(
                                            ArrayRankSpecifier(
                                                SingletonSeparatedList<ExpressionSyntax>(
                                                    OmittedArraySizeExpression())))),
                                    Identifier(Helpers.Tk_GetRpcIds))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            ReturnStatement(
                                                ArrayCreationExpression(
                                                    ArrayType(
                                                        PredefinedType(
                                                            Token(SyntaxKind.UIntKeyword)))
                                                    .WithRankSpecifiers(
                                                        SingletonList<ArrayRankSpecifierSyntax>(
                                                            ArrayRankSpecifier(
                                                                SingletonSeparatedList<ExpressionSyntax>(
                                                                    OmittedArraySizeExpression())))))
                                                .WithInitializer(
                                                    InitializerExpression(
                                                        SyntaxKind.ArrayInitializerExpression,
                                                        SeparatedList<ExpressionSyntax>(_idArray))))))),
                                // =======================================
                                MethodDeclaration(
                                    ArrayType(
                                        IdentifierName("Type"))
                                    .WithRankSpecifiers(
                                        SingletonList<ArrayRankSpecifierSyntax>(
                                            ArrayRankSpecifier(
                                                SingletonSeparatedList<ExpressionSyntax>(
                                                    OmittedArraySizeExpression())))),
                                    Identifier(Helpers.Tk_GetProtocol))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getProtocol))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.IntKeyword)),
                                    Identifier(Helpers.Tk_GetRpcCalleeParam))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getRpcCalleeParam))))),
                                // =======================================
                                MethodDeclaration(
                                    IdentifierName(Helpers.Tk_RpcCaller),
                                    Identifier(Helpers.Tk_GetRpcCaller))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getRpcCaller))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.IntKeyword)),
                                    Identifier(Helpers.Tk_GetRpcCallerParam))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getRpcCallerParam))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.StringKeyword)),
                                    Identifier(Helpers.Tk_GetRpcName))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getRpcName))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.StringKeyword)),
                                    Identifier(Helpers.Tk_GetRpcParamName))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SeparatedList<ParameterSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Parameter(
                                                    Identifier(Helpers.ArgTk_RpcId))
                                                .WithType(
                                                    PredefinedType(
                                                        Token(SyntaxKind.UIntKeyword))),
                                                Token(SyntaxKind.CommaToken),
                                                Parameter(
                                                    Identifier(Helpers.ArgTk_ParamInd))
                                                .WithType(
                                                    PredefinedType(
                                                        Token(SyntaxKind.IntKeyword)))})))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getRpcParamName))))),
                                // =======================================
                                MethodDeclaration(
                                    IdentifierName(Helpers.Tk_Protocol),
                                    Identifier(Helpers.Tk_GetSendProtocol))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_getSendProtocol))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.BoolKeyword)),
                                    Identifier(Helpers.Tk_IsInvokeOnCaller))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SingletonSeparatedList<ParameterSyntax>(
                                            Parameter(
                                                Identifier(Helpers.ArgTk_RpcId))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword))))))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_isInvokeOnCaller))))),
                                // =======================================
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.VoidKeyword)),
                                    Identifier(Helpers.Tk_InvokeRpc))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.ProtectedKeyword),
                                            Token(SyntaxKind.OverrideKeyword)}))
                                .WithParameterList(
                                    ParameterList(
                                        SeparatedList<ParameterSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Parameter(
                                                    Identifier(Helpers.ArgTk_RpcId))
                                                .WithType(
                                                    PredefinedType(
                                                        Token(SyntaxKind.UIntKeyword))),
                                                Token(SyntaxKind.CommaToken),
                                                Parameter(
                                                    Identifier(Helpers.ArgTk_Target))
                                                .WithType(
                                                    IdentifierName(Helpers.Tk_NetworkObject)),
                                                Token(SyntaxKind.CommaToken),
                                                Parameter(
                                                    Identifier(Helpers.ArgTk_Args))
                                                .WithType(
                                                    ArrayType(
                                                        PredefinedType(
                                                            Token(SyntaxKind.ObjectKeyword)))
                                                    .WithRankSpecifiers(
                                                        SingletonList<ArrayRankSpecifierSyntax>(
                                                            ArrayRankSpecifier(
                                                                SingletonSeparatedList<ExpressionSyntax>(
                                                                    OmittedArraySizeExpression())))))})))
                                .WithBody(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            SwitchStatement(
                                                IdentifierName(Helpers.ArgTk_RpcId))
                                            .WithSections(List(_invokeRpc)))))}))))
            .NormalizeWhitespace();
        }

        public static void AddRpc(GeneratorState.RpcData data)
        {
            _idArray.Add(LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    Literal(data.id)));

            _getProtocol.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            ArrayCreationExpression(
                                ArrayType(
                                    IdentifierName("Type"))
                                .WithRankSpecifiers(
                                    SingletonList<ArrayRankSpecifierSyntax>(
                                        ArrayRankSpecifier(
                                            SingletonSeparatedList<ExpressionSyntax>(
                                                OmittedArraySizeExpression())))))
                                .WithInitializer(
                                    InitializerExpression(
                                        SyntaxKind.ArrayInitializerExpression,
                                        SeparatedList<ExpressionSyntax>(
                                            GetParamTypesList(data.paramData))))))));
            
            _getRpcCalleeParam.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(GetRpcCalleeParam(data.paramData)))))));
            
            _getRpcCallerParam.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(GetRpcCallerParam(data.paramData)))))));
            
            _getRpcCaller.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(Helpers.Tk_RpcCaller),
                                IdentifierName(GetRpcCallerToken(data.caller)))))));

            _getRpcName.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(data.name))))));

            _getSendProtocol.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(Helpers.Tk_Protocol),
                                IdentifierName(data.useTcp ? Helpers.Tk_TcpProtocol : Helpers.Tk_UdpProtocol))))));
            
            _isInvokeOnCaller.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                data.invokeOnCaller ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)))));
            
            _getRpcParamName.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        SwitchStatement(
                            IdentifierName(Helpers.ArgTk_ParamInd))
                        .WithSections(
                            List(GetParamNamesSwitch(data.paramData))))));
            
            _invokeRpc.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(data.id)))))
                .WithStatements(
                    List<StatementSyntax>(
                        new StatementSyntax[]{
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParenthesizedExpression(
                                            CastExpression(
                                                IdentifierName(data.parentClass),
                                                IdentifierName(Helpers.ArgTk_Target))),
                                        IdentifierName(data.name)))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList(GetArgumentList(data.paramData))))),
                            BreakStatement()})));
        }

        private static List<ArgumentSyntax> GetArgumentList(GeneratorState.ParamData[] paramData)
        {
            var list = new List<ArgumentSyntax>();

            for (int i = 0; i < paramData.Length; i++)
            {
                list.Add(Argument(
                    CastExpression(
                        IdentifierName(paramData[i].type),
                        ElementAccessExpression(
                            IdentifierName(Helpers.ArgTk_Args))
                        .WithArgumentList(
                            BracketedArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        LiteralExpression(
                                            SyntaxKind.NumericLiteralExpression,
                                            Literal(i)))))))));
            }
            return list;
        }

        private static List<SwitchSectionSyntax> GetParamNamesSwitch(GeneratorState.ParamData[] paramData)
        {
            var list = new List<SwitchSectionSyntax>();

            for (int i = 0; i < paramData.Length; i++)
            {
                list.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CaseSwitchLabel(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(i)))))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(paramData[i].name))))));
            }
            list.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(""))))));
            
            return list;
        }

        private static IEnumerable<ExpressionSyntax> GetParamTypesList(GeneratorState.ParamData[] paramData)
        {
            return paramData.Select(p => TypeOfExpression(IdentifierName(p.type)));
        }

        private static int GetRpcCallerParam(GeneratorState.ParamData[] paramData)
        {
            for (int i = 0; i < paramData.Length; i++)
                if (paramData[i].isRpcCaller) return i;
            return -1;
        }

        private static int GetRpcCalleeParam(GeneratorState.ParamData[] paramData)
        {
            for (int i = 0; i < paramData.Length; i++)
                if (paramData[i].isRpcCallee) return i;
            return -1;
        }

        private static string GetRpcCallerToken(GeneratorState.RpcCaller caller)
        {
            switch (caller)
            {
                case GeneratorState.RpcCaller.Server:
                    return Helpers.Tk_ServerCaller;
                case GeneratorState.RpcCaller.Client:
                    return Helpers.Tk_ClientCaller;
                case GeneratorState.RpcCaller.Any:
                default:
                    return Helpers.Tk_AnyCaller;
            }
        }
    }
}