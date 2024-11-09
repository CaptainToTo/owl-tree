
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OwlTree.Generator
{
    /// <summary>
    /// Generator state cache. Use to manage any state that needs to be shared across 
    /// generator steps, or between compilations.
    /// </summary>
    internal static class GeneratorState
    {
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

        public static string GetConstsString()
        {
            var str = new StringBuilder("solved consts:\n");

            foreach (var pair in _consts)
                str.Append($"{pair.Key} : {pair.Value}\n");

            return str.ToString();
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

        public static string GetEnumsString()
        {
            var str = new StringBuilder("solved enums:\n");

            foreach (var pair in _enums)
                str.Append($"{pair.Key} : {pair.Value}\n");

            return str.ToString();
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

        // Rpc Id Cache ==========================
        
        static Dictionary<string, uint> _rpcIds = new();

        public static void ClearRpcIds() => _rpcIds.Clear();

        public static void AddRpcId(string k, uint v) => _rpcIds.Add(k, v);

        public static bool HasRpc(string k) => _rpcIds.ContainsKey(k);

        public static bool HasRpcId(uint v) => _rpcIds.ContainsValue(v);

        public static uint GetRpcId(string k) => _rpcIds[k];

        public static string GetRpc(uint v) => _rpcIds.Where(p => p.Value == v).FirstOrDefault().Key;

        public static bool TryGetRpcId(string k, out uint v) => _rpcIds.TryGetValue(k, out v);

        public static Dictionary<string, uint>.Enumerator GetRpcIds() => _rpcIds.GetEnumerator();

        public static string GetRpcIdsString()
        {
            var str = new StringBuilder("assigned rpc ids:\n");

            foreach (var pair in _rpcIds)
                str.Append($"{pair.Key} : {pair.Value}\n");

            return str.ToString();
        }

        // =======================================
    }
}