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
            // cache IEncodable types
            var encodableProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_IEncodable),
                transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var encodableCompilation = context.CompilationProvider.Combine(encodableProvider.Collect());

            context.RegisterSourceOutput(encodableCompilation, IEncodableAnalyzer.CacheEncodables);

            // pre-solve const and enum values
            var registryProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_IdRegistry),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var registryCompilation = context.CompilationProvider.Combine(registryProvider.Collect());

            context.RegisterSourceOutput(registryCompilation, ConstAndEnumAnalyzer.SolveConstAndEnumValues);

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
            try
            {
                var (compilation, list) = tuple;

                CacheFinder.GetCache(compilation);

                NetworkObjectAnalyzer.AssignTypeIds(context, list);
                NetworkObjectAnalyzer.AssignRpcIds(context, list);

                if (list.Length == 0)
                {
                    GeneratorState.WriteCache();

                    var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "OwlTree",
                        "Source Generation Complete",
                        "Generator complete.",
                        "Completion",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true), null);

                    context.ReportDiagnostic(diagnostic);

                    return;
                }

                if (GeneratorState.IsLibraryProject)
                {
                    GeneratorState.WriteCache();

                    var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "OwlTree",
                        "Source Generation Complete",
                        "Generator complete.",
                        "Completion",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true), null);

                    context.ReportDiagnostic(diagnostic);

                    return;
                }

                ProxyFactoryGenerator.Reset();

                var includes = CacheFinder.GetIncludedProjects(GeneratorState.CurProjectPath);

                foreach (var c in GeneratorState.GetTypeData(includes))
                {
                    var proxy = ProxyGenerator.CreateProxy(c);
                    ProxyFactoryGenerator.AddClass(c);
                    context.AddSource(ProxyGenerator.GetProxyName(c) + Helpers.Tk_CsFile, proxy.ToString());
                }

                var factory = ProxyFactoryGenerator.GetFactory().NormalizeWhitespace();
                context.AddSource(Helpers.Tk_ProjectProxies + Helpers.Tk_CsFile, factory.ToString());

                RpcProtocolsGenerator.Reset();

                foreach (var data in GeneratorState.GetRpcs(includes))
                {
                    RpcProtocolsGenerator.AddRpc(data);
                }

                var protocols = RpcProtocolsGenerator.GetRpcProtocols();
                context.AddSource(Helpers.Tk_ProjectProtocols + Helpers.Tk_CsFile, protocols.ToString());

                GeneratorState.WriteCache();

                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "OwlTree",
                            "Source Generation Complete",
                            "Generator complete.",
                            "Completion",
                            DiagnosticSeverity.Info,
                            isEnabledByDefault: true), null);

                    context.ReportDiagnostic(diagnostic);
                }
            }
            catch (Exception e)
            {
                Diagnostics.GeneratorException(e);
            }
        }

        
    }
}