using System;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Core.Relics;
using Deeptide.Tools;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>CSV 解析与数据校验（策划/09 M2）。</summary>
    public class DataTests
    {
        [Test]
        public void RealData_PassesValidation()
        {
            var errors = GameData.Validate(DataFiles.Read());
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void Csv_HandlesQuotesCommasNewlinesAndChinese()
        {
            const string text = "﻿id,text,n\r\n" +
                                "# 注释行\r\n" +
                                "a,\"造成 6 伤害，然后, 再说\",1\r\n" +
                                "\r\n" +
                                "b,\"第一行\n第二行\",2\n" +
                                "c,\"他说\"\"你好\"\"\",3";
            var t = Csv.Parse(text, "t.csv");
            Assert.That(t.Rows.Count, Is.EqualTo(3));
            Assert.That(t.Rows[0]["id"], Is.EqualTo("a"));
            Assert.That(t.Rows[0]["text"], Is.EqualTo("造成 6 伤害，然后, 再说"));
            Assert.That(t.Rows[1]["text"], Is.EqualTo("第一行\n第二行"));
            Assert.That(t.Rows[2]["text"], Is.EqualTo("他说\"你好\""));
            Assert.That(t.Rows[2].Int("n"), Is.EqualTo(3));
            Assert.That(t.Rows[2].Line, Is.EqualTo(7));
        }

        [Test]
        public void Csv_RejectsWrongColumnCountAndOpenQuote()
        {
            Assert.Throws<FormatException>(() => Csv.Parse("a,b\n1,2,3", "t.csv"));
            Assert.Throws<FormatException>(() => Csv.Parse("a,b\n1,\"2", "t.csv"));
        }

        [Test]
        public void Vars_ParseInOrder()
        {
            var v = Csv.ParseVars("Damage=6; Hits=2;");
            Assert.That(v["Damage"], Is.EqualTo(6));
            Assert.That(v["Hits"], Is.EqualTo(2));
            Assert.Throws<FormatException>(() => Csv.ParseVars("Damage=six"));
        }

        static DataSource Modified(Func<string, string> cards = null, Func<string, string> enemies = null, Func<string, string> relics = null)
        {
            var src = DataFiles.Read();
            if (cards != null) src.Cards = cards(src.Cards);
            if (enemies != null) src.Enemies = enemies(src.Enemies);
            if (relics != null) src.Relics = relics(src.Relics);
            return src;
        }

        static void AssertError(DataSource src, string fragment)
        {
            var errors = GameData.Validate(src);
            Assert.That(errors.Any(e => e.Contains(fragment)), Is.True, $"应报告「{fragment}」，实际：\n{string.Join("\n", errors)}");
        }

        [Test]
        public void Validation_RowWithoutClass()
        {
            AssertError(Modified(cards: c => c + "Z99,不存在,Skill,Common,通用,1,,None,,,,Block=5,,获得 {Block} 格挡。,,,\n"), "Z99 没有对应的类");
        }

        [Test]
        public void Validation_ClassWithoutRow()
        {
            AssertError(Modified(cards: c => string.Join("\n", c.Split('\n').Where(l => !l.StartsWith("B01,")))), "代码里有类 B01");
        }

        [Test]
        public void Validation_UndefinedTextVariable()
        {
            AssertError(Modified(cards: c => c.Replace("造成 {Damage} 伤害。清明：抽 {Draw} 张牌。", "造成 {Damage} 伤害。清明：抽 {Drew} 张牌。")), "{Drew}");
        }

        [Test]
        public void Validation_UpgradeVariableMustExist()
        {
            AssertError(Modified(cards: c => c.Replace("Damage=6,Damage=9", "Damage=6,Dmg=9")), "升级变量 Dmg");
        }

        [Test]
        public void Validation_EnemyMoveNotDeclared()
        {
            AssertError(Modified(enemies: c => c.Replace("Heavy=献祭重击", "Hevy=献祭重击")), "招式 Heavy 没有在 enemies.csv");
        }

        [Test]
        public void Validation_EnemyMissingVar()
        {
            AssertError(Modified(enemies: c => c.Replace(";PrayerStrength=2", "")), "缺少数值 PrayerStrength");
        }

        [Test]
        public void Validation_ReportsAllErrorsAtOnce_AndFailedLoadKeepsOldData()
        {
            var bad = Modified(
                cards: c => c.Replace("获得 {Block} 格挡。,,,", "获得 {Blk} 格挡。,,,"),
                relics: r => r.Replace("{Draw}", "{Drw}"));
            Assert.That(GameData.Validate(bad).Count, Is.GreaterThanOrEqualTo(2));
            Assert.Throws<DataValidationException>(() => GameData.Load(bad));
            Assert.That(CardLibrary.Exists("B01"), Is.True, "载入失败不应清掉已有数据");
        }

        [Test]
        public void Libraries_KeepTableOrder()
        {
            Assert.That(CardLibrary.All.First().Id, Is.EqualTo("B01"));
            Assert.That(EnemyLibrary.All.First().Id, Is.EqualTo(EnemyIds.Acolyte));
            Assert.That(RelicLibrary.All.First().Id, Is.EqualTo(RelicIds.InvestigatorNotes));
            Assert.That(RelicLibrary.Create(RelicIds.InvestigatorNotes).Description, Does.Contain("获得 1 能量并抽 2 张牌"));
        }
    }
}
