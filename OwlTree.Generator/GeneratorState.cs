
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

        public static void WriteLibraryCache()
        {
            File.WriteAllText(
                Path.GetDirectoryName(CurProjectPath) + "/" + CurProjectName + Helpers.CacheFileType,
                GetLibraryString());
        }

        public static string GetLibraryString()
        {
            var str = new StringBuilder();
            str.Append(GetCacheVersionString());
            str.Append(GetLibraryProjectString());
            str.Append(GetLibraryEncodablesString());
            str.Append(GetLibraryTypeIdsString());
            str.Append(GetLibraryRpcDataString());
            return str.ToString();
        }

        public static string GetCacheString()
        {
            var str = new StringBuilder();
            str.Append(GetCacheVersionString());
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

            if (!IsCurrentCacheVersion(str))
                return;

            FromProjectsString(str);
            FromEncodablesString(str);
            FromConstsString(str);
            FromEnumsString(str);
            FromTypeIdsString(str);
            FromTypeIdString(str);
            FromRpcDataString(str);
            FromRpcIdString(str);
        }

        public static void LoadLibrary(string dllFile, string cacheFile)
        {
            if (!File.Exists(dllFile) || !File.Exists(cacheFile))
                return;

            var str = File.ReadAllText(cacheFile);

            if (!IsCurrentCacheVersion(str))
                return;

            if (FromLibraryProjectString(dllFile, str))
                return;
            var projectId = GetProjectId(dllFile);

            FromLibraryEncodablesString(str, projectId);
            FromLibraryTypeIdsString(str, projectId);
            FromLibraryRpcDataString(str, projectId);
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

        // Cache Version ========================

        public const int CacheVersion = 4;
        const string CacheVersionTag = "<OwlTreeCacheVersion>";
        const string CacheVersionClose = "</OwlTreeCacheVersion>";

        private static string GetCacheVersionString()
        {
            return CacheVersionTag + CacheVersion.ToString() + CacheVersionClose + "\n";
        }

        private static bool IsCurrentCacheVersion(string str)
        {
            var start = str.IndexOf(CacheVersionTag) + CacheVersionTag.Length;
            var end = str.IndexOf(CacheVersionClose);

            if (end == -1)
                return false;

            var subStr = str.Substring(start, end - start);
            return int.Parse(subStr) == CacheVersion;
        }

        // ======================================

        // Project Paths ========================

        public struct ProjectData
        {
            public int id;
            public string path;
            public bool isDll;
            public long timestamp;

            public override string ToString()
            {
                return id.ToString() + ";" + path + ";" + isDll + ";" + timestamp;
            }

            public static ProjectData Parse(string str)
            {
                var tokens = str.Split(';');
                return new ProjectData
                {
                    id = int.Parse(tokens[0]),
                    path = tokens[1].ToLower(),
                    isDll = bool.Parse(tokens[2]),
                    timestamp = long.Parse(tokens[3])
                };
            }
        }

        public static string CachePath = null;
        public static string CacheFile => CachePath + "/" + Helpers.CacheFile;

        public static string CurProjectPath = null;
        public static int CurProjectId = 0;
        public static string CurProjectName => Path.GetFileNameWithoutExtension(CurProjectPath);

        public static bool IsLibraryProject = false;

        static Dictionary<int, ProjectData> _projects = new();

        public static void AddProject(string project)
        {
            _projects[_projects.Count + 1] = new ProjectData
            {
                id = _projects.Count + 1,
                path = project.ToLower(),
                isDll = false,
                timestamp = 0
            };
        }

        public static void AddProject(int id, string project)
        {
            _projects[id] = new ProjectData
            {
                id = id,
                path = project.ToLower(),
                isDll = false,
                timestamp = 0
            };
        }

        public static void AddLibrary(string dll, long timestamp)
        {
            _projects[_projects.Count + 1] = new ProjectData
            {
                id = _projects.Count + 1,
                path = dll.ToLower(),
                isDll = true,
                timestamp = timestamp
            };
        }

        public static void AddLibrary(int id, string dll, long timestamp)
        {
            _projects[id] = new ProjectData
            {
                id = id,
                path = dll.ToLower(),
                isDll = true,
                timestamp = timestamp
            };
        }

        public static bool HasProject(string project) => _projects.Any(p => p.Value.path == project.ToLower());

        public static int GetProjectId(string project) => _projects.Where(p => p.Value.path == project.ToLower()).FirstOrDefault().Key;

        public static ProjectData GetProject(int id) => _projects[id];

        public static ProjectData GetProject(string project) => _projects.Where(p => p.Value.path == project.ToLower()).FirstOrDefault().Value;

        public static IEnumerable<ProjectData> GetProjects() => _projects.Values;

        public static void ClearProjects() => _projects.Clear();

        const string ProjectsTag = "<OwlTreeProjects>";
        const string ProjectsClose = "</OwlTreeProjects>";

        const string LibraryProjectTag = "<OwlTreeLibraryProject>";
        const string LibraryProjectClose = "</OwlTreeLibraryProject>";

        private static string GetProjectsString()
        {
            var str = new StringBuilder(ProjectsTag + "\n");

            foreach (var pair in _projects)
                str.Append(pair.Key + "," + pair.Value + "\n");

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
                var tokens = project.Split(',');
                _projects.Add(int.Parse(tokens[0]), ProjectData.Parse(tokens[1]));
            }
        }

        private static string GetLibraryProjectString()
        {
            return LibraryProjectTag + CurProjectName + ":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + LibraryProjectClose + "\n";
        }

        private static bool FromLibraryProjectString(string dll, string str)
        {
            var start = str.IndexOf(LibraryProjectTag) + LibraryProjectTag.Length;
            var end = str.IndexOf(LibraryProjectClose);

            var subStr = str.Substring(start, end - start);

            var tokens = subStr.Split(':');
            var timestamp = long.Parse(tokens[1]);
            var exists = HasProject(dll) && GetProject(dll).timestamp == timestamp;
            AddLibrary(dll, timestamp);
            return exists;
        }

        // ======================================

        // IEncodable Cache =====================

        static Dictionary<string, (bool isVariable, int projectId)> _encodables = new();

        public static void ClearEncodables() => _encodables.Clear();

        public static void AddEncodable(string k, bool isVariable, int projectId) => _encodables[k] = (isVariable, projectId);

        public static void RemoveEncodable(string k) => _encodables.Remove(k);

        public static void RemoveEncodables(int projectId)
        {
            foreach (var e in GetEncodables(projectId).ToArray())
                RemoveEncodable(e.name);
        }

        public static bool HasEncodable(string k, bool isVariable, int projectId) => _encodables.ContainsKey(k) && _encodables[k] == (isVariable, projectId);

        public static bool HasEncodable(string k) => _encodables.ContainsKey(k);

        public static bool EncodableIsVariable(string k) => _encodables[k].isVariable;

        public static Dictionary<string, (bool isVariable, int projectId)>.Enumerator GetEncodables() => _encodables.GetEnumerator();

        public static IEnumerable<(string name, bool isVariable, int projectId)> GetEncodables(int projectId) =>
            _encodables.Where(p => p.Value.projectId == projectId).Select(p => (p.Key, p.Value.isVariable, p.Value.projectId));

        const string EncodablesTag = "<OwlTreeEncodables true==IVariableLength>";
        const string EncodablesClose = "</OwlTreeEncodables>";

        private static string GetEncodablesString()
        {
            var str = new StringBuilder(EncodablesTag + "\n");

            foreach (var pair in _encodables.Where(p => p.Value.projectId != 0))
                str.Append($"{pair.Key}:{pair.Value.isVariable},{pair.Value.projectId}\n");
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
                var values = tokens[1].Split(',');
                _encodables.Add(tokens[0], (bool.Parse(values[0]),int.Parse(values[1])));
            }
        }

        private static string GetLibraryEncodablesString()
        {
            var str = new StringBuilder(EncodablesTag + "\n");

            foreach (var pair in _encodables.Where(p => p.Value.projectId != 0))
                str.Append($"{pair.Key}:{pair.Value.isVariable}\n");
            str.Append(EncodablesClose + "\n");

            return str.ToString();
        }

        private static void FromLibraryEncodablesString(string str, int projectId)
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
                _encodables.Add(tokens[0], (bool.Parse(tokens[1]),projectId));
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
            public string baseClass;
            public string ns;
            public string[] usings;
            public string[] rpcs;
            public string[] inheritedRpcs;
            public int projectId;

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
                str.Append($"base:{baseClass}\n");
                str.Append($"ns:{ns}\n");
                str.Append($"project:{projectId}\n");

                str.Append("usings:");
                for (int i = 0; i < usings.Length; i++)
                    str.Append(usings[i] + (i < usings.Length - 1 ? "," : "\n"));
                if (usings.Length == 0)
                    str.Append('\n');

                str.Append("rpcs:");
                for (int i = 0; i < rpcs.Length; i++)
                    str.Append(rpcs[i] + (i < rpcs.Length - 1 ? "," : "\n"));
                if (rpcs.Length == 0)
                    str.Append('\n');
                
                str.Append("inherited:");
                for (int i = 0; i < inheritedRpcs.Length; i++)
                    str.Append(inheritedRpcs[i] + (i < inheritedRpcs.Length - 1 ? "," : ""));
                
                return str.ToString();
            }

            public string ToLibraryString()
            {
                var str = new StringBuilder();
                str.Append($"name:{name}\n");
                str.Append($"base:{baseClass}\n");
                str.Append($"ns:{ns}\n");

                str.Append("usings:");
                for (int i = 0; i < usings.Length; i++)
                    str.Append(usings[i] + (i < usings.Length - 1 ? "," : "\n"));
                if (usings.Length == 0)
                    str.Append('\n');

                str.Append("rpcs:");
                for (int i = 0; i < rpcs.Length; i++)
                    str.Append(rpcs[i] + (i < rpcs.Length - 1 ? "," : "\n"));
                if (rpcs.Length == 0)
                    str.Append('\n');
                
                str.Append("inherited:");
                for (int i = 0; i < inheritedRpcs.Length; i++)
                    str.Append(inheritedRpcs[i] + (i < inheritedRpcs.Length - 1 ? "," : ""));
                
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
                        case "base": data.baseClass = tokens[1]; break;
                        case "ns": data.ns = tokens[1]; break;
                        case "usings": data.usings = tokens[1].Split(','); break;
                        case "rpcs": data.rpcs = tokens[1].Split(','); break;
                        case "inherited": data.inheritedRpcs = tokens[1].Split(','); break;
                        case "project": data.projectId = int.Parse(tokens[1]); break;
                    }
                }

                return data;
            }
        }

        static Dictionary<string, TypeData> _types = new();

        public static void ClearTypes() => _types.Clear();

        public static void AddTypeData(string k, TypeData v) => _types[k] = v;

        public static void RemoveTypeData(string k) => _types.Remove(k);

        public static byte[] RemoveTypes(int project)
        {
            var types = GetTypeData(project).OrderBy(d => d.name).ToArray();
            var ids = new byte[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                RemoveTypeData(types[i].name);
                ids[i] = types[i].typeId;
            }
            return ids;
        }

        public static bool HasType(string k) => _types.ContainsKey(k);

        public static bool HasTypeId(byte v) => _types.Any(p => p.Value.typeId == v);

        public static TypeData GetTypeData(string k) => _types[k];

        public static TypeData GetTypeData(byte v) => _types.Where(p => p.Value.typeId == v).FirstOrDefault().Value;

        public static int GetTypesCount() => _types.Count;

        public static IEnumerable<byte> GetTypeIds() => _types.Select(p => p.Value.typeId);

        public static IEnumerable<TypeData> GetTypeData(IEnumerable<int> projects) => _types.Values.Where(t => projects.Contains(t.projectId));

        public static IEnumerable<TypeData> GetTypeData(int project) => _types.Values.Where(t => t.projectId == project);

        public static bool HasTypeData(TypeData data)
        {
            if (!_types.TryGetValue(data.name, out var original))
                return false;

            return data.typeId == original.typeId && data.projectId == original.projectId &&
                data.name == original.name && data.ns == original.ns &&
                Helpers.ArraysEqual(data.usings, original.usings) && Helpers.ArraysEqual(data.rpcs, original.rpcs);
        }

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

        private static string GetLibraryTypeIdsString()
        {
            var str = new StringBuilder(TypesTag + "\n");

            foreach (var pair in _types)
                str.Append($"{pair.Key}=>{pair.Value.ToLibraryString()}<=\n");
            str.Append(TypesClose + "\n");

            return str.ToString();
        }

        private static void FromLibraryTypeIdsString(string str, int projectId)
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
                var data = TypeData.Parse(tokens[1]);
                data.projectId = projectId;
                data.typeId = NextTypeId();
                IncrementTypeId();
                _types.Add(tokens[0], data);
            }
        }

        // =======================================

        // Type Id ===============================

        private static Queue<(byte start, byte end)> _missingTypeIds = new();
        private static (byte start, byte end) _curTypeRange = (0, 0);
        private static byte _lastTypeId = (byte)Helpers.FirstTypeId;

        private static byte _nextTypeId = (byte)Helpers.FirstTypeId;

        public static byte NextTypeId() => _nextTypeId;

        public static void IncrementTypeId()
        {
            _nextTypeId += 1;

            if (_nextTypeId > _lastTypeId)
                return;
            else if (_nextTypeId >= _curTypeRange.end && _missingTypeIds.Count > 0)
            {
                _curTypeRange = _missingTypeIds.Dequeue();
                _nextTypeId = _curTypeRange.start;
            }
            else if (_nextTypeId >= _curTypeRange.end && _missingTypeIds.Count == 0)
            {
                _nextTypeId = _lastTypeId;
            }
        }

        public static void SweepTypeIds()
        {
            if (_types.Count == 0)
                return;

            _missingTypeIds.Clear();
            _curTypeRange = (0, 0);

            var ids = _types.Select(p => p.Value.typeId).OrderBy(id => id);

            byte prevId = (byte)(Helpers.FirstTypeId - 1);
            foreach (var id in ids)
            {
                if (id - prevId > 1)
                    _missingTypeIds.Enqueue(((byte)(prevId + 1), id));
                prevId = id;
            }

            _lastTypeId = (byte)(prevId + 1);

            if (_missingTypeIds.Count > 0 && _curTypeRange == (0, 0))
            {
                _curTypeRange = _missingTypeIds.Dequeue();
                _nextTypeId = _curTypeRange.start;
            }
            else
            {
                _nextTypeId = _lastTypeId;
            }
        }

        public static void ResetTypeId() => _nextTypeId = (byte)Helpers.FirstTypeId;

        const string TypeIdTag = "<OwlTreeTypeId>";
        const string TypeIdClose = "</OwlTreeTypeId>";

        private static string GetTypeIdString()
        {
            return TypeIdTag + Math.Max(_lastTypeId, _nextTypeId).ToString().ToString() + TypeIdClose + "\n";
        }

        private static void FromTypeIdString(string str)
        {
            var start = str.IndexOf(TypeIdTag) + TypeIdTag.Length;
            var end = str.IndexOf(TypeIdClose);

            var subStr = str.Substring(start, end - start);
            _lastTypeId = byte.Parse(subStr);
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
            public int projectId;

            public bool HasSameParams(RpcData data)
            {
                if (paramData.Length != data.paramData.Length)
                    return false;

                for (int i = 0; i < paramData.Length; i++)
                {
                    if (paramData[i].name != data.paramData[i].name ||
                        paramData[i].type != data.paramData[i].type ||
                        paramData[i].isRpcCallee != data.paramData[i].isRpcCallee ||
                        paramData[i].isRpcCaller != data.paramData[i].isRpcCaller)
                    {
                        return false;
                    }
                }

                return true;
            }

            public override string ToString()
            {
                var str = new StringBuilder($"id:{id}\n");
                str.Append($"project:{projectId}\n");
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

            public string ToLibraryString()
            {
                var str = new StringBuilder();
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
                        case "project": data.projectId = int.Parse(tokens[1]); break;
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

        public static void AddRpcData(string k, RpcData v) => _rpcIds[k] = v;

        public static void RemoveRpcData(string k) => _rpcIds.Remove(k);

        public static uint[] RemoveRpcs(int projectId)
        {
            var rpcs = GetRpcs(projectId).OrderBy(d => d.fullName).ToArray();
            var ids = new uint[rpcs.Length];
            for (int i = 0; i < rpcs.Length; i++)
            {
                RemoveRpcData(rpcs[i].fullName);
                ids[i] = rpcs[i].id;
            }
            return ids;
        }

        public static bool HasRpc(string k) => _rpcIds.ContainsKey(k);

        public static bool HasRpcId(string k, uint id) => _rpcIds.Any(p => p.Value.id == id && p.Key != k);

        public static RpcData GetRpcData(string k) => _rpcIds[k];

        public static string GetRpc(RpcData v) => _rpcIds.Where(p => p.Value.id == v.id).FirstOrDefault().Key;

        public static string GetRpc(uint id) => _rpcIds.Where(p => p.Value.id == id).FirstOrDefault().Key;

        public static bool TryGetRpcData(string k, out RpcData v) => _rpcIds.TryGetValue(k, out v);

        public static IEnumerable<RpcData> GetRpcs(IEnumerable<int> projects) => _rpcIds.Values.Where(d => projects.Contains(d.projectId));

        public static IEnumerable<RpcData> GetRpcs(int project) => _rpcIds.Values.Where(d => d.projectId == project);

        public static bool HasRpcData(RpcData data)
        {
            if (!_rpcIds.TryGetValue(data.fullName, out var original))
                return false;

            return data.fullName == original.fullName && data.id == original.id &&
                data.projectId == original.projectId && data.parentClass == original.parentClass &&
                data.perms == original.perms && data.invokeOnCaller == original.invokeOnCaller &&
                data.useTcp == original.useTcp && data.HasSameParams(original);

        }

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

        private static string GetLibraryRpcDataString()
        {
            var str = new StringBuilder(RpcDataTag + "\n");

            foreach (var pair in _rpcIds)
                str.Append($"{pair.Key}=>{pair.Value.ToLibraryString()}<=\n");
            str.Append(RpcDataClose + "\n");

            return str.ToString();
        }

        private static void FromLibraryRpcDataString(string str, int projectId)
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
                var data = RpcData.Parse(tokens[1]);
                data.projectId = projectId;
                data.id = NextRpcId();
                IncrementRpcId();
                _rpcIds.Add(tokens[0], data);
            }
        }

        // =======================================

        // Rpc Id ================================

        private static Queue<(uint start, uint end)> _missingRpcIds = new();
        private static (uint start, uint end) _curRpcRange = (0, 0);
        private static uint _lastRpcId = Helpers.FirstRpcId;

        private static uint _nextRpcId = Helpers.FirstRpcId;

        public static uint NextRpcId() => _nextRpcId;

        public static void IncrementRpcId()
        {
            _nextRpcId += 1;

            if (_nextRpcId > _lastRpcId)
                return;
            else if (_nextRpcId >= _curRpcRange.end && _missingRpcIds.Count > 0)
            {
                _curRpcRange = _missingRpcIds.Dequeue();
                _nextRpcId = _curRpcRange.start;
            }
            else if (_nextRpcId >= _curRpcRange.end && _missingRpcIds.Count == 0)
            {
                _nextRpcId = _lastRpcId;
            }
        }

        public static void SweepRpcIds()
        {
            if (_rpcIds.Count == 0)
                return;

            _missingRpcIds.Clear();
            _curRpcRange = (0, 0);

            var ids = _rpcIds.Select(p => p.Value.id).OrderBy(id => id);

            uint prevId = Helpers.FirstRpcId - 1;
            foreach (var id in ids)
            {
                if (id - prevId > 1)
                    _missingRpcIds.Enqueue((prevId + 1, id));
                prevId = id;
            }

            _lastRpcId = prevId + 1;

            if (_missingRpcIds.Count > 0 && _curRpcRange == (0, 0))
            {
                _curRpcRange = _missingRpcIds.Dequeue();
                _nextRpcId = _curRpcRange.start;
            }
            else
            {
                _nextRpcId = _lastRpcId;
            }
        }

        public static void ResetRpcId() => _nextRpcId = Helpers.FirstRpcId;

        const string RpcIdTag = "<OwlTreeRpcId>";
        const string RpcIdClose = "</OwlTreeRpcId>";

        private static string GetRpcIdString()
        {
            return RpcIdTag + Math.Max(_lastRpcId, _nextRpcId).ToString() + RpcIdClose + "\n";
        }

        private static void FromRpcIdString(string str)
        {
            var start = str.IndexOf(RpcIdTag) + RpcIdTag.Length;
            var end = str.IndexOf(RpcIdClose);

            var subStr = str.Substring(start, end - start);
            _lastRpcId = uint.Parse(subStr);
        }

        // =======================================

        // Usings Cache ==========================
        // used to make sure generated RPC protocols are using the namespaces for all the rpc args

        static Dictionary<int, HashSet<string>> _usings = new();

        public static void ClearUsings()
        {
            _usings.Clear();
            _usings.Add(CurProjectId, new HashSet<string>());
            _usings[CurProjectId].Add(Helpers.Tk_OwlTree);
            _usings[CurProjectId].Add(Helpers.Tk_System);
            _usings[CurProjectId].Add(Helpers.Tk_CompilerServices);
        }

        public static void AddUsing(string u)
        {
            if (!_usings.ContainsKey(CurProjectId))
                _usings.Add(CurProjectId, new HashSet<string>());
            if (!_usings[CurProjectId].Contains(u))
                _usings[CurProjectId].Add(u);
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

            var usings = new UsingDirectiveSyntax[_usings[CurProjectId].Count];
            int i = 0;
            foreach (var u in _usings[CurProjectId])
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

            foreach (var p in _usings)
            {
                str.Append(p.Key.ToString() + ":\n");
                foreach (var u in p.Value)
                    str.Append("  " + u + "\n");
            }

            str.Append(UsingsClose + "\n");
            return str.ToString();
        }

        // =======================================
    }
}