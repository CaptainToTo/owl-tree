
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    /// <summary>
    /// Creates the project's proxy factory file. Incrementally feed this class network object class declarations to
    /// add them to the factory with <c>AddClass()</c>. 
    /// Once all network objects are added to the factory, generate the full factory class with <c>GetFactory()</c>.
    /// </summary>
    public static class ProxyFactoryGenerator
    {
        private static List<SwitchSectionSyntax> _createProxy = new();
        private static List<SwitchSectionSyntax> _hasTypeId = new();
        private static List<SwitchSectionSyntax> _typeFromId = new();
        private static List<SwitchSectionSyntax> _typeId = new();
        private static List<UsingDirectiveSyntax> _namespaces = new();

        public static void Reset()
        {
            _createProxy.Clear();
            _hasTypeId.Clear();
            _typeFromId.Clear();
            _typeId.Clear();
            _namespaces.Clear();
            _namespaces.Add(UsingDirective(IdentifierName(Helpers.Tk_OwlTree)));
            _namespaces.Add(UsingDirective(IdentifierName(Helpers.Tk_System)));
            _namespaces.Add(UsingDirective(IdentifierName(Helpers.Tk_CompilerServices)));
        }

        public static CompilationUnitSyntax GetFactory()
        {
            AddDefaults();

            return CompilationUnit()
            .WithUsings(List<UsingDirectiveSyntax>(_namespaces))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    ClassDeclaration(Helpers.Tk_ProjectProxies)
                    .WithAttributeLists(
                        SingletonList<AttributeListSyntax>(
                            AttributeList(
                                SingletonSeparatedList<AttributeSyntax>(
                                    Attribute(
                                        IdentifierName(Helpers.AttrTk_CompilerGenerated))))))
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
                                    ArrayType(
                                        PredefinedType(
                                            Token(SyntaxKind.ByteKeyword)))
                                    .WithRankSpecifiers(
                                        SingletonList<ArrayRankSpecifierSyntax>(
                                            ArrayRankSpecifier(
                                                SingletonSeparatedList<ExpressionSyntax>(
                                                    OmittedArraySizeExpression())))),
                                    Identifier(Helpers.Tk_GetTypeIds))
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
                                                            Token(SyntaxKind.ByteKeyword)))
                                                    .WithRankSpecifiers(
                                                        SingletonList<ArrayRankSpecifierSyntax>(
                                                            ArrayRankSpecifier(
                                                                SingletonSeparatedList<ExpressionSyntax>(
                                                                    OmittedArraySizeExpression())))))
                                                .WithInitializer(
                                                    InitializerExpression(
                                                        SyntaxKind.ArrayInitializerExpression,
                                                        SeparatedList<ExpressionSyntax>(
                                                            CreateIdArray()))))))),
                                MethodDeclaration(
                                    IdentifierName(Helpers.Tk_NetworkObject),
                                    Identifier(Helpers.Tk_CreateProxy))
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
                                    Identifier(Helpers.Tk_HasTypeId))
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
                                    Identifier(Helpers.Tk_TypeFromId))
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
                                    Identifier(Helpers.Tk_TypeId))
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
                                                List<SwitchSectionSyntax>( _typeId )))))}))));
        }

        private static ExpressionSyntax[] CreateIdArray()
        {
            var ids = new ExpressionSyntax[GeneratorState.GetTypeIds().Count];

            int i  = 0;
            foreach (var pair in GeneratorState.GetTypeIds())
            {
                ids[i] = LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    Literal(pair.Value));
                i++;
            }

            return ids;
        }

        public static void AddClass(ClassDeclarationSyntax c)
        {
            byte id = GeneratorState.GetTypeId(Helpers.GetFullName(c.Identifier.ValueText, c));

            var space = Helpers.GetNamespaceName(c);
            if (space != null && !_namespaces.Any(u => u.Name.ToString() == space))
            {
                _namespaces.Add(UsingDirective(IdentifierName(space)));
            }

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
                                        IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c))))))))
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
                                        IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c))))))))
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
                                Literal(id)))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            TypeOfExpression(
                                IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c)))))));
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
                                        IdentifierName(Helpers.GetFullName(c.Identifier.ValueText, c))))))))
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
                                        IdentifierName(Helpers.Tk_NetworkObject)))))))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            LiteralExpression(
                                SyntaxKind.TrueLiteralExpression)))));

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
                                IdentifierName(Helpers.Tk_NetworkObject),
                                IdentifierName(Helpers.MTk_NetworkBaseTypeId))))));
        }
    }
}