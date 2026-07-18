using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// View (MonoBehaviour) のフィールドを reflection で逆引きし、UI GameObject → "ViewType/fieldName" の安定 ID 辞書を作る
    /// 読む範囲は明示宣言されたフィールドのみ: [SerializeField] (Inspector 配線) と [UiNodeSource] (動的生成の実行時フィールド)。
    /// Dictionary はキーがそのまま意味的 ID になる (例: keywordButtons[emotion])
    /// </summary>
    public static class UiViewIdMap
    {

        // reflection コスト削減用 (型ごとの対象フィールドは実行中不変)
        private static readonly Dictionary<System.Type, (FieldInfo[] Serialized, FieldInfo[] Runtime)> FieldCache = new();

        public static IReadOnlyDictionary<GameObject, string> Build()
        {
            if (string.IsNullOrEmpty(NoemaConfig.ProjectAssemblyPrefix))
                throw new System.InvalidOperationException("NoemaConfig.ProjectAssemblyPrefix が未設定 (利用側プロジェクトのアセンブリ接頭辞を起動時に設定すること)");
            var map = new Dictionary<GameObject, string>();
            var behaviours = new List<(MonoBehaviour Behaviour, FieldInfo[] Serialized, FieldInfo[] Runtime)>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var type = behaviour.GetType();
                if (!IsProjectAssembly(type)) continue;
                var (serialized, runtime) = FieldsOf(type);
                behaviours.Add((behaviour, serialized, runtime));
            }
            // Inspector 配線 (serialized) を全 View 分先に登録し切る。behaviour ごとの交互登録だと FindObjectsByType の不定順で優先が崩れる
            foreach (var (behaviour, serialized, _) in behaviours)
                foreach (var field in serialized) Register(map, field.GetValue(behaviour), IdOf(behaviour.GetType(), field));
            foreach (var (behaviour, _, runtime) in behaviours)
                foreach (var field in runtime) Register(map, field.GetValue(behaviour), IdOf(behaviour.GetType(), field));
            return map;
        }

        internal static bool IsProjectAssembly(System.Type type)
        {
            return type.Assembly.GetName().Name is { } name && name.StartsWith(NoemaConfig.ProjectAssemblyPrefix, System.StringComparison.Ordinal);
        }

        // 実行時フィールドの "_" プレフィックスは ID に含めない (ReportView/_keywordButtons → ReportView/keywordButtons)
        private static string IdOf(System.Type type, FieldInfo field)
        {
            return $"{type.Name}/{field.Name.TrimStart('_')}";
        }

        private static (FieldInfo[], FieldInfo[]) FieldsOf(System.Type viewType)
        {
            if (FieldCache.TryGetValue(viewType, out var cached)) return cached;
            var serialized = new List<FieldInfo>();
            var runtime = new List<FieldInfo>();
            // GetFields は基底の private を返さないため、継承チェーンを自前で遡る
            for (var type = viewType; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) serialized.Add(field);
                    // 実行時フィールドは暗黙推定せず、[UiNodeSource] の明示宣言のみ読む
                    else if (field.GetCustomAttribute<UiNodeSourceAttribute>() != null) runtime.Add(field);
                }
            }
            var result = (serialized.ToArray(), runtime.ToArray());
            FieldCache[viewType] = result;
            return result;
        }

        private static void Register(Dictionary<GameObject, string> map, object value, string id)
        {
            switch (value)
            {
                // fake-null (未アサイン/破棄済み) を先に落とす。Transform 等が IEnumerable ケースへ落ちて列挙時に例外になるのを防ぐ
                case Object unityObject when unityObject == null:
                    break;
                case Component component:
                    map.TryAdd(component.gameObject, id);
                    break;
                case GameObject go:
                    map.TryAdd(go, id);
                    break;
                // Dictionary<TKey, Button/GameObject> はキーを意味的 ID として使う (IEnumerable より先に判定する)
                case IDictionary dictionary:
                    {
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            if (entry.Value is Component c && c != null) map.TryAdd(c.gameObject, $"{id}[{entry.Key}]");
                            else if (entry.Value is GameObject g && g != null) map.TryAdd(g, $"{id}[{entry.Key}]");
                        }
                        break;
                    }
                // List<Button> 等の動的生成要素はインデックス付き ID にする
                case IEnumerable enumerable and not string:
                    {
                        var index = 0;
                        foreach (var element in enumerable)
                        {
                            if (element is Component c && c != null) map.TryAdd(c.gameObject, $"{id}[{index}]");
                            else if (element is GameObject g && g != null) map.TryAdd(g, $"{id}[{index}]");
                            index++;
                        }
                        break;
                    }
            }
        }
    }
}
