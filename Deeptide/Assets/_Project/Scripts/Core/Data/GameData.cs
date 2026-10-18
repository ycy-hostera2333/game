using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;

namespace Deeptide.Core.Data
{
    /// <summary>一次载入所需的全部表格文本。Core 不读文件：Unity 传 TextAsset.text，Tools 直接读磁盘。</summary>
    public sealed class DataSource
    {
        public string Cards;
        public string Enemies;
        public string Relics;
        public string Potions;
        public string Encounters;
        public string Events;

        /// <summary>Data 目录下的文件名，外层按这个名字找文件。</summary>
        public static readonly string[] FileNames = { "cards.csv", "enemies.csv", "relics.csv", "potions.csv", "encounters.csv", "events.csv" };

        /// <summary>按文件名取文本，供外层统一读取：DataSource.From(name => File.ReadAllText(dir + name))。</summary>
        public static DataSource From(Func<string, string> read) => new DataSource
        {
            Cards = read("cards.csv"),
            Enemies = read("enemies.csv"),
            Relics = read("relics.csv"),
            Potions = read("potions.csv"),
            Encounters = read("encounters.csv"),
            Events = read("events.csv"),
        };
    }

    public sealed class DataValidationException : Exception
    {
        public readonly IReadOnlyList<string> Errors;

        public DataValidationException(IReadOnlyList<string> errors)
            : base($"数据校验失败（{errors.Count} 处）：\n" + string.Join("\n", errors))
        {
            Errors = errors;
        }
    }

    /// <summary>
    /// 内容数据的载入与校验（策划/09 M2）。校验项：
    /// 每一行都有对应的类、每个类都有对应的行；文本里的 {变量} 都有定义；升级变量在基础变量里存在；
    /// 敌人招式表里的每个招式都在 moves 列声明过，声明过的招式也都被用到。所有问题一次性列出来。
    /// </summary>
    public static class GameData
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public static bool IsLoaded { get; private set; }

        /// <summary>校验并载入全部内容。有任何校验错误就抛出 DataValidationException，已经载入的内容保持不变。</summary>
        public static void Load(DataSource source)
        {
            var staged = Stage(source, out var errors);
            if (errors.Count > 0) throw new DataValidationException(errors);

            CardLibrary.Clear();
            EnemyLibrary.Clear();
            RelicLibrary.Clear();
            PotionLibrary.Clear();
            EncounterLibrary.Clear();
            EventLibrary.Clear();
            foreach (var d in staged.Cards) CardLibrary.Add(d);
            foreach (var d in staged.Enemies) EnemyLibrary.Add(d);
            foreach (var d in staged.Relics) RelicLibrary.Add(d);
            foreach (var d in staged.Potions) PotionLibrary.Add(d);
            foreach (var d in staged.Encounters) EncounterLibrary.Add(d);
            foreach (var d in staged.Events) EventLibrary.Add(d);
            IsLoaded = true;
        }

        /// <summary>只校验、不载入，返回所有问题（空列表 = 通过）。</summary>
        public static IReadOnlyList<string> Validate(DataSource source)
        {
            Stage(source, out var errors);
            return errors;
        }

        sealed class Staged
        {
            public readonly List<CardDef> Cards = new List<CardDef>();
            public readonly List<EnemyDef> Enemies = new List<EnemyDef>();
            public readonly List<RelicDef> Relics = new List<RelicDef>();
            public readonly List<PotionDef> Potions = new List<PotionDef>();
            public readonly List<EncounterDef> Encounters = new List<EncounterDef>();
            public readonly List<EventDef> Events = new List<EventDef>();
        }

        static Staged Stage(DataSource source, out List<string> errors)
        {
            var staged = new Staged();
            var errs = new List<string>();
            LoadTable(source.Cards, "cards.csv", errs, t => LoadCards(t, errs, staged.Cards));
            LoadTable(source.Enemies, "enemies.csv", errs, t => LoadEnemies(t, errs, staged.Enemies));
            LoadTable(source.Relics, "relics.csv", errs, t => LoadRelics(t, errs, staged.Relics));
            LoadTable(source.Potions, "potions.csv", errs, t => LoadPotions(t, errs, staged.Potions));
            LoadTable(source.Encounters, "encounters.csv", errs, t => LoadEncounters(t, errs, staged.Encounters, staged.Enemies));
            LoadTable(source.Events, "events.csv", errs, t => LoadEvents(t, errs, staged.Events));
            errors = errs;
            return staged;
        }

        static void LoadTable(string text, string file, List<string> errors, Action<CsvTable> load)
        {
            if (text == null)
            {
                errors.Add($"缺少 {file}");
                return;
            }
            CsvTable table;
            try { table = Csv.Parse(text, file); }
            catch (FormatException ex)
            {
                errors.Add(ex.Message);
                return;
            }
            load(table);
        }

