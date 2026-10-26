using System;
using System.Collections.Generic;
using System.Linq;

namespace Deeptide.Core.Run
{
    /// <summary>
    /// 局外档（策划/12 §3.5、§9）：跨局保留的夜数和剧情进度。纯数据，外层序列化成 profile.json。
    /// 只影响文本（开局的信、事件的循环变体、结算画面），不碰任何规则。
    /// </summary>
    public sealed class Profile
    {
        public int Version = 1;
        /// <summary>第几夜 = 开过几局新的（读档继续不算，放弃算）。0 = 一局都没开过。</summary>
        public int Nights;
        /// <summary>倒下过。</summary>
        public bool Died;
        /// <summary>打倒过溺亡司祭。</summary>
        public bool BeatPriest;
        /// <summary>前几夜见过的事件。</summary>
        public List<string> EventsSeen = new List<string>();
        /// <summary>信上已经出现的附言，按出现顺序。</summary>
        public List<string> Postscripts = new List<string>();
    }

    public sealed class Postscript
    {
        public string Id;
        public string Text;
        internal Func<Profile, bool> Ready;
    }

    /// <summary>
    /// 循环叙事「回潮」（策划/12）：开局的信和附言、结算画面的文案，以及局外档怎么跟着每一局更新。
    /// 附言按顺序出现，一夜最多多出一条（12 §8.1）：第一局就通关的玩家不会直接看到最后一条。
    /// </summary>
    public static class Story
    {
        public static readonly IReadOnlyList<string> Letter = new[]
        {
            "我在灰潮镇，一时回不去。",
            "大潮那天有一班末班船，黄昏靠岸，你坐那一班来。",
            "镇上的人在等一场潮。我拦过，一个人拦不住。",
            "下了船，别数钟声，别下水，见了镜子绕着走。",
            "笔记本带上，看见什么都记下来。",
        };

        /// <summary>信的落款。导师的名字还没定（12 §10），「知名不具」正好不用写。</summary>
        public const string Signature = "知名不具";

        public static readonly IReadOnlyList<Postscript> Postscripts = new[]
        {
            new Postscript { Id = "A", Text = "又及：如果你是第二次读到这封信，先别慌。", Ready = p => p.Nights >= 2 },
            new Postscript { Id = "B", Text = "又及：死了不要紧，钟响七下，你会回到栈桥。别沉下去，海拿走的东西不还。", Ready = p => p.Died || p.Nights >= 4 },
            new Postscript { Id = "C", Text = "又及：这一夜是我折起来的。想出去，只有一个办法：在同一夜里走到底。", Ready = p => p.EventsSeen.Contains("dream_mentor") || p.Nights >= 6 },
            new Postscript { Id = "D", Text = "又及：对不起。锚只能是外面来的人。", Ready = p => p.BeatPriest },
        };

        public const string DefeatTitle = "钟响了七下";
        public const string DefeatText = "你站在栈桥上，手里拿着那封信。";
        public const string VictoryTitle = "天还没亮";
        public const string VictoryText = "司祭倒在祭坛上，水却没有停。\n远处灯塔的塔基上，水漫过了第六道潮痕，又漫过了第七道。\n教堂底下传来钟声。一下，两下……一共七下。";
        public const string ActEnd = "第一幕 · 完";

        public static string NightLabel(int night) => $"第 {night} 夜";

        /// <summary>
        /// 开一局新的：夜数 +1，抄进这一局（Stats.Night、Stats.EventsBefore），再按顺序看下一条附言能不能出现。
        /// 返回这一夜新出现的附言，没有为 null。调用前先把上一局（放弃的存档）Record 进来。
        /// </summary>
        public static Postscript BeginNight(Profile p, RunState run)
        {
            p.Nights++;
            run.Stats.Night = p.Nights;
            run.Stats.EventsBefore = p.EventsSeen.ToList();
            var next = Postscripts.FirstOrDefault(ps => !p.Postscripts.Contains(ps.Id));
            if (next == null || !next.Ready(p)) return null;
            p.Postscripts.Add(next.Id);
            return next;
        }

        /// <summary>把一局的进度记进局外档（每次存档、放弃时调用）。返回局外档有没有变。</summary>
        public static bool Record(Profile p, RunState run)
        {
            bool changed = false;
            if (run.Phase == RunPhase.Defeat && !p.Died) changed = p.Died = true;
            if (run.Phase == RunPhase.Victory && !p.BeatPriest) changed = p.BeatPriest = true;
            foreach (var id in run.EventsSeen)
                if (!p.EventsSeen.Contains(id))
                {
                    p.EventsSeen.Add(id);
                    changed = true;
                }
            return changed;
        }

        /// <summary>信上现在有的附言，按出现顺序。</summary>
        public static List<Postscript> Shown(Profile p) =>
            p.Postscripts.Select(id => Postscripts.FirstOrDefault(ps => ps.Id == id)).Where(ps => ps != null).ToList();
    }
}
