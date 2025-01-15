

using System;
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
        /// Syntax category for any message that communicates a problem 
        /// that WILL prevent the program from working correctly.<br/>
        /// Throw as error.
        /// </summary>
        public const string Cat_Syntax = "Syntax";

        /// <summary>
        /// Usage category for any message that communicates a problem
        /// that COULD prevent the program from working correctly, which will be determined
        /// by later analyzer steps.<br/>
        /// Throw as error.
        /// </summary>
        public const string Cat_Usage = "Usage";

        public enum Ids
        {
            NonVirtualRpc = 1,
            NonVoidRpc,
            StaticRpc,
            BadRpcIdConst,
            BadTypeIdAssignment,
            DuplicateTypeIds,
            BadRpcIdAssignment,
            DuplicateRpcIds,
            MultipleIdRegistries,
            NonStaticRegistry,
            NonEncodableRpcParam,
            NonClientIdRpcCallee,
            NonClientIdRpcCaller,
            UnnecessaryCalleeIdParam
        }

        public static string GetId(Ids id)
        {
            return Symbol + ((int)id).ToString("D3");
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
                m.Identifier.GetLocation(),
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
                m.Identifier.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m));

            context.ReportDiagnostic(diagnostic);
        }

        public static void StaticRpc(SourceProductionContext context, MethodDeclarationSyntax m)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.StaticRpc),
                    "RPC Cannot Be Static",
                    "RPC method '{0}' cannot be static.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                m.Identifier.GetLocation(),
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

        public static void BadTypeIdAssignment(SourceProductionContext context, ClassDeclarationSyntax c, AttributeSyntax attr)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.BadTypeIdAssignment),
                    "Invalid Assign Type Id Value",
                    "NetworkObject type '{0}' can only have its type id assigned with a literal integer, or a constant or enum value from an id registry.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.GetLocation(),
                Helpers.GetFullName(c.Identifier.ValueText, c));

            context.ReportDiagnostic(diagnostic);
        }

        public static void DuplicateTypeIds(SourceProductionContext context, ClassDeclarationSyntax c, byte id, string collision)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.DuplicateTypeIds),
                    "Duplicate Type Ids",
                    "NetworkObject type '{0}' cannot have the same id '{1}' as another NetworkObject type, '{2}'.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                c.Identifier.GetLocation(),
                Helpers.GetFullName(c.Identifier.ValueText, c), id, collision);

            context.ReportDiagnostic(diagnostic);
        }

        public static void BadRpcIdAssignment(SourceProductionContext context, MethodDeclarationSyntax m, AttributeSyntax attr)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.BadRpcIdAssignment),
                    "Invalid Assign RPC Id Value",
                    "RPC method '{0}' can only have its RPC id assigned with a literal integer, or a constant or enum value from an id registry.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m));

            context.ReportDiagnostic(diagnostic);
        }

        public static void DuplicateRpcIds(SourceProductionContext context, MethodDeclarationSyntax m, uint id, string collision)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.DuplicateRpcIds),
                    "Duplicate RPC Ids",
                    "RPC method '{0}' cannot have the same id '{1}' as another RPC, '{2}'.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                m.Identifier.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), id, collision);

            context.ReportDiagnostic(diagnostic);
        }

        public static void MultipleIdRegistries(SourceProductionContext context, ClassDeclarationSyntax c)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.MultipleIdRegistries),
                    "RPC Id Registry Already Exists",
                    "Class '{0}' is labeled as an RPC id registry, but one already exists. Values from this registry will be ignored.",
                    Cat_Usage,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                c.Identifier.GetLocation(),
                Helpers.GetFullName(c.Identifier.ValueText, c));

            context.ReportDiagnostic(diagnostic);
        }

        public static void NonStaticRegistry(SourceProductionContext context, ClassDeclarationSyntax c)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonStaticRegistry),
                    "RPC Id Registry Not Static",
                    "RPC id registry Class '{0}' must be static.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                c.Identifier.GetLocation(),
                Helpers.GetFullName(c.Identifier.ValueText, c));

            context.ReportDiagnostic(diagnostic);
        }

        public static void NonEncodableRpcParam(SourceProductionContext context, MethodDeclarationSyntax m, ParameterSyntax p)
        {
            
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonEncodableRpcParam),
                    "RPC Parameter Is Not Encodable",
                    "RPC method '{0}' has a non-encodable parameter '{1}' of type '{2}'. RPC parameters must all be encodable.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                p.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), p.Identifier.ValueText, p.Type.ToString());

            context.ReportDiagnostic(diagnostic);
        }

        public static void NonClientIdRpcCallee(SourceProductionContext context, MethodDeclarationSyntax m, ParameterSyntax p)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonEncodableRpcParam),
                    "RpcCallee RPC Parameter Is Not ClientId",
                    "RPC method '{0}' has a CalleeId parameter '{1}' which is not of type 'ClientId'. All CalleeId parameters must be of type 'ClientId'.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                p.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), p.Identifier.ValueText);

            context.ReportDiagnostic(diagnostic);
        }

        public static void NonClientIdRpcCaller(SourceProductionContext context, MethodDeclarationSyntax m, ParameterSyntax p)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.NonEncodableRpcParam),
                    "RpcCaller RPC Parameter Is Not ClientId",
                    "RPC method '{0}' has a CallerId parameter '{1}' which is not of type 'ClientId'. All CallerId parameters must be of type 'ClientId'.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                p.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), p.Identifier.ValueText);

            context.ReportDiagnostic(diagnostic);
        }

        internal static void UnnecessaryCalleeIdParam(SourceProductionContext context, MethodDeclarationSyntax m, ParameterSyntax p)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    GetId(Ids.UnnecessaryCalleeIdParam),
                    "Unnecessary Callee Id Param",
                    "RPC method '{0}' has a CalleeId param '{1}', but it can only be sent to the authority. This parameter is redundant.",
                    Cat_Syntax,
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                p.GetLocation(),
                Helpers.GetFullName(m.Identifier.ValueText, m), p.Identifier.ValueText);

            context.ReportDiagnostic(diagnostic);
        }
    }
}