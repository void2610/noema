using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// View (MonoBehaviour) のフィールドを reflection で逆引きし、UI GameObject → "ViewType/fieldName" の安定 ID 辞書を作る
    /// 読む範囲は明示宣言されたフィールドのみ: [SerializeField] (Inspector 配線) と [UiNodeSource] (動的生成の実行時フィールド)。
    /// Dictionary はキーがそのまま意味的 ID になる (例: keywordButtons[emotion])。
    /// 他の View のコレクション要素として握られている View は、自分のフィールドの ID を親の要素 ID で合成する
    /// (例: HandView/cards[2]/cardButton)。同型 View が複数並ぶときに ID が衝突しないようにするため
    /// </summary>
    public static class UiViewIdMap
    {

        // reflection コスト削減用 (型ごとの対象フィールドは実行中不変)
        private static readonly Dictionary<System.Type, (FieldInfo[] Serialized, FieldInfo[] Runtime)> FieldCache = new();

        public static IReadOnlyDictionary<GameObject, string> Build()
        {
            if (!NoemaConfig.HasProjectAssemblyPrefix)
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
            var bases = ResolveBaseIds(behaviours);
            // 優先順位を段階で決め切る。同じ GameObject を複数フィールドが指すとき、
            // 登録順が FindObjectsByType の不定順に依存すると実行ごとに ID が入れ替わってしまう。
            // 要素を階層の配下に持つ View からの参照を最優先する (選択中の要素を追うデバッグ用の参照等に ID を奪われないように)。
            // 自分自身を指す参照 (View が自分の Image を持つ等) は内部実装で、外から引く ID としては
            // 親 View からの参照 (親/cards[0] 等) のほうが通りが良いので後段へ回す
            RegisterPass(map, behaviours, bases, serialized: true, Relation.Contained);
            RegisterPass(map, behaviours, bases, serialized: false, Relation.Contained);
            RegisterPass(map, behaviours, bases, serialized: true, Relation.Unrelated);
            RegisterPass(map, behaviours, bases, serialized: true, Relation.Self);
            RegisterPass(map, behaviours, bases, serialized: false, Relation.Unrelated);
            RegisterPass(map, behaviours, bases, serialized: false, Relation.Self);
            return map;
        }

        // 参照先 GameObject と参照元 View の位置関係
        private enum Relation
        {
            Contained,
            Unrelated,
            Self,
        }

        private static Relation RelationOf(GameObject go, GameObject owner)
        {
            if (go == owner) return Relation.Self;
            return go.transform.IsChildOf(owner.transform) ? Relation.Contained : Relation.Unrelated;
        }

        internal static bool IsProjectAssembly(System.Type type)
        {
            return NoemaConfig.IsProjectAssemblyName(type.Assembly.GetName().Name);
        }

        // 実行時フィールドの "_" プレフィックスは ID に含めない (ReportView/_keywordButtons → ReportView/keywordButtons)
        private static string FieldNameOf(FieldInfo field) => field.Name.TrimStart('_');

        private static (FieldInfo[], FieldInfo[]) FieldsOf(System.Type viewType)
        {
            if (FieldCache.TryGetValue(viewType, out var cached)) return cached;
            var serialized = new List<FieldInfo>();
            var runtime = new List<FieldInfo>();
            // GetFields は基底の private を返さないため、継承チェーンを自前で遡る
            for (var type = viewType; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                // Button 派生の自作クラス等で、基底 (エンジン側) の m_TargetGraphic 等を ID の供給源にしない
                if (!IsProjectAssembly(type)) continue;
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

        // View ごとの ID の起点 (通常は型名、コレクション要素として握られていれば親の要素 ID) を決める
        private static Dictionary<MonoBehaviour, string> ResolveBaseIds(
            List<(MonoBehaviour Behaviour, FieldInfo[] Serialized, FieldInfo[] Runtime)> behaviours)
        {
            var projectBehaviours = new HashSet<MonoBehaviour>();
            foreach (var (behaviour, _, _) in behaviours) projectBehaviours.Add(behaviour);

            // 子 View → (親 View, 親から見た要素名)。複数の親に握られている場合は決定的に 1 つへ絞る
            var parents = new Dictionary<MonoBehaviour, (MonoBehaviour Owner, string Element, string SortKey)>();
            foreach (var (owner, serialized, runtime) in behaviours)
            {
                foreach (var field in serialized) CollectCollectionChildren(owner, field, projectBehaviours, parents);
                foreach (var field in runtime) CollectCollectionChildren(owner, field, projectBehaviours, parents);
            }

            var bases = new Dictionary<MonoBehaviour, string>();
            var resolving = new HashSet<MonoBehaviour>();

            string BaseOf(MonoBehaviour behaviour)
            {
                if (bases.TryGetValue(behaviour, out var cached)) return cached;
                var typeName = behaviour.GetType().Name;
                // 循環参照は合成せず型名で打ち切る
                if (!parents.TryGetValue(behaviour, out var parent) || !resolving.Add(behaviour)) return typeName;
                var id = $"{BaseOf(parent.Owner)}/{parent.Element}";
                resolving.Remove(behaviour);
                bases[behaviour] = id;
                return id;
            }

            foreach (var (behaviour, _, _) in behaviours) bases[behaviour] = BaseOf(behaviour);
            return bases;
        }

        private static void CollectCollectionChildren(MonoBehaviour owner, FieldInfo field, HashSet<MonoBehaviour> projectBehaviours,
            Dictionary<MonoBehaviour, (MonoBehaviour Owner, string Element, string SortKey)> parents)
        {
            var value = field.GetValue(owner);
            // Transform 等の UnityEngine.Object は IEnumerable でもコレクションとして扱わない
            if (value is Object || value is string) return;

            void Candidate(object element, string key)
            {
                if (element is not MonoBehaviour child || child == null || child == owner || !projectBehaviours.Contains(child)) return;
                var elementName = $"{FieldNameOf(field)}[{key}]";
                var sortKey = $"{owner.GetType().FullName}/{elementName}";
                if (parents.TryGetValue(child, out var existing) && string.CompareOrdinal(existing.SortKey, sortKey) <= 0) return;
                parents[child] = (owner, elementName, sortKey);
            }

            switch (value)
            {
                case IDictionary dictionary:
                    foreach (DictionaryEntry entry in dictionary) Candidate(entry.Value, entry.Key?.ToString());
                    break;
                case IEnumerable enumerable:
                    {
                        var index = 0;
                        foreach (var element in enumerable) Candidate(element, (index++).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    }
            }
        }

        private static void RegisterPass(Dictionary<GameObject, string> map,
            List<(MonoBehaviour Behaviour, FieldInfo[] Serialized, FieldInfo[] Runtime)> behaviours,
            Dictionary<MonoBehaviour, string> bases, bool serialized, Relation relation)
        {
            foreach (var (behaviour, serializedFields, runtimeFields) in behaviours)
            {
                foreach (var field in serialized ? serializedFields : runtimeFields)
                    Register(map, field.GetValue(behaviour), $"{bases[behaviour]}/{FieldNameOf(field)}", behaviour.gameObject, relation);
            }
        }

        private static void Register(Dictionary<GameObject, string> map, object value, string id,
            GameObject owner, Relation relation)
        {
            // 要素ごとに参照元との位置関係を見て、その段のものだけ登録する
            void TryAdd(GameObject go) => TryAddWith(go, id);

            void TryAddWith(GameObject go, string elementId)
            {
                if (RelationOf(go, owner) != relation) return;
                map.TryAdd(go, elementId);
            }

            switch (value)
            {
                // fake-null (未アサイン/破棄済み) を先に落とす。Transform 等が IEnumerable ケースへ落ちて列挙時に例外になるのを防ぐ
                case Object unityObject when unityObject == null:
                    break;
                case Component component:
                    TryAdd(component.gameObject);
                    break;
                case GameObject go:
                    TryAdd(go);
                    break;
                // Dictionary<TKey, Button/GameObject> はキーを意味的 ID として使う (IEnumerable より先に判定する)
                case IDictionary dictionary:
                    {
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            if (entry.Value is Component c && c != null) TryAddWith(c.gameObject, $"{id}[{entry.Key}]");
                            else if (entry.Value is GameObject g && g != null) TryAddWith(g, $"{id}[{entry.Key}]");
                        }
                        break;
                    }
                // List<Button> 等の動的生成要素はインデックス付き ID にする
                case IEnumerable enumerable and not string:
                    {
                        var index = 0;
                        foreach (var element in enumerable)
                        {
                            if (element is Component c && c != null) TryAddWith(c.gameObject, $"{id}[{index}]");
                            else if (element is GameObject g && g != null) TryAddWith(g, $"{id}[{index}]");
                            index++;
                        }
                        break;
                    }
            }
        }
    }
}
