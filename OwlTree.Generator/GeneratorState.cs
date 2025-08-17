
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace OwlTree.Generator
{
    /// <summary>
    /// Generator state cache. Use to manage any state that needs to be shared across 
    /// generator steps, or between compilations.
    /// </summary>
    public static class GeneratorState
    {
        // Multi-project Cache Path =============

        public static void WriteCache()
        {
            var str = new StringBuilder();
            str.Append(GetProjectsString());
            str.Append(GetEncodablesString());
            str.Append(GetConstsString());
            str.Append(GetEnumsString());
            str.Append(GetTypeIdsString());
            str.Append(GetRpcIdsString());

            File.WriteAllText(CachePath, str.ToString());
        }

        public static void LoadCache()
        {
            if (!File.Exists(CachePath))
                return;
            var str = File.ReadAllText(CachePath);
            FromProjectsString(str);
            FromEncodablesString(str);
            FromConstsString(str);
            FromEnumsString(str);
            FromTypeIdsString(str);
            FromRpcIdsString(str);
        }

        public static void ResetCache()
        {
            ClearProjects();
            ClearEncodables();
            ClearConsts();
            ClearEnums();
            ClearTypeIds();
            ClearRpcData();
            ClearUsings();
        }

        public static string CachePath = null;

        static HashSet<string> _projects = new();

        public static void AddProject(string project) => _projects.Add(project);

        public static bool HasProject(string project) => _projects.Contains(project);

        public static void ClearProjects() => _projects.Clear();

        const string ProjectsTag = "<OwlTreeProjects>";
        const string ProjectsClose = "</OwlTreeProjects>";

        public static string GetProjectsString()
        {
            var str = new StringBuilder(ProjectsTag + "\n");

            foreach (var project in _projects)
                str.Append(project).Append('\n');

            str.Append(ProjectsClose + "\n");
            return str.ToString();
        }

        public static void FromProjectsString(string str)
        {
            var start = str.IndexOf(ProjectsTag) + ProjectsTag.Length;
            var end = str.IndexOf(ProjectsClose);

            var subStr = str.Substring(start, end - start);
            var projects = subStr.Split('\n');

            foreach (var project in projects)
            {
                if (string.IsNullOrEmpty(project))
                    continue;
                _projects.Add(project);
            }
        }

        // IEncodable Cache =====================

        static Dictionary<string, bool> _encodables = new();

        public static void ClearEncodables() => _encodables.Clear();

        public static void AddEncodable(string k, bool isVariable) => _encodables.Add(k, isVariable);

        public static bool HasEncodable(string k) => _encodables.ContainsKey(k);

        public static bool EncodableIsVariable(string k) => _encodables[k];

        public static Dictionary<string, bool>.Enumerator GetEncodables() => _encodables.GetEnumerator();

        const string EncodablesTag = "<OwlTreeEncodables true==IVariableLength>";
        const string EncodablesClose = "</OwlTreeEncodables>";

        public static string GetEncodablesString()
        {
            var str = new StringBuilder(EncodablesTag + "\n");

            foreach (var pair in _encodables)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(EncodablesClose + "\n");

            return str.ToString();
        }

        public static void FromEncodablesString(string str)
        {
            var start = str.IndexOf(EncodablesTag) + EncodablesTag.Length;
            var end = str.IndexOf(EncodablesClose);

            var subStr = str.Substring(start, end - start);
            var encodables = subStr.Split('\n');

            foreach (var encodable in encodables)
            {
                if (string.IsNullOrEmpty(encodable))
                    continue;
                var tokens = encodable.Split(':');
                _encodables.Add(tokens[0], bool.Parse(tokens[1]));
            }
        }

        // ======================================

        // Consts Cache =========================

        static Dictionary<string, int> _consts = new();

        public static void ClearConsts() => _consts.Clear();

        public static void AddConst(string k, int v) => _consts.Add(k, v);

        public static bool HasConst(string k) => _consts.ContainsKey(k);

        public static bool HasConstValue(int v) => _consts.ContainsValue(v);

        public static int GetConst(string k) => _consts[k];

        public static string GetConst(int v) => _consts.Where(p => p.Value == v).FirstOrDefault().Key;

        public static bool TryGetConst(string k, out int v) => _consts.TryGetValue(k, out v);

        public static Dictionary<string, int>.Enumerator GetConsts() => _consts.GetEnumerator();

        const string ConstsTag = "<OwlTreeConsts>";
        const string ConstsClose = "</OwlTreeConsts>";

        public static string GetConstsString()
        {
            var str = new StringBuilder(ConstsTag + "\n");

            foreach (var pair in _consts)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(ConstsClose + "\n");

            return str.ToString();
        }

        public static void FromConstsString(string str)
        {
            var start = str.IndexOf(ConstsTag) + ConstsTag.Length;
            var end = str.IndexOf(ConstsClose);

            var subStr = str.Substring(start, end - start);
            var consts = subStr.Split('\n');

            foreach (var c in consts)
            {
                if (string.IsNullOrEmpty(c))
                    continue;
                var tokens = c.Split(':');
                _consts.Add(tokens[0], int.Parse(tokens[1]));
            }
        }

        // ======================================

        // Enum Cache ===========================

        static Dictionary<string, int> _enums = new();

        public static void ClearEnums() => _enums.Clear();

        public static void AddEnum(string k, int v) => _enums.Add(k, v);

        public static bool HasEnum(string k) => _enums.ContainsKey(k);

        public static bool HasEnumValue(int v) => _enums.ContainsValue(v);

        public static int GetEnum(string k) => _enums[k];

        public static string GetEnum(int v) => _enums.Where(p => p.Value == v).FirstOrDefault().Key;

        public static bool TryGetEnum(string k, out int v) => _enums.TryGetValue(k, out v);

        public static Dictionary<string, int>.Enumerator GetEnums() => _consts.GetEnumerator();

        const string EnumsTag = "<OwlTreeEnums>";
        const string EnumsClose = "</OwlTreeEnums>";

        public static string GetEnumsString()
        {
            var str = new StringBuilder(EnumsTag + "\n");

            foreach (var pair in _enums)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(EnumsClose + "\n");

            return str.ToString();
        }

        public static void FromEnumsString(string str)
        {
            var start = str.IndexOf(EnumsTag) + EnumsTag.Length;
            var end = str.IndexOf(EnumsClose);

            var subStr = str.Substring(start, end - start);
            var enums = subStr.Split('\n');

            foreach (var c in enums)
            {
                if (string.IsNullOrEmpty(c))
                    continue;
                var tokens = c.Split(':');
                _enums.Add(tokens[0], int.Parse(tokens[1]));
            }
        }

        // ======================================

        public static bool TryGetConstOrEnum(string k, out int v)
        {
            if (TryGetConst(k, out v))
                return true;
            if (TryGetEnum(k, out v))
                return true;
            return false;
        }

        // Network Object Type Cache =============

        static Dictionary<string, byte> _typeIds = new();

        public static void ClearTypeIds() => _typeIds.Clear();

        public static void AddTypeId(string k, byte v) => _typeIds.Add(k, v);

        public static bool HasType(string k) => _typeIds.ContainsKey(k);

        public static bool HasTypeId(byte v) => _typeIds.ContainsValue(v);

        public static byte GetTypeId(string k) => _typeIds[k];

        public static string GetType(byte v) => _typeIds.Where(p => p.Value == v).FirstOrDefault().Key;

        public static Dictionary<string, byte> GetTypeIds() => _typeIds;

        const string TypesTag = "<OwlTreeNetworkObjectTypes>";
        const string TypesClose = "</OwlTreeNetworkObjectTypes>";

        public static string GetTypeIdsString()
        {
            var str = new StringBuilder(TypesTag + "\n");

            foreach (var pair in _typeIds)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(TypesClose + "\n");

            return str.ToString();
        }

        public static void FromTypeIdsString(string str)
        {
            var start = str.IndexOf(TypesTag) + TypesTag.Length;
            var end = str.IndexOf(TypesClose);

            var subStr = str.Substring(start, end - start);
            var types = subStr.Split('\n');

            foreach (var c in types)
            {
                if (string.IsNullOrEmpty(c))
                    continue;
                var tokens = c.Split(':');
                _typeIds.Add(tokens[0], byte.Parse(tokens[1]));
            }
        }

        // =======================================

        // Rpc Id Cache ==========================

        public struct ParamData
        {
            public string name;
            public string type;
            public bool isRpcCallee;
            public bool isRpcCaller;

            public override string ToString()
            {
                return $"{type} {name}" + (isRpcCallee ? " [RpcCallee]" : "") + (isRpcCaller ? " [RpcCaller]" : "");
            }

            public static ParamData Parse(string str)
            {
                var tokens = str.Split(' ');
                var data = new ParamData();
                data.type = tokens[0];
                data.name = tokens[1];
                data.isRpcCallee = tokens[2] == "[RpcCallee]";
                data.isRpcCaller = tokens[2] == "[RpcCaller]";
                return data;
            }
        }

        public enum RpcPerms
        {
            AuthorityToClients,
            ClientsToAuthority,
            ClientsToClients,
            ClientsToAll,
            AnyToAll
        }

        public class RpcData
        {
            public uint id;
            public string name;
            public string parentClass;
            public RpcPerms perms;
            public bool invokeOnCaller;
            public bool useTcp;
            public ParamData[] paramData;

            public override string ToString()
            {
                var str = new StringBuilder($"id:{id}\n");
                str.Append($"name:{name}\n");
                str.Append($"class:{parentClass}\n");
                str.Append($"perms:{(int)perms}\n");
                str.Append($"invokeOnCaller:{invokeOnCaller}\n");
                str.Append($"protocol:{(useTcp ? "TCP" : "UDP")}\n");
                str.Append("params:");
                foreach (var p in paramData)
                    str.Append(p.ToString() + ",");
                return str.ToString();
            }

            public static RpcData Parse(string str)
            {
                var fields = str.Split('\n');
                var data = new RpcData();

                foreach (var field in fields)
                {
                    var tokens = field.Split(':');

                    switch (tokens[0])
                    {
                        case "name": data.name = tokens[1]; break;
                        case "class": data.parentClass = tokens[1]; break;
                        case "perms": data.perms = (RpcPerms)int.Parse(tokens[1]); break;
                        case "invokeOnCaller": data.invokeOnCaller = bool.Parse(tokens[1]); break;
                        case "protocol": data.useTcp = tokens[1] == "TCP"; break;
                        case "params":
                            var ps = tokens[1].Split(',');
                            data.paramData = new ParamData[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                                data.paramData[i] = ParamData.Parse(ps[i]);
                            break;
                    }
                }

                return data;
            }
        }
        
        static Dictionary<string, RpcData> _rpcIds = new();

        public static void ClearRpcData() => _rpcIds.Clear();

        public static void AddRpcData(string k, RpcData v) => _rpcIds.Add(k, v);

        public static bool HasRpc(string k) => _rpcIds.ContainsKey(k);

        public static bool HasRpcId(uint id) => _rpcIds.Any(p => p.Value.id == id);

        public static RpcData GetRpcData(string k) => _rpcIds[k];

        public static string GetRpc(RpcData v) => _rpcIds.Where(p => p.Value.id == v.id).FirstOrDefault().Key;

        public static string GetRpc(uint id) => _rpcIds.Where(p => p.Value.id == id).FirstOrDefault().Key;

        public static bool TryGetRpcData(string k, out RpcData v) => _rpcIds.TryGetValue(k, out v);

        public static Dictionary<string, RpcData> GetRpcs() => _rpcIds;

        const string RpcIdsTag = "<OwlTreeRpcIds>";
        const string RpcIdsClose = "</OwlTreeRpcIds>";

        public static string GetRpcIdsString()
        {
            var str = new StringBuilder(RpcIdsTag + "\n");

            foreach (var pair in _rpcIds)
                str.Append($"{pair.Key}=>{pair.Value}<=\n");
            str.Append(RpcIdsClose + "\n");

            return str.ToString();
        }

        public static void FromRpcIdsString(string str)
        {
            var start = str.IndexOf(RpcIdsTag) + RpcIdsTag.Length;
            var end = str.IndexOf(RpcIdsClose);

            var subStr = str.Substring(start, end - start);
            var rpcs = subStr.Split(new[] { "<=\n" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var rpc in rpcs)
            {
                var tokens = rpc.Split(new[] { "=>" }, System.StringSplitOptions.RemoveEmptyEntries);
                _rpcIds.Add(tokens[0], RpcData.Parse(tokens[1]));
            }
        }

        // =======================================

        // Usings Cache ==========================
        // used to make sure generated RPC protocols are using the namespaces for all the rpc args

        static Dictionary<string, bool> _usings = new();

        public static void ClearUsings() 
        {
            _usings.Clear();
            _usings.Add(Helpers.Tk_OwlTree, true);
            _usings.Add(Helpers.Tk_System, true);
        }

        public static void AddUsing(UsingDirectiveSyntax u)
        {
            var name = u.Name.ToString();
            if (!_usings.ContainsKey(name))
                _usings.Add(name, true);
        }

        public static void AddUsings(SyntaxList<UsingDirectiveSyntax> usings)
        {
            foreach (var u in usings)
                AddUsing(u);
        }

        public static UsingDirectiveSyntax[] GetUsings()
        {
            var usings = new UsingDirectiveSyntax[_usings.Count];
            int i = 0;
            foreach (var u in _usings.Keys)
            {
                usings[i] = UsingDirective(IdentifierName(u));
                i++;
            }
            return usings;
        }

        // =======================================
    }
}