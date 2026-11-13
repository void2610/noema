using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// <see cref="UiMonkey"/> の動かし方
    /// </summary>
    public sealed class UiMonkeyOptions
    {
        /// <summary>押す要素を選ぶ乱数のシード。同じシードと同じ画面なら同じ順に押す</summary>
        public int Seed { get; set; }
        /// <summary>押す回数</summary>
        public int Steps { get; set; } = 100;
        /// <summary>押さない要素の ID の接頭辞 (終了・シーンを離れるボタン等)</summary>
        public IReadOnlyList<string> ExcludedIdPrefixes { get; set; } = Array.Empty<string>();
        /// <summary>押せる要素が出て落ち着くのを待つ長さ (実時間の秒)。超えたら行き詰まりとして失敗にする</summary>
        public float SettleTimeoutSeconds { get; set; } = 30f;
        /// <summary>押せる要素の一覧がこのフレーム数続けて変わらなければ、演出が終わって入力を待っているとみなす</summary>
        public int StableFrames { get; set; } = 5;
        /// <summary>例外のログに加えて、エラーログも失敗にする</summary>
        public bool FailOnErrorLog { get; set; }
        /// <summary>1 回押して落ち着くたびに呼ぶ、利用側の不変条件。破れていれば理由を、守られていれば null を返す</summary>
        public Func<string> CheckInvariant { get; set; }
        /// <summary>true の間は押さずに待つ (利用側の処理や演出の途中)。押す時点の画面がそろい、同じシードの結果が実行ごとにずれにくくなる</summary>
        public Func<bool> IsBusy { get; set; }
        /// <summary>true を返したら、残りの回数を押さずに成功として終える (探索の対象のシーンを離れた等)</summary>
        public Func<bool> ShouldStop { get; set; }
    }

    /// <summary>
    /// <see cref="UiMonkey"/> の結果。Trace は押した ID を順に並べたもので、<see cref="UiMonkey.ReplayAsync"/> へ渡すと同じ操作をたどれる
    /// </summary>
    public sealed class UiMonkeyResult
    {
        public bool Success { get; }
        /// <summary>失敗の理由。成功なら空文字</summary>
        public string Failure { get; }
        public IReadOnlyList<string> Trace { get; }

        public UiMonkeyResult(bool success, string failure, IReadOnlyList<string> trace)
        {
            Success = success;
            Failure = failure ?? "";
            Trace = trace;
        }

        public override string ToString() =>
            Success ? $"ok: {Trace.Count} steps" : $"failed at step {Trace.Count}: {Failure}\ntrace: {string.Join(UiMonkey.TRACE_SEPARATOR, Trace)}";
    }

    /// <summary>
    /// いま押せる要素 (<see cref="UiQuery.FindPressable"/>) からランダムに選んで実入力で押し続ける探索。
    /// 期待値を持たず、例外・行き詰まり・利用側の不変条件の破れを失敗として、そこまでに押した ID の列と一緒に返す
    /// </summary>
    public static class UiMonkey
    {
        public const string TRACE_SEPARATOR = " > ";

        public static async Awaitable<UiMonkeyResult> RunAsync(UiMonkeyOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var random = new System.Random(options.Seed);
            var trace = new List<string>();
            using var logs = new LogWatcher(options.FailOnErrorLog);
            for (var step = 0; step < options.Steps; step++)
            {
                if (options.ShouldStop?.Invoke() == true) return new UiMonkeyResult(true, null, trace);
                var candidates = await WaitForSettledAsync(options, logs, cancellationToken);
                if (logs.Failure != null) return new UiMonkeyResult(false, logs.Failure, trace);
                if (options.ShouldStop?.Invoke() == true) return new UiMonkeyResult(true, null, trace);
                if (candidates == null) return new UiMonkeyResult(false, $"押せる要素が {options.SettleTimeoutSeconds} 秒待っても出て落ち着かなかった (行き詰まり)", trace);

                var violation = options.CheckInvariant?.Invoke();
                if (violation != null) return new UiMonkeyResult(false, violation, trace);

                var id = candidates[random.Next(candidates.Count)];
                trace.Add(id);
                await ClickAsync(id, cancellationToken);
                if (logs.Failure != null) return new UiMonkeyResult(false, logs.Failure, trace);
            }

            // 最後に押した操作の結果まで確かめる
            if (options.ShouldStop?.Invoke() == true) return new UiMonkeyResult(true, null, trace);
            await WaitForSettledAsync(options, logs, cancellationToken);
            if (logs.Failure != null) return new UiMonkeyResult(false, logs.Failure, trace);
            var lastViolation = options.CheckInvariant?.Invoke();
            return lastViolation != null ? new UiMonkeyResult(false, lastViolation, trace) : new UiMonkeyResult(true, null, trace);
        }

        /// <summary>
        /// <see cref="UiMonkeyResult.Trace"/> の ID を順に、押せるようになるのを待って押す。押せないまま時間切れになった ID があればそこで失敗を返す
        /// </summary>
        public static async Awaitable<UiMonkeyResult> ReplayAsync(IReadOnlyList<string> ids, UiMonkeyOptions options, CancellationToken cancellationToken = default)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var trace = new List<string>();
            using var logs = new LogWatcher(options.FailOnErrorLog);
            foreach (var id in ids)
            {
                var deadline = Time.realtimeSinceStartup + options.SettleTimeoutSeconds;
                UiPointer.ClickResult result;
                while (true)
                {
                    result = await ClickAsync(id, cancellationToken);
                    if (result.Success || logs.Failure != null || Time.realtimeSinceStartup >= deadline) break;
                    await Awaitable.NextFrameAsync(cancellationToken);
                }
                trace.Add(id);
                if (logs.Failure != null) return new UiMonkeyResult(false, logs.Failure, trace);
                if (!result.Success) return new UiMonkeyResult(false, $"'{id}' を押せなかった: {result.Message}", trace);
            }

            await WaitForSettledAsync(options, logs, cancellationToken);
            if (logs.Failure != null) return new UiMonkeyResult(false, logs.Failure, trace);
            var violation = options.CheckInvariant?.Invoke();
            return violation != null ? new UiMonkeyResult(false, violation, trace) : new UiMonkeyResult(true, null, trace);
        }

        /// <summary>押せる要素 (除外を引いた ID の昇順) が空でなく、顔ぶれと位置が StableFrames 続けて変わらなくなったら返す。時間切れ・失敗のログなら null</summary>
        private static async Awaitable<List<string>> WaitForSettledAsync(UiMonkeyOptions options, LogWatcher logs, CancellationToken cancellationToken)
        {
            var deadline = Time.realtimeSinceStartup + options.SettleTimeoutSeconds;
            string lastSignature = null;
            var stableFrames = 0;
            while (logs.Failure == null && options.ShouldStop?.Invoke() != true)
            {
                var candidates = options.IsBusy?.Invoke() == true ? new List<UiNode>() : CollectCandidates(options.ExcludedIdPrefixes);
                // 動いている途中の要素を押すと当たる先が実行ごとに変わるので、ID だけでなく位置も止まるまで待つ
                var signature = string.Join("\n", candidates.Select(n => $"{n.Id} {Mathf.RoundToInt(n.ScreenBounds.x)},{Mathf.RoundToInt(n.ScreenBounds.y)},{Mathf.RoundToInt(n.ScreenBounds.width)},{Mathf.RoundToInt(n.ScreenBounds.height)}"));
                if (candidates.Count > 0 && signature == lastSignature)
                {
                    if (++stableFrames >= options.StableFrames) return candidates.Select(n => n.Id).ToList();
                }
                else
                {
                    lastSignature = signature;
                    stableFrames = 0;
                }
                if (Time.realtimeSinceStartup >= deadline) return null;
                await Awaitable.NextFrameAsync(cancellationToken);
            }
            return null;
        }

        private static List<UiNode> CollectCandidates(IReadOnlyList<string> excludedIdPrefixes) =>
            UiQuery.FindPressable()
                .Where(n => !excludedIdPrefixes.Any(prefix => n.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .ToList();

        private static async Awaitable<UiPointer.ClickResult> ClickAsync(string id, CancellationToken cancellationToken)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return new UiPointer.ClickResult(false, $"node '{id}' not found");
            return node.IsWorld ? await UiWorldPointer.ClickAsync(node, cancellationToken) : UiPointer.Click(node);
        }

        /// <summary>探索の間に出た例外 (と、指定があればエラー) のログのうち最初の 1 件を覚える</summary>
        private sealed class LogWatcher : IDisposable
        {
            private readonly bool _failOnErrorLog;

            public string Failure { get; private set; }

            public LogWatcher(bool failOnErrorLog)
            {
                _failOnErrorLog = failOnErrorLog;
                Application.logMessageReceived += OnLog;
            }

            public void Dispose() => Application.logMessageReceived -= OnLog;

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (Failure != null) return;
                if (type == LogType.Exception || (_failOnErrorLog && type is LogType.Error or LogType.Assert))
                    Failure = $"{type}: {condition}";
            }
        }
    }
}
