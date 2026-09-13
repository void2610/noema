using System;
using NUnit.Framework;

namespace Void2610.Noema.Tests
{
    /// <summary>
    /// EditMode 全体で一度だけ noema の対象アセンブリ prefix を本テストアセンブリへ向ける (各テストでの設定漏れ crash-fast を防ぐ)。
    /// 利用側プロジェクトのテストと同じ実行に載る想定なので、スコープで必ず元の値へ戻す
    /// </summary>
    [SetUpFixture]
    public sealed class NoemaTestSetup
    {
        // 前方一致なので、本体アセンブリ (Void2610.Noema) まで拾わないようテストアセンブリ名をフルで指定する
        private const string TEST_ASSEMBLY_PREFIX = "Void2610.Noema.Tests";

        private IDisposable _prefixScope;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _prefixScope = NoemaConfig.PushProjectAssemblyPrefix(TEST_ASSEMBLY_PREFIX);

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _prefixScope?.Dispose();
            _prefixScope = null;
        }
    }
}
