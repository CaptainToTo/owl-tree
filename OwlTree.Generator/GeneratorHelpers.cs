
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OwlTree.Generator
{
    // syntax factory helpers
    public static class Helpers
    {
        // generator consts
        public const uint FirstRpcId = 30; // ! needs to match RpcId.FirstRpcId
        public const uint FirstTypeId = 2; // ! needs to match NetworkObject.FirstTypeId
        public const byte FirstNetworkTypeId = 2; // ! needs to match NetworkSpawner.FirstNetworkTypeId

        // ! must match defaults on RpcAttribute
        public const GeneratorState.RpcPerms RpcCallerDefault = GeneratorState.RpcPerms.AnyToAll;
        public const bool RpcInvokeOnCallerDefault = false;
        public const bool RpcUseTcpDefault = true;

        // * tokens

        // classes and namespaces
        public const string Tk_CompilerServices = "System.Runtime.CompilerServices"; // for the compiler generated attribute
        public const string Tk_System = "System";
        public const string Tk_OwlTree = "OwlTree";
        public const string Tk_NetworkObject = "NetworkObject";
        public const string Tk_ProxySuffix = "Proxy";
        public const string Tk_CsFile = ".g.cs";
        public const string Tk_DebugFile = ".g.cs.debug";
        public const string Tk_IEncodable = "IEncodable";
        public const string Tk_IVariable = "IVariable";
        public const string Tk_ProxyFactory = "ProxyFactory";
        public const string Tk_ProjectProxies = "ProjectProxyFactory";
        public const string Tk_RpcProtocols = "RpcProtocols";
        public const string Tk_ProjectProtocols = "ProjectRpcProtocols";
        public const string Tk_NetworkSpawner = "NetworkSpawner";

        public const string Tk_RpcId = "RpcId";
        public const string Tk_ClientId = "ClientId";
        public const string Tk_AppId = "AppId";
        public const string Tk_NetworkId = "NetworkId";

        public const string Tk_NetworkBitSet = "NetworkBitSet";
        public const string Tk_NetworkDict = "NetworkDict";
        public const string Tk_NetworkList = "NetworkList";
        public const string Tk_NetworkString = "NetworkString";
        public const string Tk_NetworkVec2 = "NetworkVec2";
        public const string Tk_NetworkVec3 = "NetworkVec3";
        public const string Tk_NetworkVec4 = "NetworkVec4";

        // rpc protocols tokens
        // ! must match members of RpcProtocols abstract class
        public const string Tk_GetProtocol = "GetProtocol";
        public const string Tk_GetCalleeIdParam = "GetCalleeIdParam";
        public const string Tk_GetCallerIdParam = "GetCallerIdParam";
        public const string Tk_GetRpcPerms = "GetRpcPerms";
        public const string Tk_GetRpcName = "GetRpcName";
        public const string Tk_GetRpcParamName = "GetRpcParamName";
        public const string Tk_GetSendProtocol = "GetSendProtocol";
        public const string Tk_IsInvokeOnCaller = "IsInvokeOnCaller";
        public const string Tk_InvokeRpc = "InvokeRpc";
        public const string Tk_GetRpcIds = "GetRpcIds";

        public const string ArgTk_RpcId = "rpcId";
        public const string ArgTk_ParamInd = "paramInd";
        public const string ArgTk_Target = "target";
        public const string ArgTk_Args = "args";

        // proxy factory tokens
        // ! must match members of ProxyFactory abstract class
        public const string Tk_GetTypeIds = "GetTypeIds";
        public const string Tk_CreateProxy = "CreateProxy";
        public const string Tk_HasTypeId = "HasTypeId";
        public const string Tk_TypeFromId = "TypeFromId";
        public const string Tk_TypeId = "TypeId";

        // owl tree member accessors
        public const string MTk_Connection = "Connection";
        public const string MTk_LocalId = "LocalId";
        public const string MTk_ConnectionProtocols = "Protocols";
        public const string MTk_CanCallRpc = "CanCallRpc";
        public const string MTk_Id = "Id";
        public const string MTk_IsActive = "IsActive";
        public const string MTk_NetRole = "NetRole";
        public const string MTk_ReceivingRpc = "i_ReceivingRpc";
        public const string MTk_OnRpcCall = "i_OnRpcCall";
        public const string MTk_None = "None";

        public const string MTk_NetworkBaseTypeId = "NetworkBaseTypeId";

        // first id tokens
        public const string Tk_FirstRpcId = "FirstRpcId";
        public const string Tk_FirstRpcIdWithClass = "RpcId.FirstRpcId";
        public const string Tk_FirstRpcIdWithNamespace = "OwlTree.RpcId.FirstRpcId";

        public const string Tk_FirstTypeId = "FirstTypeId";
        public const string Tk_FirstTypeIdWithClass = "NetworkObject.FirstTypeId";
        public const string Tk_FirstTypeIdWithNamespace = "OwlTree.NetworkObject.FirstTypeId";

        // attributes
        public const string AttrTk_Rpc = "Rpc";
        public const string AttrTk_AssignRpcId = "AssignRpcId";
        public const string AttrTk_IdRegistry = "IdRegistry";
        public const string AttrTk_RpcCallerId = "CallerId";
        public const string AttrTk_RpcCalleeId = "CalleeId";
        public const string AttrTk_AssignTypeId = "AssignTypeId";

        public const string AttrTk_CompilerGenerated = "CompilerGenerated";

        // rpc attr args
        public const string Tk_RpcPerms = "RpcPerms";
        public const string Tk_AuthorityCaller = "AuthorityToClients";
        public const string Tk_ClientCaller = "ClientsToAuthority";
        public const string Tk_ClientToClient = "ClientsToClients";
        public const string Tk_ClientToAll = "ClientsToAll";
        public const string Tk_AnyCaller = "AnyToAll";

        public const string Tk_InvokeOnCaller = "InvokeOnCaller";

        public const string Tk_RpcProtocol = "RpcProtocol";
        public const string Tk_Protocol = "Protocol";
        public const string Tk_TcpProtocol = "Tcp";
        public const string Tk_UdpProtocol = "Udp";

        // int types
        public const string Tk_Byte = "byte";
        public const string Tk_Bool = "bool";
        public const string Tk_UShort = "ushort";
        public const string Tk_UInt16 = "UInt16";
        public const string Tk_Short = "short";
        public const string Tk_Int16 = "Int16";
        public const string Tk_UInt = "uint";
        public const string Tk_UInt32 = "UInt32";
        public const string Tk_Int = "int";
        public const string Tk_Int32 = "Int32";
        public const string Tk_ULong = "ulong";
        public const string Tk_UInt64 = "UInt64";
        public const string Tk_Long = "long";
        public const string Tk_Int64 = "Int64";

        public const string Tk_Float = "float";
        public const string Tk_Double = "double";
        public const string Tk_String = "string";

        // * helpers
        
        /// <summary>
        /// Checks if the given method is virtual.
        /// </summary>
        public static bool IsVirtual(MethodDeclarationSyntax m)
        {
            return m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.VirtualKeyword));
        }

        /// <summary>
        /// Checks if the given method has no return type.
        /// </summary>
        public static bool IsProcedure(MethodDeclarationSyntax m)
        {
            return m.ReturnType.GetFirstToken().IsKind(SyntaxKind.VoidKeyword);
        }

        /// <summary>
        /// Checks if the given base class name is in the given class declaration's base list.
        /// </summary>
        public static bool InheritsFrom(TypeDeclarationSyntax c, string baseClass)
        {
            return c.BaseList?.Types.Any(t => t.Type is IdentifierNameSyntax idn && idn.Identifier.ValueText == baseClass) ?? false;
        }

        /// <summary>
        /// Checks if the given member is static.
        /// </summary>
        public static bool IsStatic(MemberDeclarationSyntax c)
        {
            return c.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword));
        }

        /// <summary>
        /// Checks if the given field is a constant.
        /// </summary>
        public static bool IsConst(FieldDeclarationSyntax field)
        {
            return field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
        }

        /// <summary>
        /// Checks if the given field is an integer type: byte, ushort, short, uint, int, ulong, long,
        /// or their system struct equivalents.
        /// </summary>
        public static bool IsInt(FieldDeclarationSyntax field)
        {
            var typeName = field.Declaration.Type.ToString();
            return typeName == Tk_Byte || 
                typeName == Tk_UShort  || typeName == Tk_Short || typeName == Tk_UInt16 || typeName == Tk_Int16 ||
                typeName == Tk_UInt    || typeName == Tk_Int   || typeName == Tk_UInt32 || typeName == Tk_Int32 ||
                typeName == Tk_ULong   || typeName == Tk_Long  || typeName == Tk_UInt64 || typeName == Tk_Int64;
        }

        /// <summary>
        /// Gets the name of the given variable.
        /// </summary>
        public static string GetFieldName(FieldDeclarationSyntax field)
        {
            return field.Declaration.Variables.First().Identifier.ValueText;
        }

        /// <summary>
        /// Reconstructs full dot-notation member access string.
        /// </summary>
        public static string GetAccessorString(MemberAccessExpressionSyntax access)
        {
            var name = new StringBuilder(access.Name.ToString());
            var expression = access.Expression;
            while (expression is MemberAccessExpressionSyntax nextAccess)
            {
                name.Insert(0, ".").Insert(0, nextAccess.Name.ToString());
                expression = nextAccess.Expression;
            }

            if (expression is IdentifierNameSyntax identifier)
            {
                name.Insert(0, ".").Insert(0, identifier.Identifier.ValueText);
            }

            return name.ToString();
        }

        /// <summary>
        /// Fills the names list all versions of the accessor strings that could be used to access the given node.
        /// The token argument is the name of that node, used to start the accessor chain.<br />
        /// The node "MyVar", will create: "MyVar", "MyClass.MyVar", "MyNamespace.MyClass.MyVar", etc.
        /// </summary>
        public static void GetAllNames(string token, SyntaxNode node, List<string> names)
        {
            var name = new StringBuilder(token);
            names.Add(name.ToString());
            
            var parents = node.Ancestors().OfType<ClassDeclarationSyntax>();

            foreach (var parent in parents)
            {
                name.Insert(0, ".").Insert(0, parent.Identifier.ValueText);
                names.Add(name.ToString());
            }

            var space = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (space != null)
            {
                name.Insert(0, ".").Insert(0, space.Name);
                names.Add(name.ToString());
            }
            var fileSpace = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (fileSpace != null)
            {
                name.Insert(0, ".").Insert(0, fileSpace.Name);
                names.Add(name.ToString());
            }
        }

        /// <summary>
        /// Gets the full identifier of the given node. The full dot-notation, of all
        /// parenting classes, and namespace.
        /// </summary>
        public static string GetFullName(string token, SyntaxNode node)
        {
            var name = new StringBuilder(token);
            var parents = node.Ancestors().OfType<ClassDeclarationSyntax>();
            foreach (var parent in parents)
            {
                name.Insert(0, ".").Insert(0, parent.Identifier.ValueText);
            }
            var space = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (space != null)
            {
                name.Insert(0, ".").Insert(0, space.Name);
            }
            var fileSpace = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (fileSpace != null)
            {
                name.Insert(0, ".").Insert(0, fileSpace.Name);
            }
            return name.ToString();
        }

        /// <summary>
        /// Gets the full name of the class the given method is a member of.
        /// </summary>
        public static string GetParentClassName(MethodDeclarationSyntax m)
        {
            var parent = m.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            return GetFullName(parent.Identifier.ValueText, parent);
        }

        /// <summary>
        /// Returns the namespace this node is declared under, or null.
        /// </summary>
        public static NamespaceDeclarationSyntax GetNamespace(SyntaxNode node)
        {
            return node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        }

        /// <summary>
        /// Returns the namespace this node is declared under, or null.
        /// </summary>
        public static FileScopedNamespaceDeclarationSyntax GetFileNamespace(SyntaxNode node)
        {
            return node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        }

        public static string GetNamespaceName(SyntaxNode node)
        {
            var space = GetNamespace(node);
            if (space != null)
            {
                return space.Name.ToString();
            }
            else
            {
                var fileSpace = GetFileNamespace(node);
                if (fileSpace != null)
                {
                    return fileSpace.Name.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the literal value from an integer variable definition.
        /// Verify this field is an integer first with <c>IsInt()</c>.
        /// </summary>
        public static int GetInt(FieldDeclarationSyntax field)
        {
            var def = field.Declaration.Variables.First();

            if (def.Initializer.Value is LiteralExpressionSyntax literal)
            {
                if (literal.IsKind(SyntaxKind.NumericLiteralExpression))
                {
                    return (int)literal.Token.Value;
                }
                return -1;
            }

            return -1;
        }

        /// <summary>
        /// Check if the given attribute is in the given node's attributes.
        /// Use one of the tokens <c>Tk_TokenName</c> in this helper class for the attrName.
        /// </summary>
        public static bool HasAttribute(SyntaxList<AttributeListSyntax> attrLists, string attrName)
        {
            return attrLists.Any(attrList => attrList.Attributes.Any(a => a.Name.ToString() == attrName));
        }

        /// <summary>
        /// Retrieves an attribute from the given attributes.
        /// Use one of the tokens <c>Tk_TokenName</c> in this helper class for the attrName.
        /// </summary>
        public static AttributeSyntax GetAttribute(SyntaxList<AttributeListSyntax> attrLists, string attrName)
        {
            foreach (var attrList in attrLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    if (attr.Name.ToString() == attrName)
                        return attr;
                }
            }
            return null;
        }

        /// <summary>
        /// Tries to get an assigned RPC id value from the given attribute.
        /// This attribute should be an AssignRpcIdAttribute or AssignTypeIdAttribute.
        /// </summary>
        public static int GetAssignedId(AttributeSyntax attr)
        {
            var arg = attr.ArgumentList.Arguments.FirstOrDefault();

            if (arg == null)
                return -1;

            switch (arg.Expression)
            {
                // AssignRpcId(10)
                case LiteralExpressionSyntax literal:
                    if (literal != null && literal.IsKind(SyntaxKind.NumericLiteralExpression))
                        return (int)literal.Token.Value;
                break;

                // AssignRpcId(MyConst)
                case IdentifierNameSyntax identifier:
                    if (GeneratorState.TryGetConst(identifier.Identifier.ValueText, out var v))
                        return v;
                break;

                // AssignRpcId(MyClass.MyConst)
                case MemberAccessExpressionSyntax access:
                    if (GeneratorState.TryGetConstOrEnum(GetAccessorString(access), out v))
                        return v;
                break;

                // AssignRpcId((int)MyClass.MyEnum.Val1)
                case CastExpressionSyntax cast:
                    switch (cast.Expression)
                    {
                        case IdentifierNameSyntax identifier:
                            if (GeneratorState.TryGetConst(identifier.Identifier.ValueText, out v))
                                return v;
                        break;

                        case MemberAccessExpressionSyntax access:
                            if (GeneratorState.TryGetConstOrEnum(GetAccessorString(access), out v))
                                return v;
                        break;
                    }
                break;
            }

            return -1;
        }

        /// <summary>
        /// Checks if a given token defines a member scope (public, private, etc).
        /// </summary>
        public static bool IsScopeToken(SyntaxToken t)
        {
            return t.IsKind(SyntaxKind.PublicKeyword) || t.IsKind(SyntaxKind.PrivateKeyword) ||
                t.IsKind(SyntaxKind.ProtectedKeyword) || t.IsKind(SyntaxKind.InterfaceKeyword);
        }

        /// <summary>
        /// Gets the scope for the given method (public, private, etc).
        /// </summary>
        public static SyntaxKind GetMethodScope(MethodDeclarationSyntax m)
        {
            var found = m.Modifiers.Where(mod => IsScopeToken(mod)).FirstOrDefault().Kind();
            if (found == SyntaxKind.None)
                return SyntaxKind.PrivateKeyword;
            return found;
        }

        public static string RemoveTemplateType(string tStr)
        {
            var ind = tStr.IndexOf('<');
            return tStr.Substring(0, ind == -1 ? tStr.Length : ind);
        }

        /// <summary>
        /// Checks if all the parameters of a method have an encodable type.
        /// If a parameter is found that isn't encodable, returns that parameter in the err argument.
        /// err = 0 for success, err = 1 for non-encodable, err = 2 for non-ClientId RpcCallee, err = 3 for non-ClientId RpcCaller.
        /// </summary>
        public static bool IsEncodable(ParameterListSyntax paramList, out int err, out ParameterSyntax pErr, out ParameterSyntax calleeId, out ParameterSyntax callerId)
        {
            calleeId = null;
            callerId = null;
            foreach (var p in paramList.Parameters)
            {
                if (!GeneratorState.HasEncodable(RemoveTemplateType(p.Type.ToString())))
                {
                    pErr = p;
                    err = 1;
                    return false;
                }
                else if (HasAttribute(p.AttributeLists, AttrTk_RpcCalleeId))
                {
                    calleeId = p;
                    if (!IsClientId(p))
                    {
                        pErr = p;
                        err = 2;
                        return false;
                    }
                }
                else if (HasAttribute(p.AttributeLists, AttrTk_RpcCallerId))
                {
                    callerId = p;
                    if (!IsClientId(p))
                    {
                        pErr = p;
                        err = 3;
                        return false;
                    }
                }
            }
            pErr = null;
            err = 0;
            return true;
        }

        /// <summary>
        /// Checks if the given parameter is a client id.
        /// </summary>
        public static bool IsClientId(ParameterSyntax p)
        {
            return p.Type.ToString() == Tk_ClientId || p.Type.ToString() == Tk_OwlTree + "." + Tk_ClientId;
        }

        public static bool IsClientId(string type)
        {
            return type == Tk_ClientId || type == Tk_OwlTree + "." + Tk_ClientId;
        }

        /// <summary>
        /// Parses the arguments given for the RPC attribute.
        /// </summary>
        public static void GetRpcAttrArgs(AttributeSyntax a, out GeneratorState.RpcPerms caller, out bool invokeOnCaller, out bool useTcp)
        {
            invokeOnCaller = RpcInvokeOnCallerDefault;
            useTcp = RpcUseTcpDefault;

            if (a.ArgumentList == null)
            {
                caller = RpcCallerDefault;
                return;
            }

            var args = a.ArgumentList.Arguments;

            if (args.Count == 0)
            {
                caller = RpcCallerDefault;
                return;
            }

            caller = GetCallerArg(args[0]);

            if (args.Count == 1)
            {
                return;
            }

            foreach (var arg in args)
            {
                if (arg.NameEquals == null) continue;

                if (arg.NameEquals.Name.Identifier.ValueText == Tk_InvokeOnCaller)
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                        invokeOnCaller = literal.Token.ValueText == "true";
                }
                else if (arg.NameEquals.Name.Identifier.ValueText == Tk_RpcProtocol)
                {
                    if (arg.Expression is MemberAccessExpressionSyntax access)
                        useTcp = access.Name.ToString() == Tk_TcpProtocol;
                }
            }
        }

        public static GeneratorState.RpcPerms GetCallerArg(AttributeArgumentSyntax arg)
        {
            var access = (MemberAccessExpressionSyntax)arg.Expression;
            switch (access.Name.ToString())
            {
                case Tk_AuthorityCaller: return GeneratorState.RpcPerms.AuthorityToClients;
                case Tk_ClientCaller: return GeneratorState.RpcPerms.ClientsToAuthority;
                case Tk_ClientToClient: return GeneratorState.RpcPerms.ClientsToClients;
                case Tk_ClientToAll: return GeneratorState.RpcPerms.ClientsToAll;
                case Tk_AnyCaller: 
                default: 
                    return GeneratorState.RpcPerms.AnyToAll;
            }
        }

        /// <summary>
        /// Gets all of the using directives that are in the file this node originates from.
        /// </summary>
        public static SyntaxList<UsingDirectiveSyntax> GetAllUsings(SyntaxNode node)
        {
            return node.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault().Usings;
        }

        public static bool IsUsing(SyntaxList<UsingDirectiveSyntax> usings, string directive)
        {
            return usings.Any(u => u.Name.ToString() == directive);
        }
    }
}