        /// <summary>逐行执行；一行出错只记录错误，继续检查其余行。</summary>
        static void EachRow(CsvTable table, List<string> errors, Action<CsvRow> fn)
        {
            foreach (var row in table.Rows)
            {
                try { fn(row); }
                catch (Exception ex) when (ex is FormatException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
                {
                    errors.Add(ex.Message.StartsWith(row.Where, StringComparison.Ordinal) ? ex.Message : $"{row.Where}：{ex.Message}");
                }
            }
        }

        static void CheckClasses(string file, IEnumerable<string> rowIds, IEnumerable<string> classIds, List<string> errors)
        {
            var rows = new HashSet<string>(rowIds);
            foreach (var id in classIds)
                if (!rows.Contains(id)) errors.Add($"{file}：代码里有类 {id}，但表里没有这一行");
        }

        static void CheckPlaceholders(string where, string text, Dictionary<string, int> vars, List<string> errors)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (Match m in Placeholder.Matches(text))
                if (!vars.ContainsKey(m.Groups[1].Value))
                    errors.Add($"{where}：文本引用了未定义的变量 {{{m.Groups[1].Value}}}");
        }

        // ------------------------------------------------------------------ cards.csv

        static void LoadCards(CsvTable table, List<string> errors, List<CardDef> output)
        {
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"卡牌 ID 重复：{id}");
                if (!CardClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 CardClasses 里注册）");

                string cost = row["cost"];
                var def = new CardDef
                {
                    Id = id,
                    Name = row["name"],
                    Type = row.Enum<CardType>("type"),
                    Rarity = row.Enum<Rarity>("rarity"),
                    Archetype = row["archetype"],
                    XCost = cost == "X",
                    Cost = cost == "X" ? 0 : row.Int("cost"),
                    UpgradedCost = row.IntOr("cost_up", -1),
                    Target = row.Enum<CardTarget>("target"),
                    Dual = row.Flag("dual"),
                    Keywords = ParseKeywords(row["keywords"]),
                    UpgradedKeywords = row["keywords_up"].Length == 0 ? (CardKeywords?)null : ParseKeywords(row["keywords_up"]),
                    Vars = Csv.ParseVars(row["vars"]),
                    UpgradeVars = Csv.ParseVars(row["vars_up"]),
                    TextSane = row["text_sane"],
                    TextMad = row["text_mad"],
                    TextSaneUp = row["text_sane_up"],
                    TextMadUp = row["text_mad_up"],
                    Factory = factory,
                };
                if (def.Name.Length == 0) throw new FormatException($"{id} 没有名字");
                if (def.Dual && def.TextMad.Length == 0) throw new FormatException($"{id} 是双面牌但没有癫狂面文本");
                if (!def.Dual && (def.TextMad.Length > 0 || def.TextMadUp.Length > 0)) throw new FormatException($"{id} 不是双面牌却写了癫狂面文本");
                foreach (var key in def.UpgradeVars.Keys)
                    if (!def.Vars.ContainsKey(key)) throw new FormatException($"{id} 的升级变量 {key} 在 vars 里不存在");
                foreach (var text in new[] { def.TextSane, def.TextMad, def.TextSaneUp, def.TextMadUp })
                    CheckPlaceholders($"{row.Where}（{id}）", text, def.Vars, errors);
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, CardClasses.Factories.Keys, errors);
        }

        static CardKeywords ParseKeywords(string text)
        {
            var result = CardKeywords.None;
            foreach (var k in Csv.ParseList(text))
            {
                if (!Enum.TryParse(k, true, out CardKeywords kw) || kw == CardKeywords.None)
                    throw new FormatException($"未知关键词：{k}（可选 exhaust / ethereal / unplayable / innate / retain）");
                result |= kw;
            }
            return result;
        }

        // ------------------------------------------------------------------ enemies.csv

        static void LoadEnemies(CsvTable table, List<string> errors, List<EnemyDef> output)
        {
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"敌人 ID 重复：{id}");
                if (!EnemyClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 EnemyClasses 里注册）");

                var def = new EnemyDef
                {
                    Id = id,
                    Name = row["name"],
                    HpMin = row.Int("hp_min"),
                    HpMax = row.Int("hp_max"),
                    Will = row.Int("will"),
                    Vars = Csv.ParseVars(row["vars"]),
                    MoveNames = Csv.ParseMap(row["moves"]).ToDictionary(kv => kv.Key, kv => kv.Value),
                    Factory = factory,
                };
                if (def.HpMin <= 0 || def.HpMax < def.HpMin) throw new FormatException($"{id} 的生命区间不对：{def.HpMin}–{def.HpMax}");
                if (def.MoveNames.Count == 0) throw new FormatException($"{id} 没有声明任何招式");

                // 搭一次招式表：缺数值、用了没声明的招式都会在这里报出来
                var behaviour = EnemyLibrary.CreateBehaviour(def);
                var built = new HashSet<string>(behaviour.Moves.Select(m => m.Id));
                foreach (var move in def.MoveNames.Keys)
                    if (!built.Contains(move)) throw new FormatException($"{id} 声明了招式 {move}，但代码里的招式表没有用到它");
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, EnemyClasses.Factories.Keys, errors);
        }

        // ------------------------------------------------------------------ relics.csv

        static void LoadRelics(CsvTable table, List<string> errors, List<RelicDef> output)
        {
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"遗物 ID 重复：{id}");
                if (!RelicClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 RelicClasses 里注册）");

                var def = new RelicDef
                {
                    Id = id,
                    Name = row["name"],
                    Rarity = row.Enum<RelicRarity>("rarity"),
                    Tendency = row["tendency"],
                    Vars = Csv.ParseVars(row["vars"]),
                    Text = row["text"],
                    Factory = factory,
                };
                CheckPlaceholders($"{row.Where}（{id}）", def.Text, def.Vars, errors);
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, RelicClasses.Factories.Keys, errors);
        }

        // ------------------------------------------------------------------ potions.csv

        static void LoadPotions(CsvTable table, List<string> errors, List<PotionDef> output)
        {
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"药水 ID 重复：{id}");
                if (!PotionClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 PotionClasses 里注册）");

                var def = new PotionDef
                {
                    Id = id,
                    Name = row["name"],
                    Rarity = row.Enum<PotionRarity>("rarity"),
                    Target = row.Enum<CardTarget>("target"),
                    Vars = Csv.ParseVars(row["vars"]),
                    Text = row["text"],
                    Factory = factory,
                };
                CheckPlaceholders($"{row.Where}（{id}）", def.Text, def.Vars, errors);
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, PotionClasses.Factories.Keys, errors);
        }

        // ------------------------------------------------------------------ encounters.csv

        static void LoadEncounters(CsvTable table, List<string> errors, List<EncounterDef> output, List<EnemyDef> enemies)
        {
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"遭遇 ID 重复：{id}");
                var def = new EncounterDef
                {
                    Id = id,
                    Name = row["name"],
                    Pool = row.Enum<EncounterPool>("pool"),
                    Enemies = Csv.ParseList(row["enemies"]),
                };
                if (def.Enemies.Count == 0) throw new FormatException($"{id} 没有敌人");
                foreach (var enemy in def.Enemies)
                    if (enemies.All(x => x.Id != enemy)) throw new FormatException($"{id} 引用了不存在的敌人 {enemy}");
                output.Add(def);
            });
            foreach (EncounterPool pool in Enum.GetValues(typeof(EncounterPool)))
                if (output.All(d => d.Pool != pool)) errors.Add($"{table.FileName}：{pool} 池是空的");
        }

        // ------------------------------------------------------------------ events.csv

        static void LoadEvents(CsvTable table, List<string> errors, List<EventDef> output)
        {
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"事件 ID 重复：{id}");
                if (!EventClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 EventClasses 里注册）");
                var def = new EventDef
                {
                    Id = id,
                    Name = row["name"],
                    MinFloor = row.IntOr("min_floor", 1),
                    Vars = Csv.ParseVars(row["vars"]),
                    Text = row["text"],
                    Factory = factory,
                };
                foreach (var part in row["options"].Split('|'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) throw new FormatException($"{id} 的选项格式错误：{part}（应为 选项ID=文本）");
                    def.Options.Add(new KeyValuePair<string, string>(part.Substring(0, eq).Trim(), part.Substring(eq + 1).Trim()));
                }
                string where = $"{row.Where}（{id}）";
                CheckPlaceholders(where, def.Text, def.Vars, errors);
                foreach (var kv in def.Options) CheckPlaceholders(where, kv.Value, def.Vars, errors);
                var handled = new HashSet<string>(factory().Handles);
                foreach (var kv in def.Options)
                    if (!handled.Contains(kv.Key)) errors.Add($"{where}：选项 {kv.Key} 在事件类里没有处理");
                foreach (var h in handled)
                    if (def.Options.All(kv => kv.Key != h)) errors.Add($"{where}：事件类处理了选项 {h}，但表里没有");
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, EventClasses.Factories.Keys, errors);
        }
    }
}
