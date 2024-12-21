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
        // TODO: create cache to allow generator to be applied across multiple projects
        // will allow for easier add-on creation, since pre-built IEncodables, and NetworkObjects from different projects
        // can be analyzed "together".
        // static string outputPath = "";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // cache IEncodable types
            var encodableProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_IEncodable),
                transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var encodableCompilation = context.CompilationProvider.Combine(encodableProvider.Collect());

            context.RegisterSourceOutput(encodableCompilation, IEncodableAnalyzer.CacheEncodables);
            // File.WriteAllText(outputPath + "/encodable-out.txt", GeneratorState.GetEncodablesString());

            // pre-solve const and enum values
            var registryProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.HasAttribute(c.AttributeLists, Helpers.AttrTk_IdRegistry),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var registryCompilation = context.CompilationProvider.Combine(registryProvider.Collect());

            context.RegisterSourceOutput(registryCompilation, ConstAndEnumAnalyzer.SolveConstAndEnumValues);
            // File.WriteAllText(outputPath + "/const-out.txt", GeneratorState.GetConstsString());
            // File.WriteAllText(outputPath + "/enum-out.txt", GeneratorState.GetEnumsString());

            // generate network object proxies
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_NetworkObject),
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            ).Where(m => m is not null);

            var compilation = context.CompilationProvider.Combine(provider.Collect());

            context.RegisterSourceOutput(compilation, GenerateProxies);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            throw new NotImplementedException();
        }

        private void GenerateProxies(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple)
        {
            var (compilation, list) = tuple;

            GeneratorState.ClearTypeIds();
            GeneratorState.ClearRpcData();
            GeneratorState.ClearUsings();

            if (list.Length == 0) return;

            NetworkObjectAnalyzer.AssignTypeIds(context, list);
            // File.WriteAllText(outputPath + "/types-out.txt", GeneratorState.GetTypeIdsString());
            NetworkObjectAnalyzer.AssignRpcIds(context, list);
            // File.WriteAllText(outputPath + "/rpc-out.txt", GeneratorState.GetRpcIdsString());

            ProxyFactoryGenerator.Reset();
            
            foreach (var c in list)
            {
                var proxy = ProxyGenerator.CreateProxy(c);
                ProxyFactoryGenerator.AddClass(c);
                context.AddSource(ProxyGenerator.GetProxyName(c) + Helpers.Tk_CsFile, proxy.ToString());
                // File.WriteAllText("path/" + ProxyGenerator.GetProxyName(c) + Helpers.Tk_DebugFile, proxy.ToString());
            }

            var factory = ProxyFactoryGenerator.GetFactory().NormalizeWhitespace();
            context.AddSource(Helpers.Tk_ProjectProxies + Helpers.Tk_CsFile, factory.ToString());
            // File.WriteAllText("path/" + Helpers.Tk_ProjectProxies + Helpers.Tk_DebugFile, factory.ToString());

            RpcProtocolsGenerator.Reset();

            foreach (var pair in GeneratorState.GetRpcs())
            {
                RpcProtocolsGenerator.AddRpc(pair.Value);
            }

            var protocols = RpcProtocolsGenerator.GetRpcProtocols();
            context.AddSource(Helpers.Tk_ProjectProtocols + Helpers.Tk_CsFile, protocols.ToString());
            // File.WriteAllText(outputPath + "/" + Helpers.Tk_ProjectProtocols + Helpers.Tk_DebugFile, protocols.ToString());

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
}