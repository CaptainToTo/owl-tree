
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OwlTree.Generator
{
    // syntax factory helpers
    public static class Helpers
    {
        // generator consts
        public const uint FIRST_RPC_ID = 10; // ! needs to match RpcId.FIRST_RPC_ID

        // * tokens

        // classes and namespaces
        public const string Tk_OwlTreeNamespace = "OwlTree";
        public const string Tk_NetworkObject = "NetworkObject";
        public const string Tk_ProxySuffix = "Proxy";

        // first rpc id tokens
        public const string Tk_FirstId = "FIRST_RPC_ID";
        public const string Tk_FirstIdWithClass = "RpcId.FIRST_RPC_ID";
        public const string Tk_FirstIdWithNamespace = "OwlTree.RpcId.FIRST_RPC_ID";

        // attributes
        public const string AttrTk_Rpc = "Rpc";
        public const string AttrTk_AssignRpcId = "AssignRpcId";
        public const string AttrTk_RpcIdEnum = "RpcIdEnum";
        public const string AttrTk_RpcIdConst = "RpcIdConst";

        // int types
        public const string Tk_Byte = "byte";
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

        // * helpers

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

        public static string GetFieldName(FieldDeclarationSyntax field)
        {
            return field.Declaration.Variables.First().Identifier.ValueText;
        }

        /// <summary>
        /// Reconstructs full dot-notation member access.
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
            }
        }

        /// <summary>
        /// Tries to parse the variable name and literal value from an integer variable definition.
        /// </summary>
        public static bool TryGetInt(FieldDeclarationSyntax field, List<string> names, out int value)
        {
            var def = field.Declaration.Variables.First();

            GetAllNames(def.Identifier.ValueText, field, names);

            value = 0;

            if (def.Initializer.Value is LiteralExpressionSyntax literal)
            {
                if (literal.IsKind(SyntaxKind.NumericLiteralExpression))
                {
                    value = (int)literal.Token.Value;
                    return true;
                }
                return false;
            }

            return false;
        }

        /// <summary>
        /// Check if the given attribute is in the given node's attributes.
        /// Use one of the tokens <c>Tk_TokenName</c> in this helper class for the attrName.
        /// </summary>
        public static bool HasAttribute(SyntaxList<AttributeListSyntax> attrLists, string attrName)
        {
            return attrLists.Any(attrList => attrList.Attributes.Any(a => a.Name.ToString() == attrName));
        }

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
        /// Tries to get an assigned RPC id value from the given method declaration.
        /// </summary>
        public static int GetAssignedRpcId(AttributeSyntax attr)
        {
            var arg = attr.ArgumentList.Arguments.FirstOrDefault();

            if (arg == null)
            {
                return -1;
            }

            switch (arg.Expression)
            {
                case LiteralExpressionSyntax literal:
                    if (literal != null && literal.IsKind(SyntaxKind.NumericLiteralExpression))
                    {
                        return (int)literal.Token.Value;
                    }
                break;

                case IdentifierNameSyntax identifier:
                    if (OwlTreeGenerator.TryGetConst(identifier.Identifier.ValueText, out var v))
                    {
                        return v;
                    }
                break;

                case CastExpressionSyntax cast:
                    switch (cast.Expression)
                    {
                        case IdentifierNameSyntax identifier:
                            if (OwlTreeGenerator.TryGetConst(identifier.Identifier.ValueText, out v))
                            {
                                return v;
                            }
                        break;

                        case MemberAccessExpressionSyntax access:
                            if (OwlTreeGenerator.TryGetConstOrEnum(access, out v))
                            {
                                return v;
                            }
                        break;
                    }
                break;
            }

            return -1;
        }
    }
}