
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{
    public static class ConstAndEnumAnalyzer
    {
        /// <summary>
        /// Pre-solve const enum values from the project IdRegistry for RPC and type id assignment.
        /// </summary>
        public static void SolveConstAndEnumValues(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            GeneratorState.ClearConsts();
            GeneratorState.ClearEnums();

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

        private static void SolveConstValues(SourceProductionContext context, IEnumerable<FieldDeclarationSyntax> fields)
        {
            // add built in first id consts
            GeneratorState.AddConst(Helpers.Tk_FirstRpcId, (int)Helpers.FirstRpcId);
            GeneratorState.AddConst(Helpers.Tk_FirstRpcIdWithClass, (int)Helpers.FirstRpcId);
            GeneratorState.AddConst(Helpers.Tk_FirstRpcIdWithNamespace, (int)Helpers.FirstRpcId);
            GeneratorState.AddConst(Helpers.Tk_FirstTypeId, (int)Helpers.FirstTypeId);
            GeneratorState.AddConst(Helpers.Tk_FirstTypeIdWithClass, (int)Helpers.FirstTypeId);
            GeneratorState.AddConst(Helpers.Tk_FirstTypeIdWithNamespace, (int)Helpers.FirstTypeId);

            var names = new List<string>();
            foreach (var field in fields)
            {
                if (GeneratorState.HasConst(Helpers.GetFullName(Helpers.GetFieldName(field), field)))
                    continue;

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
        }

        private static void SolveEnumValues(SourceProductionContext context, IEnumerable<EnumDeclarationSyntax> enums)
        {
            var names = new List<string>();
            foreach (var e in enums)
            {
                if (GeneratorState.HasEnum(Helpers.GetFullName(e.Identifier.ValueText, e)))
                    continue;

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
        }
    }
}