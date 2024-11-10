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
            GeneratorState.ClearRpcIds();
            AddPrimitives();
            AddBuiltIns();

            // cache IEncodable types
            var encodableProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_IEncodable),
                transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var encodableCompilation = context.CompilationProvider.Combine(encodableProvider.Collect());

            context.RegisterSourceOutput(encodableCompilation, CacheEncodables);

            // pre-solve const and enum values
            var registryProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_RpcIdRegistry),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var registryCompilation = context.CompilationProvider.Combine(registryProvider.Collect());

            context.RegisterSourceOutput(registryCompilation, SolveConstAndEnumValues);

            // generate network object proxies
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_NetworkObject),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(m => m is not null);

            var compilation = context.CompilationProvider.Combine(provider.Collect());

            context.RegisterSourceOutput(compilation, GenerateProxies);
        }

        private void AddBuiltIns()
        {
            GeneratorState.AddEncodable(Helpers.Tk_RpcId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_RpcId, false);
            GeneratorState.AddEncodable(Helpers.Tk_ClientId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_ClientId, false);
            GeneratorState.AddEncodable(Helpers.Tk_AppId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_AppId, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkId, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkId, false);

            GeneratorState.AddEncodable(Helpers.Tk_NetworkBitSet, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkBitSet, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkDict, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkDict, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkList, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkList, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkString, true);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkString, true);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec2, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec2, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec3, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec3, false);
            GeneratorState.AddEncodable(Helpers.Tk_NetworkVec4, false);
            GeneratorState.AddEncodable(Helpers.Tk_OwlTree + "." + Helpers.Tk_NetworkVec4, false);
        }

        private void AddPrimitives()
        {
            GeneratorState.AddEncodable(Helpers.Tk_Byte, false);

            GeneratorState.AddEncodable(Helpers.Tk_UShort, false);
            GeneratorState.AddEncodable(Helpers.Tk_Short, false);

            GeneratorState.AddEncodable(Helpers.Tk_UInt, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int, false);
            GeneratorState.AddEncodable(Helpers.Tk_UInt32, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int32, false);

            GeneratorState.AddEncodable(Helpers.Tk_Float, false);

            GeneratorState.AddEncodable(Helpers.Tk_ULong, false);
            GeneratorState.AddEncodable(Helpers.Tk_Long, false);
            GeneratorState.AddEncodable(Helpers.Tk_UInt64, false);
            GeneratorState.AddEncodable(Helpers.Tk_Int64, false);

            GeneratorState.AddEncodable(Helpers.Tk_Double, false);

            GeneratorState.AddEncodable(Helpers.Tk_String, false);
        }

        private void CacheEncodables(SourceProductionContext context, (Compilation Left, ImmutableArray<TypeDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            if (list.Length == 0)
                return;
            
            var names = new List<string>();

            foreach (var encodable in list)
            {
                names.Clear();
                Helpers.GetAllNames(encodable.Identifier.ValueText, encodable, names);
                bool isVariable = Helpers.InheritsFrom(encodable, Helpers.Tk_IVariable);

                if (!GeneratorState.HasEncodable(names.Last()))
                {
                    foreach (var name in names)
                        GeneratorState.AddEncodable(name, isVariable);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "encodable-out.txt", GeneratorState.GetEncodablesString());
        }

        private void SolveConstAndEnumValues(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            if (list.Length == 0)
                return;
            
            var registry = list[0];

            for (int i = 1; i < list.Length; i++)
                Diagnostics.MultipleIdRegistries(context, list[i]);

            if (!Helpers.IsStatic(registry))
            {
                Diagnostics.NonStaticRegistry(context, registry);
                return;
            }

            var fields = registry.Members.OfType<FieldDeclarationSyntax>();
            SolveConstValues(context, fields);

            var enums = registry.Members.OfType<EnumDeclarationSyntax>();
            SolveEnumValues(context, enums);
        }

        private void SolveConstValues(SourceProductionContext context, IEnumerable<FieldDeclarationSyntax> fields)
        {
            // add built in first rpc id const
            GeneratorState.AddConst(Helpers.Tk_FirstId, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithClass, (int)Helpers.FIRST_RPC_ID);
            GeneratorState.AddConst(Helpers.Tk_FirstIdWithNamespace, (int)Helpers.FIRST_RPC_ID);

            var names = new List<string>();
            foreach (var field in fields)
            {

                if (Helpers.IsConst(field) && Helpers.IsInt(field))
                {
                    names.Clear();
                    Helpers.GetAllNames(Helpers.GetFieldName(field), field, names);
                    var value = Helpers.GetInt(field);
                    foreach (var name in names)
                        GeneratorState.AddConst(name, value);
                }
                else
                {
                    Diagnostics.BadRpcIdConst(context, field);
                }
            }

            File.WriteAllText(EnvConsts.ProjectPath + "const-out.txt", GeneratorState.GetConstsString());
        }

        private void SolveEnumValues(SourceProductionContext context, IEnumerable<EnumDeclarationSyntax> enums)
        {

            GeneratorState.ClearEnums();
            var names = new List<string>();
            foreach (var e in enums)
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

        private void GenerateProxies(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            // select all methods, filter for rpcs, and sort rpcs with assigned ids first
            var methods = list.SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>())
                .Where(m => Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_Rpc))
                .OrderBy(m => (
                    Helpers.HasAttribute(m.AttributeLists, Helpers.AttrTk_AssignRpcId) ? "0" : "1"
                    ) + m.Identifier.ValueText);
            AssignRpcIds(context, methods);
            
            foreach (var c in list)
            {
                var proxy = CreateProxy(c);
                File.WriteAllText(EnvConsts.ProjectPath + c.Identifier.ValueText + ".txt", proxy.ToString());
            }
        }

        public void AssignRpcIds(SourceProductionContext context, IOrderedEnumerable<MethodDeclarationSyntax> methods)
        {
            uint curId = Helpers.FIRST_RPC_ID;
            uint _curId = Helpers.FIRST_RPC_ID;
            GeneratorState.ClearRpcIds();

            foreach (MethodDeclarationSyntax m in methods)
            {
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

                if (!Helpers.IsEncodable(m.ParameterList, out var err))
                {
                    Diagnostics.NonEncodableRpcParam(context, m, err);
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