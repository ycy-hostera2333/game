namespace Deeptide.Core
{
    /// <summary>
    /// 战斗规则参数，默认值见 策划/01 §2.1 与 02 §1。集中在这里，方便纸面原型验证后统一调整。
    /// 开战时引擎复制一份，再交给监听器的 <see cref="BattleListener.ModifyConfig"/> 改（回响改的就是这里，策划/15 §7.1），
    /// 所以 BattleSetup 传进来的这份不会被改动，几场战斗可以共用。
    /// </summary>
    public sealed class BattleConfig
    {
        public int EnergyPerTurn = 3;
        public int DrawPerTurn = 5;
        public int HandLimit = 10;

        /// <summary>理智 ≤ 此值即处于癫狂。</summary>
        public int LunacyThreshold = 4;

        /// <summary>清明：理智 ≥ 上限 − 此值（0 = 必须满理智）。默认 1：上限 10 时理智 9、10 都算清明（策划/07 §7.5）。</summary>
        public int ClarityMargin = 1;

        /// <summary>开战理智 = 上限 − 此值（最低 1）。默认 0：满理智开战。</summary>
        public int StartSanityDeficit = 0;

        /// <summary>从第几回合开始侵蚀（回合开始时失去理智）。</summary>
        public int ErosionStartTurn = 4;
        public int ErosionAmount = 1;

        /// <summary>崩溃：失去最大生命的百分比（向下取整），之后理智重置为该值。</summary>
        public int CollapseHpPercent = 10;
        public int CollapseSanityReset = 3;

        /// <summary>每次震慑后意志 ×(Num/Den)，向上取整。默认 ×1.5。</summary>
        public int WillGrowthNum = 3;
        public int WillGrowthDen = 2;

        /// <summary>震慑时保留的恐惧百分比（向下取整），最多保留到新意志 − 1，不会一挨恐惧就再次震慑。默认 0：清零。</summary>
        public int StunFearKeptPercent = 0;

        public BattleConfig Clone() => (BattleConfig)MemberwiseClone();
    }
}
