
using System;
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
            File.WriteAllText(CacheFile, GetCacheString());
        }

        public static string GetCacheString()
        {
            var str = new StringBuilder();
            str.Append(GetProjectsString());
            str.Append(GetEncodablesString());
            str.Append(GetConstsString());
            str.Append(GetEnumsString());
            str.Append(GetTypeIdsString());
            str.Append(GetTypeIdString());
            str.Append(GetRpcDataString());
            str.Append(GetRpcIdString());
            str.Append(GetUsingsString());
            return str.ToString();
        }

        public static void LoadCache()
        {
            if (!File.Exists(CacheFile))
                return;
            var str = File.ReadAllText(CacheFile);
            FromProjectsString(str);
            FromEncodablesString(str);
            FromConstsString(str);
            FromEnumsString(str);
            FromTypeIdsString(str);
            FromTypeIdString(str);
            FromRpcDataString(str);
            FromRpcIdString(str);
        }

        public static void ResetCache()
        {
            ClearProjects();
            ClearEncodables();
            ClearConsts();
            ClearEnums();
            ClearTypes();
            ClearRpcData();
            ClearUsings();
            ResetRpcId();
            ResetTypeId();
        }

        public static string CachePath = null;
        public static string CacheFile => CachePath + "/" + Helpers.CacheFile;

        public static bool IsLibraryProject = false;

        static HashSet<string> _projects = new();

        public static void AddProject(string project) => _projects.Add(project.ToLower());

        public static bool HasProject(string project) => _projects.Contains(project.ToLower());

        public static void ClearProjects() => _projects.Clear();

        const string ProjectsTag = "<OwlTreeProjects>";
        const string ProjectsClose = "</OwlTreeProjects>";

        private static string GetProjectsString()
        {
            var str = new StringBuilder(ProjectsTag + "\n");

            foreach (var project in _projects)
                str.Append(project).Append('\n');

            str.Append(ProjectsClose + "\n");
            return str.ToString();
        }

        private static void FromProjectsString(string str)
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

        private static string GetEncodablesString()
        {
            var str = new StringBuilder(EncodablesTag + "\n");

            foreach (var pair in _encodables)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(EncodablesClose + "\n");

            return str.ToString();
        }

        private static void FromEncodablesString(string str)
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

        private static string GetConstsString()
        {
            var str = new StringBuilder(ConstsTag + "\n");

            foreach (var pair in _consts)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(ConstsClose + "\n");

            return str.ToString();
        }

        private static void FromConstsString(string str)
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

        private static string GetEnumsString()
        {
            var str = new StringBuilder(EnumsTag + "\n");

            foreach (var pair in _enums)
                str.Append($"{pair.Key}:{pair.Value}\n");
            str.Append(EnumsClose + "\n");

            return str.ToString();
        }

        private static void FromEnumsString(string str)
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

        public struct TypeData
        {
            public byte typeId;
            public string name;
            public string ns;
            public string[] usings;
            public string[] rpcs;

            public IEnumerable<string> GetFullRpcNames()
            {
                foreach (var rpc in rpcs)
                    yield return name + "." + rpc;
            }

            public string GetFullRpcName(string rpc) => name + "." + rpc;

            public override string ToString()
            {
                var str = new StringBuilder($"id:{typeId}\n");
                str.Append($"name:{name}\n");
                str.Append($"ns:{ns}\n");
                str.Append("usings:");
                for (int i = 0; i < usings.Length; i++)
                    str.Append(usings[i] + (i < usings.Length - 1 ? "," : "\n"));
                str.Append("rpcs:");
                for (int i = 0; i < rpcs.Length; i++)
                    str.Append(rpcs[i] + (i < rpcs.Length - 1 ? "," : ""));
                return str.ToString();
            }

            public static TypeData Parse(string str)
            {
                var fields = str.Split('\n');
                var data = new TypeData();

                foreach (var field in fields)
                {
                    var tokens = field.Split(':');
                    switch (tokens[0])
                    {
                        case "id": data.typeId = byte.Parse(tokens[1]); break;
                        case "name": data.name = tokens[1]; break;
                        case "ns": data.ns = tokens[1]; break;
                        case "usings": data.usings = tokens[1].Split(','); break;
                        case "rpcs": data.rpcs = tokens[1].Split(','); break;
                    }
                }

                return data;
            }
        }

        static Dictionary<string, TypeData> _types = new();

        public static void ClearTypes() => _types.Clear();

        public static void AddTypeData(string k, TypeData v) => _types.Add(k, v);

        public static bool HasType(string k) => _types.ContainsKey(k);

        public static bool HasTypeId(byte v) => _types.Any(p => p.Value.typeId == v);

        public static TypeData GetTypeData(string k) => _types[k];

        public static TypeData GetTypeData(byte v) => _types.Where(p => p.Value.typeId == v).FirstOrDefault().Value;

        public static int GetTypesCount() => _types.Count;

        public static IEnumerable<byte> GetTypeIds() => _types.Select(p => p.Value.typeId);

        public static IEnumerable<TypeData> GetTypeData() => _types.Values;

        const string TypesTag = "<OwlTreeNetworkObjectTypes>";
        const string TypesClose = "</OwlTreeNetworkObjectTypes>";

        private static string GetTypeIdsString()
        {
            var str = new StringBuilder(TypesTag + "\n");

            foreach (var pair in _types)
                str.Append($"{pair.Key}=>{pair.Value}<=\n");
            str.Append(TypesClose + "\n");

            return str.ToString();
        }

        private static void FromTypeIdsString(string str)
        {
            var start = str.IndexOf(TypesTag) + TypesTag.Length + 1;
            var end = str.IndexOf(TypesClose);

            var subStr = str.Substring(start, end - start);
            var types = subStr.Split(new[] { "<=\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var t in types)
            {
                if (string.IsNullOrEmpty(t) || string.IsNullOrWhiteSpace(t))
                    continue;
                var tokens = t.Split(new[] { "=>" }, StringSplitOptions.RemoveEmptyEntries);
                _types.Add(tokens[0], TypeData.Parse(tokens[1]));
            }
        }

        // =======================================

        // Type Id ===============================

        private static byte _nextTypeId = (byte)Helpers.FirstTypeId;

        public static byte NextTypeId() => _nextTypeId;

        public static void IncrementTypeId() => _nextTypeId += 1;

        public static void SetTypeId(byte v) => _nextTypeId = v;

        public static void ResetTypeId() => _nextTypeId = (byte)Helpers.FirstTypeId;

        const string TypeIdTag = "<OwlTreeTypeId>";
        const string TypeIdClose = "</OwlTreeTypeId>";

        private static string GetTypeIdString()
        {
            return TypeIdTag + _nextTypeId.ToString() + TypeIdClose + "\n";
        }

        private static void FromTypeIdString(string str)
        {
            var start = str.IndexOf(TypeIdTag) + TypeIdTag.Length;
            var end = str.IndexOf(TypeIdClose);

            var subStr = str.Substring(start, end - start);
            _nextTypeId = byte.Parse(subStr);
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
                if (tokens.Length == 3)
                {
                    data.isRpcCallee = tokens[2] == "[RpcCallee]";
                    data.isRpcCaller = tokens[2] == "[RpcCaller]";
                }
                else
                {
                    data.isRpcCallee = false;
                    data.isRpcCaller = false;
                }
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
            public string fullName;
            public string parentClass;
            public RpcPerms perms;
            public bool invokeOnCaller;
            public bool useTcp;
            public ParamData[] paramData;

            public override string ToString()
            {
                var str = new StringBuilder($"id:{id}\n");
                str.Append($"name:{name}\n");
                str.Append($"fullName:{fullName}\n");
                str.Append($"class:{parentClass}\n");
                str.Append($"perms:{(int)perms}\n");
                str.Append($"invokeOnCaller:{invokeOnCaller}\n");
                str.Append($"protocol:{(useTcp ? "TCP" : "UDP")}\n");
                str.Append("params:");
                for (int i = 0; i < paramData.Length; i++)
                    str.Append(paramData[i].ToString() + (i == paramData.Length - 1 ? "" : ","));
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
                        case "id": data.id = uint.Parse(tokens[1]); break;
                        case "name": data.name = tokens[1]; break;
                        case "fullName": data.fullName = tokens[1]; break;
                        case "class": data.parentClass = tokens[1]; break;
                        case "perms": data.perms = (RpcPerms)int.Parse(tokens[1]); break;
                        case "invokeOnCaller": data.invokeOnCaller = bool.Parse(tokens[1]); break;
                        case "protocol": data.useTcp = tokens[1] == "TCP"; break;
                        case "params":
                            if (string.IsNullOrEmpty(tokens[1]))
                            {
                                data.paramData = new ParamData[0];
                                break;
                            }
                            var ps = tokens[1].Split(',');
                            data.paramData = new ParamData[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                            {
                                if (!string.IsNullOrEmpty(ps[i]))
                                    data.paramData[i] = ParamData.Parse(ps[i]);
                            }
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

        const string RpcDataTag = "<OwlTreeRpcData>";
        const string RpcDataClose = "</OwlTreeRpcData>";

        private static string GetRpcDataString()
        {
            var str = new StringBuilder(RpcDataTag + "\n");

            foreach (var pair in _rpcIds)
                str.Append($"{pair.Key}=>{pair.Value}<=\n");
            str.Append(RpcDataClose + "\n");

            return str.ToString();
        }

        private static void FromRpcDataString(string str)
        {
            var start = str.IndexOf(RpcDataTag) + RpcDataTag.Length + 1; // +1 to chop off newline after opening tag
            var end = str.IndexOf(RpcDataClose);

            var subStr = str.Substring(start, end - start);
            var rpcs = subStr.Split(new[] { "<=\n" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var rpc in rpcs)
            {
                if (string.IsNullOrEmpty(rpc) || string.IsNullOrWhiteSpace(rpc))
                    continue;
                var tokens = rpc.Split(new[] { "=>" }, System.StringSplitOptions.RemoveEmptyEntries);
                _rpcIds.Add(tokens[0], RpcData.Parse(tokens[1]));
            }
        }

        // =======================================

        // Rpc Id ================================

        private static uint _nextRpcId = Helpers.FirstRpcId;

        public static uint NextRpcId() => _nextRpcId;

        public static void IncrementRpcId() => _nextRpcId += 1;

        public static void SetRpcId(uint v) => _nextRpcId = v;

        public static void ResetRpcId() => _nextRpcId = Helpers.FirstRpcId;

        const string RpcIdTag = "<OwlTreeRpcId>";
        const string RpcIdClose = "</OwlTreeRpcId>";

        private static string GetRpcIdString()
        {
            return RpcIdTag + _nextRpcId.ToString() + RpcIdClose + "\n";
        }

        private static void FromRpcIdString(string str)
        {
            var start = str.IndexOf(RpcIdTag) + RpcIdTag.Length;
            var end = str.IndexOf(RpcIdClose);

            var subStr = str.Substring(start, end - start);
            _nextRpcId = uint.Parse(subStr);
        }

        // =======================================

        // Usings Cache ==========================
        // used to make sure generated RPC protocols are using the namespaces for all the rpc args

        static HashSet<string> _usings = new();

        public static void ClearUsings()
        {
            _usings.Clear();
            _usings.Add(Helpers.Tk_OwlTree);
            _usings.Add(Helpers.Tk_System);
            _usings.Add(Helpers.Tk_CompilerServices);
        }

        public static void AddUsing(string u)
        {
            if (!_usings.Contains(u))
                _usings.Add(u);
        }

        public static void AddUsings(SyntaxList<UsingDirectiveSyntax> usings)
        {
            foreach (var u in usings)
                AddUsing(u.Name.ToString());
        }

        public static UsingDirectiveSyntax[] GetUsings()
        {
            AddUsing(Helpers.Tk_System);
            AddUsing(Helpers.Tk_CompilerServices);
            AddUsing(Helpers.Tk_OwlTree);

            var usings = new UsingDirectiveSyntax[_usings.Count];
            int i = 0;
            foreach (var u in _usings)
            {
                usings[i] = UsingDirective(IdentifierName(u));
                i++;
            }
            return usings;
        }

        const string UsingsTag = "<Usings>";
        const string UsingsClose = "</Usings>";

        private static string GetUsingsString()
        {
            var str = new StringBuilder(UsingsTag + "\n");

            foreach (var u in _usings)
                str.Append(u + "\n");

            str.Append(UsingsClose + "\n");
            return str.ToString();
        }

        // =======================================
    }
}