using System.Collections.Generic;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// uGUI 外の操作対象 (タイルマップのマス、ワールド上のキャラクター等) をセマンティックツリーへ供給する拡張点。
    /// 供給したノードへの操作は EventSystem ではなく実入力デバイス経由 (<see cref="UiWorldPointer"/>) で行う
    /// </summary>
    public interface IUiNodeProvider
    {
        /// <summary>現時点のノードを nodes へ追加する。ノードの Provider には自分自身を渡す</summary>
        void Collect(List<UiNode> nodes);

        /// <summary>
        /// screenPosition を実入力でクリックしたとき node に届くか。届くなら null、届かないなら理由を返す
        /// (手前の地形に遮られて別のマスが拾われる、等をテスト失敗として検出するため)
        /// </summary>
        string Probe(UiNode node, Vector2 screenPosition);
    }

    /// <summary>
    /// <see cref="IUiNodeProvider"/> の登録先。登録は利用側の View の有効化・無効化に合わせて行う
    /// </summary>
    public static class UiNodeProviders
    {
        private static readonly List<IUiNodeProvider> Providers = new();

        public static IReadOnlyList<IUiNodeProvider> All => Providers;

        public static void Register(IUiNodeProvider provider)
        {
            if (provider != null && !Providers.Contains(provider)) Providers.Add(provider);
        }

        public static void Unregister(IUiNodeProvider provider) => Providers.Remove(provider);

        // Domain Reload 無効時に前回の Play の登録が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Providers.Clear();

        internal static void CollectAll(List<UiNode> nodes)
        {
            // 列挙中の登録解除 (ノード収集で View が破棄される等) に備えて複製して回す
            foreach (var provider in Providers.ToArray())
            {
                // 登録解除し忘れた破棄済み View を呼ばない
                if (provider is Object unityObject && unityObject == null) continue;
                provider.Collect(nodes);
            }
        }
    }
}
