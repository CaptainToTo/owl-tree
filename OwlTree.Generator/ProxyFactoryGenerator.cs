
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    /// <summary>
    /// Creates the project's proxy factory file. Incrementally feed this class network object proxies to
    /// add then to the factory.
    /// </summary>
    public static class ProxyFactoryGenerator
    {
        private static List<SwitchSectionSyntax> _createProxy = new();
        private static List<SwitchSectionSyntax> _hasTypeId = new();
        private static List<SwitchSectionSyntax> _typeFromId = new();
        private static List<SwitchSectionSyntax> _typeId = new();

        public static void Reset()
        {
            _createProxy.Clear();
            _hasTypeId.Clear();
            _typeFromId.Clear();
            _typeId.Clear();
        }

        public static ClassDeclarationSyntax GetFactory()
        {
            AddDefaults();

            return ClassDeclaration(Helpers.Tk_FactoryName)
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithBaseList(
                    BaseList(
                        SingletonSeparatedList<BaseTypeSyntax>(
                            SimpleBaseType(
                                IdentifierName(Helpers.Tk_ProxyFactory)))))
                .WithMembers(
                    List<MemberDeclarationSyntax>(
                        new MemberDeclarationSyntax[]{
                            MethodDeclaration(
                                IdentifierName(Helpers.Tk_NetworkObject),
                                Identifier("CreateProxy"))
                            .WithModifiers(
                                TokenList(
                                    new []{
                                        Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.OverrideKeyword)}))
                            .WithParameterList(
                                ParameterList(
                                    SingletonSeparatedList<ParameterSyntax>(
                                        Parameter(
                                            Identifier(
                                                TriviaList(),
                                                SyntaxKind.TypeKeyword,
                                                "type",
                                                "type",
                                                TriviaList()))
                                        .WithType(
                                            IdentifierName("Type")))))
                            .WithBody(
                                Block(
                                    SingletonList<StatementSyntax>(
                                        SwitchStatement(
                                            IdentifierName(
                                                Identifier(
                                                    TriviaList(),
                                                    SyntaxKind.TypeKeyword,
                                                    "type",
                                                    "type",
                                                    TriviaList())))
                                        .WithSections(
                                            List<SwitchSectionSyntax>( _createProxy ))))),
                            MethodDeclaration(
                                PredefinedType(
                                    Token(SyntaxKind.BoolKeyword)),
                                Identifier("HasTypeId"))
                            .WithModifiers(
                                TokenList(
                                    new []{
                                        Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.OverrideKeyword)}))
                            .WithParameterList(
                                ParameterList(
                                    SingletonSeparatedList<ParameterSyntax>(
                                        Parameter(
                                            Identifier(
                                                TriviaList(),
                                                SyntaxKind.TypeKeyword,
                                                "type",
                                                "type",
                                                TriviaList()))
                                        .WithType(
                                            IdentifierName("Type")))))
                            .WithBody(
                                Block(
                                    SingletonList<StatementSyntax>(
                                        SwitchStatement(
                                            IdentifierName(
                                                Identifier(
                                                    TriviaList(),
                                                    SyntaxKind.TypeKeyword,
                                                    "type",
                                                    "type",
                                                    TriviaList())))
                                        .WithSections(
                                            List<SwitchSectionSyntax>( _hasTypeId ))))),
                            MethodDeclaration(
                                IdentifierName("Type"),
                                Identifier("TypeFromId"))
                            .WithModifiers(
                                TokenList(
                                    new []{
                                        Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.OverrideKeyword)}))
                            .WithParameterList(
                                ParameterList(
                                    SingletonSeparatedList<ParameterSyntax>(
                                        Parameter(
                                            Identifier("id"))
                                        .WithType(
                                            PredefinedType(
                                                Token(SyntaxKind.ByteKeyword))))))
                            .WithBody(
                                Block(
                                    SingletonList<StatementSyntax>(
                                        SwitchStatement(
                                            IdentifierName("id"))
                                        .WithSections(
                                            List<SwitchSectionSyntax>( _typeFromId ))))),
                            MethodDeclaration(
                                PredefinedType(
                                    Token(SyntaxKind.ByteKeyword)),
                                Identifier("TypeId"))
                            .WithModifiers(
                                TokenList(
                                    new []{
                                        Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.OverrideKeyword)}))
                            .WithParameterList(
                                ParameterList(
                                    SingletonSeparatedList<ParameterSyntax>(
                                        Parameter(
                                            Identifier(
                                                TriviaList(),
                                                SyntaxKind.TypeKeyword,
                                                "type",
                                                "type",
                                                TriviaList()))
                                        .WithType(
                                            IdentifierName("Type")))))
                            .WithBody(
                                Block(
                                    SingletonList<StatementSyntax>(
                                        SwitchStatement(
                                            IdentifierName(
                                                Identifier(
                                                    TriviaList(),
                                                    SyntaxKind.TypeKeyword,
                                                    "type",
                                                    "type",
                                                    TriviaList())))
                                        .WithSections(
                                            List<SwitchSectionSyntax>( _typeId )))))}));
        }

        public static void AddClass(ClassDeclarationSyntax c)
        {
            byte id = GeneratorState.GetTypeId(Helpers.GetFullName(c.Identifier.ValueText, c));
            _createProxy.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CasePatternSwitchLabel(
                            DeclarationPattern(
                                IdentifierName("Type"),
                                SingleVariableDesignation(
                                    Identifier("t"))),
                            Token(SyntaxKind.ColonToken))
                        .WithWhenClause(
                            WhenClause(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName("t"),
                                    TypeOfExpression(
                                        IdentifierName(c.Identifier.ValueText)))))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            ObjectCreationExpression(
                                IdentifierName(ProxyGenerator.GetProxyName(c)))
                            .WithArgumentList(
                                ArgumentList())))));
            
            _hasTypeId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CasePatternSwitchLabel(
                            DeclarationPattern(
                                IdentifierName("Type"),
                                SingleVariableDesignation(
                                    Identifier("t"))),
                            Token(SyntaxKind.ColonToken))
                        .WithWhenClause(
                            WhenClause(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName("t"),
                                    TypeOfExpression(
                                        IdentifierName(c.Identifier.ValueText)))))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.TrueLiteralExpression)))));
            
            _typeFromId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(3)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            TypeOfExpression(
                                IdentifierName(c.Identifier.ValueText))))));
            _typeId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        CasePatternSwitchLabel(
                            DeclarationPattern(
                                IdentifierName("Type"),
                                SingleVariableDesignation(
                                    Identifier("t"))),
                            Token(SyntaxKind.ColonToken))
                        .WithWhenClause(
                            WhenClause(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName("t"),
                                    TypeOfExpression(
                                        IdentifierName(c.Identifier.ValueText)))))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(id))))));
        }

        private static void AddDefaults()
        {
            _createProxy.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            ObjectCreationExpression(
                                IdentifierName(Helpers.Tk_NetworkObject))
                            .WithArgumentList(
                                ArgumentList())))));
            
            _hasTypeId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.FalseLiteralExpression)))));
            
            _typeFromId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            TypeOfExpression(
                                IdentifierName(Helpers.Tk_NetworkObject))))));
            
            _typeId.Add(SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("NetworkSpawner"),
                                IdentifierName("NETWORK_BASE_TYPE_ID"))))));
        }
    }
}