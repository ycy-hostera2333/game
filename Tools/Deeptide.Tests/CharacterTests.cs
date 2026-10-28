using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Data;
using Deeptide.Core.Map;
using Deeptide.Core.Potions;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>
    /// 角色数据化（策划/09 M6.1、14 §1.1）：角色表、按角色开局、奖励 / 商店 / 事件 / 掉落只出本角色和通用的、存档和遥测带角色。
    /// 现在表里只有调查员，所以两个角色的情形用一个只在测试里存在的「测试员」：把传染牌和通用牌、盐圈、铁剂划给它。
    /// </summary>
    public class CharacterTests
    {
        [Test]
        public void Investigator_MatchesOldDefaults()
        {
            var ch = CharacterLibrary.Get(CharacterIds.Investigator);
            Assert.That(ch.Name, Is.EqualTo("调查员"));
            Assert.That((ch.MaxHp, ch.MaxSanity, ch.Gold), Is.EqualTo((70, 10, 99)), "策划/03 §1");
            Assert.That(ch.StartingRelic, Is.EqualTo(RelicIds.InvestigatorNotes));
            Assert.That(ch.Deck, Is.EqualTo(TestDecks.Starter), "角色表和测试用的起始牌组要一致，顺序也一样（开局的卡牌编号按它排）");

            var run = RunEngine.NewRun(5);
            var s = run.State;
            Assert.That(s.Character, Is.EqualTo(CharacterIds.Investigator));
            Assert.That((s.MaxHp, s.Hp, s.MaxSanity, s.Gold), Is.EqualTo((70, 70, 10, 99)));
            Assert.That(s.Deck, Is.EqualTo(TestDecks.Starter));
            Assert.That(s.Relics, Is.EqualTo(new[] { RelicIds.InvestigatorNotes }));
        }

        [Test]
        public void RunConfig_ExplicitValuesOverrideCharacter()
        {
            var run = RunEngine.NewRun(5, new RunConfig { MaxHp = 999, Gold = 0, Deck = new List<string> { "B01" }, Relics = new List<string>() });
            var s = run.State;
            Assert.That((s.MaxHp, s.Hp, s.MaxSanity, s.Gold), Is.EqualTo((999, 999, 10, 0)), "没填的理智上限仍然取角色表");
            Assert.That(s.Deck, Is.EqualTo(new[] { "B01" }));
            Assert.That(s.Relics, Is.Empty);
        }

        [Test]
        public void UnknownCharacter_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => RunEngine.NewRun(1, new RunConfig { Character = "nobody" }));
        }

        [Test]
        public void OldSaveAndTelemetry_WithoutCharacter_ReadAsInvestigator()
        {
            var run = RunEngine.NewRun(9);
            var json = JObject.Parse(RunSaves.ToJson(run.SaveState()));
            Assert.That(json.Remove("Character"), Is.True);
            var state = RunSaves.FromJson(json.ToString());
            Assert.That(state.Character, Is.EqualTo(CharacterIds.Investigator));
            Assert.That(RunEngine.Load(state).Hash(), Is.EqualTo(run.Hash()));

            var t = JsonConvert.DeserializeObject<RunTelemetry>("{\"Seed\": 1}");
            Assert.That(t.Character, Is.EqualTo(CharacterIds.Investigator));
            Assert.That(RunTelemetry.From(run.State).Character, Is.EqualTo(CharacterIds.Investigator));
        }

        [Test]
        public void InvestigatorOnlyContent_IsMarked()
        {
            foreach (var d in CardLibrary.All)
            {
                bool shared = d.Id == "B01" || d.Id == "B02" || d.Type == CardType.Curse || d.Type == CardType.Status;
                Assert.That(d.Character, Is.EqualTo(shared ? "" : d.Id.StartsWith("L") ? "werewolf" : CharacterIds.Investigator), d.Id);
            }
            var fear = new[] { RelicIds.InvestigatorNotes, "black_candle", "fear_mask", "crown_of_dread" };
            foreach (var d in RelicLibrary.All)
                Assert.That(d.Character == CharacterIds.Investigator, Is.EqualTo(fear.Contains(d.Id)), d.Id);
            foreach (var d in PotionLibrary.All)
                Assert.That(d.Character == CharacterIds.Investigator, Is.EqualTo(d.Id == "fear_powder"), d.Id);
        }

        // ---------------- 两个角色

        /// <summary>传染 8 张 + 通用 5 张：每种稀有度、每种类型都有，商店和奖励都抽得满。</summary>
        static readonly string[] TesterCards = { "C11", "C12", "C13", "U11", "U12", "U13", "U14", "R04", "C14", "C15", "C16", "U15", "R06" };
        const string Tester = "tester";

        /// <summary>
        /// 真实数据 + 测试员：调查笔记改成通用（测试员也拿它当起始遗物），盐圈、铁剂划给测试员。
        /// 传染牌归了测试员，调查员就没有传染这个流派了，传染回响「余悸」「蔓延」也跟着划给测试员（否则倾向校验不过）。
        /// </summary>
        static DataSource WithTester()
        {
            var src = DataFiles.Read();
            src.Characters += "tester,测试员,测试,50,8,77,investigator_notes,,C11,B01*5;B02*4;C11,只在测试里出现。\n";
            src.Cards = string.Join("\n", src.Cards.Split('\n').Select(line =>
                TesterCards.Any(id => line.StartsWith(id + ",")) ? ReplaceFirst(line, ",investigator,", ",tester,") : line));
            src.Relics = src.Relics.Replace("investigator_notes,调查笔记,Starter,investigator,", "investigator_notes,调查笔记,Starter,,")
                                   .Replace("salt_circle,盐圈,Common,,", "salt_circle,盐圈,Common,tester,");
            src.Potions = src.Potions.Replace("iron_tonic,铁剂,Common,,", "iron_tonic,铁剂,Common,tester,");
            src.Echoes = src.Echoes.Replace("aftershock,余悸,Shallow,investigator,", "aftershock,余悸,Shallow,tester,")
                                   .Replace("spread,蔓延,Shallow,investigator,", "spread,蔓延,Shallow,tester,");
            return src;
        }

        static string ReplaceFirst(string text, string from, string to)
        {
            int i = text.IndexOf(from, System.StringComparison.Ordinal);
            return i < 0 ? text : text.Substring(0, i) + to + text.Substring(i + from.Length);
        }

        [Test]
        public void TwoCharacters_PoolsOnlyOfferOwnAndSharedContent()
        {
            var src = WithTester();
            var errors = GameData.Validate(src);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            try
            {
                GameData.Load(src);
                var tester = RunEngine.NewRun(3, new RunConfig { Character = Tester });
                Assert.That(tester.State.Character, Is.EqualTo(Tester));
                Assert.That((tester.State.MaxHp, tester.State.MaxSanity, tester.State.Gold), Is.EqualTo((50, 8, 77)));
                Assert.That(tester.State.Deck.Count, Is.EqualTo(10));
                Assert.That(tester.Hash(), Is.Not.EqualTo(RunEngine.NewRun(3).Hash()), "角色算进整局指纹");

                bool IsTesters(string id) => TesterCards.Contains(CardLibrary.StripUpgrade(id, out _));

                for (ulong seed = 1; seed <= 200; seed++)
                {
                    var inv = RunEngine.NewRun(seed);
                    var tst = RunEngine.NewRun(seed, new RunConfig { Character = Tester });

                    // 卡牌奖励（精英：稀有率高，三种稀有度都会抽到）和事件里的随机牌
                    Assert.That(tst.RollCardChoices(new Pcg32(seed, 1), 3, true).All(IsTesters), Is.True, $"种子 {seed}：测试员的卡牌奖励");
                    Assert.That(inv.RollCardChoices(new Pcg32(seed, 1), 3, true).Any(IsTesters), Is.False, $"种子 {seed}：调查员的卡牌奖励");
                    Assert.That(tst.RandomCards(new Pcg32(seed, 2), 5, d => true).All(IsTesters), Is.True);
                    Assert.That(inv.RandomCards(new Pcg32(seed, 2), 5, d => true).Any(IsTesters), Is.False);

                    // 遗物、药水掉落
                    foreach (var rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare })
                    {
                        var r = new Pcg32(seed, 3);
                        Assert.That(RelicLibrary.Get(tst.RollRelic(r, rarity)).Character, Is.Not.EqualTo(CharacterIds.Investigator));
                        Assert.That(inv.RollRelic(r, rarity), Is.Not.EqualTo("salt_circle"));
                    }
                    Assert.That(tst.RollPotion(new Pcg32(seed, 4)), Is.Not.EqualTo("fear_powder"));
                    Assert.That(inv.RollPotion(new Pcg32(seed, 4)), Is.Not.EqualTo("iron_tonic"));

                    if (seed > 40) continue;
                    // 商店：同一种子两个角色各进一次
                    foreach (var run in new[] { inv, tst })
                    {
                        run.State.Floor = 7;
                        run.EnterNode(6, 0, RoomType.Shop);
                        foreach (var it in run.State.Shop.Items)
                        {
                            string owner = it.Kind == ShopItemKind.Card ? CardLibrary.Get(it.Id).Character
                                : it.Kind == ShopItemKind.Relic ? RelicLibrary.Get(it.Id).Character
                                : PotionLibrary.Get(it.Id).Character;
                            Assert.That(CharacterLibrary.Allows(owner, run.State.Character), Is.True, $"种子 {seed}：{run.State.Character} 的商店里有 {it.Id}");
                        }
                    }
                }

                // 两个角色都要有足够的内容：调查员仍有 3 种稀有度的牌可抽
                var invRun = RunEngine.NewRun(1);
                foreach (var rarity in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare })
                    Assert.That(invRun.CharacterCards(rarity).Any(), Is.True, rarity.ToString());
            }
            finally
            {
                DataFiles.Reload();
            }
        }

        [Test]
        public void MainArchetype_WorksForAnyCharactersArchetypes()
        {
            var src = WithTester();
            src.Cards = src.Cards.Replace("C11,恐怖低语,Skill,Common,tester,传染,", "C11,恐怖低语,Skill,Common,tester,血宴,")
                                 .Replace("C12,影中之物,Attack,Common,tester,传染,", "C12,影中之物,Attack,Common,tester,血宴,")
                                 .Replace("C13,惊叫,Skill,Common,tester,传染,", "C13,惊叫,Skill,Common,tester,血宴,");
            try
            {
                GameData.Load(src);
                Assert.That(RunTelemetry.MainArchetype(new[] { "B01", "B02", "C11", "C12", "C13+", "C14" }), Is.EqualTo("血宴"), "通用牌和基础牌不算");
                Assert.That(RunTelemetry.MainArchetype(new[] { "C11", "C12", "S01", "S01" }), Is.EqualTo("混合"), "不到 3 张；疯语不算");
            }
            finally
            {
                DataFiles.Reload();
            }
        }
    }
}
