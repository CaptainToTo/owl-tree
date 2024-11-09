

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OwlTree.Generator
{
    /// <summary>
    /// Use to output any compiler diagnostics messages.
    /// </summary>
    public static class Diagnostics
    {

        public const string Symbol = "OWL";

        /// <summary>
        /// Syntax category for any diagnostic message that communicates a problem 
        /// that will prevent the program from working correctly.
        /// </summary>
        public const string Cat_Syntax = "Syntax";

        public enum Ids
        {
            NonVirtualRpc = 1,
            NonVoidRpc,
            BadRpcIdConst,
            BadRpcIdAssignment,
            DuplicateRpcIds
        }

        public static string GetId(Ids id)
        {
            return Symbol + id.ToString("D4");
        }

        public static void NonVirtualRpc(SourceProductionContext context, MethodDeclarationSyntax m)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonVirtualRpc),
                    "RPC Must Be Virtual",
                    "RPC method '{0}' must be virtual for RPC protocol to be generated properly.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                m.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m));

            context.ReportDiagnostic(diagnostic);
        }

        public static void NonVoidRpc(SourceProductionContext context, MethodDeclarationSyntax m)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonVoidRpc),
                    "RPC Must Return Void",
                    "RPC method '{0}' cannot have a return type.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                m.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m));

            context.ReportDiagnostic(diagnostic);
        }

        public static void BadRpcIdConst(SourceProductionContext context, FieldDeclarationSyntax field)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.BadRpcIdConst),
                    "RPC Id Consts Must Be Const Ints",
                    "RPC const '{0}' must be a const integer.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                field.GetLocation(),
                Helpers.GetFieldName(field));

            context.ReportDiagnostic(diagnostic);
        }

        public static void BadRpcIdAssignment(SourceProductionContext context, MethodDeclarationSyntax m, AttributeSyntax attr)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.BadRpcIdAssignment),
                    "Invalid Assign RPC Id Value",
                    "RPC method '{0}' can only have its RPC id assigned with a literal integer, a constant with the '{1}' attribute, or an enum value with the '{2}' attribute.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), Helpers.AttrTk_RpcIdConst, Helpers.AttrTk_RpcIdEnum);

            context.ReportDiagnostic(diagnostic);
        }

        public static void DuplicateRpcIds(SourceProductionContext context, MethodDeclarationSyntax m, uint id)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.DuplicateRpcIds),
                    "Duplicate RPC Ids",
                    "RPC method '{0}' cannot have the same id '{1}' as another RPC.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                m.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), id);

            context.ReportDiagnostic(diagnostic);
        }
    }
}