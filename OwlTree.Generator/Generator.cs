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
            GeneratorState.ClearTypeIds();
            GeneratorState.ClearRpcData();
            GeneratorState.ClearUsings();
            // refill encodables with built in encodable types
            IEncodableAnalyzer.AddPrimitives();
            IEncodableAnalyzer.AddBuiltIns();

            // cache IEncodable types
            var encodableProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax c && Helpers.InheritsFrom(c, Helpers.Tk_IEncodable),
                transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node
            ).Where(n => n is not null);

            var encodableCompilation = context.CompilationProvider.Combine(encodableProvider.Collect());

            context.RegisterSourceOutput(encodableCompilation, IEncodableAnalyzer.CacheEncodables);
            File.WriteAllText(EnvConsts.ProjectPath + "encodable-out.txt", GeneratorState.GetEncodablesString());

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
            var (compilation, list) = tuple;

            NetworkObjectAnalyzer.AssignTypeIds(context, list);
            NetworkObjectAnalyzer.AssignRpcIds(context, list);

            ProxyFactoryGenerator.Reset();
            
            foreach (var c in list)
            {
                var proxy = ProxyGenerator.CreateProxy(c);
                ProxyFactoryGenerator.AddClass(c);
                File.WriteAllText(EnvConsts.ProjectPath + ProxyGenerator.GetProxyName(c) + Helpers.Tk_CsFile, proxy.ToString());
            }

            var factory = ProxyFactoryGenerator.GetFactory().NormalizeWhitespace();
            File.WriteAllText(EnvConsts.ProjectPath + Helpers.Tk_ProjectProxies + Helpers.Tk_CsFile, factory.ToString());

            RpcProtocolsGenerator.Reset();

            foreach (var pair in GeneratorState.GetRpcs())
            {
                RpcProtocolsGenerator.AddRpc(pair.Value);
            }

            var protocols = RpcProtocolsGenerator.GetRpcProtocols();
            File.WriteAllText(EnvConsts.ProjectPath + Helpers.Tk_ProjectProtocols + Helpers.Tk_CsFile, protocols.ToString());
        }

        
    }
}