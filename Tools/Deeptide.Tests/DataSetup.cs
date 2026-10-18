using Deeptide.Tools;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>所有测试开始前载入真实的 Data/*.csv（有校验错误时整套测试直接失败）。</summary>
    [SetUpFixture]
    public sealed class DataSetup
    {
        [OneTimeSetUp]
        public void LoadData() => DataFiles.EnsureLoaded();
    }
}
