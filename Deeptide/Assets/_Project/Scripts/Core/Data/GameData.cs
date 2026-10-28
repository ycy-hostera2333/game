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
        public string Characters;
        public string Echoes;

        /// <summary>Data 目录下的文件名，外层按这个名字找文件。</summary>
        public static readonly string[] FileNames = { "cards.csv", "enemies.csv", "relics.csv", "potions.csv", "encounters.csv", "events.csv", "characters.csv", "echoes.csv" };

        /// <summary>按文件名取文本，供外层统一读取：DataSource.From(name => File.ReadAllText(dir + name))。</summary>
        public static DataSource From(Func<string, string> read) => new DataSource
        {
            Cards = read("cards.csv"),
            Enemies = read("enemies.csv"),
            Relics = read("relics.csv"),
            Potions = read("potions.csv"),
            Encounters = read("encounters.csv"),
            Events = read("events.csv"),
            Characters = read("characters.csv"),
            Echoes = read("echoes.csv"),
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
    /// 敌人招式表里的每个招式都在 moves 列声明过，声明过的招式也都被用到；卡牌 / 遗物 / 药水的 character 列都指向存在的角色，
    /// 角色的起始遗物和起始牌组都存在、且不属于别的角色；回响的档位、归属角色、倾向都写对了。所有问题一次性列出来。
    /// </summary>
    public static class GameData
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        /// <summary>角色的理智上限不能低于这个值（策划/14 §1.3）。</summary>
        public const int MinMaxSanity = 4;

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
            CharacterLibrary.Clear();
            EchoLibrary.Clear();
            foreach (var d in staged.Cards) CardLibrary.Add(d);
            foreach (var d in staged.Enemies) EnemyLibrary.Add(d);
            foreach (var d in staged.Relics) RelicLibrary.Add(d);
            foreach (var d in staged.Potions) PotionLibrary.Add(d);
            foreach (var d in staged.Encounters) EncounterLibrary.Add(d);
            foreach (var d in staged.Events) EventLibrary.Add(d);
            foreach (var d in staged.Characters) CharacterLibrary.Add(d);
            foreach (var d in staged.Echoes) EchoLibrary.Add(d);
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
            public readonly List<CharacterDef> Characters = new List<CharacterDef>();
            public readonly List<EchoDef> Echoes = new List<EchoDef>();
            /// <summary>角色表里出现过的全部 id（含校验没通过的行），归属列的检查用它，免得一行写错就连带报一串「角色不存在」。</summary>
            public readonly HashSet<string> SeenCharacters = new HashSet<string>();
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
            LoadTable(source.Characters, "characters.csv", errs, t => LoadCharacters(t, errs, staged));
            LoadTable(source.Echoes, "echoes.csv", errs, t => LoadEchoes(t, errs, staged));
            CheckOwners(staged, errs);
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
                    Character = row["character"],
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
                    Character = row["character"],
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
                    Character = row["character"],
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
                    LoopText = row["loop_text"],
                    Factory = factory,
                };
                // 循环变体的出现条件：第几夜起（≥ 2），或 seen（前几夜见过这个事件）
                string loopFrom = row["loop_from"];
                if (loopFrom == "seen") def.LoopAfterSeen = true;
                else if (loopFrom.Length > 0) def.LoopFrom = row.Int("loop_from");
                if ((def.LoopText.Length > 0) != (loopFrom.Length > 0)) throw new FormatException($"{id} 的 loop_text 和 loop_from 要么都填，要么都空");
                if (loopFrom.Length > 0 && !def.LoopAfterSeen && def.LoopFrom < 2) throw new FormatException($"{id} 的 loop_from 应为 seen 或不小于 2 的夜数");
                foreach (var part in row["options"].Split('|'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) throw new FormatException($"{id} 的选项格式错误：{part}（应为 选项ID=文本）");
                    def.Options.Add(new KeyValuePair<string, string>(part.Substring(0, eq).Trim(), part.Substring(eq + 1).Trim()));
                }
                string where = $"{row.Where}（{id}）";
                CheckPlaceholders(where, def.Text, def.Vars, errors);
                CheckPlaceholders(where, def.LoopText, def.Vars, errors);
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

        // ------------------------------------------------------------------ characters.csv

        static void LoadCharacters(CsvTable table, List<string> errors, Staged staged)
        {
            var output = staged.Characters;
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                if (id.Length == 0) throw new FormatException("id 为空");
                staged.SeenCharacters.Add(id);
                if (output.Any(d => d.Id == id)) throw new FormatException($"角色 ID 重复：{id}");
                var def = new CharacterDef
                {
                    Id = id,
                    Name = row["name"],
                    Title = row["title"],
                    MaxHp = row.Int("max_hp"),
                    MaxSanity = row.Int("max_sanity"),
                    Gold = row.Int("gold"),
                    StartingRelic = row["relic"],
                    Trait = row["trait"],
                    DreamCard = row["dream_card"],
                    Deck = ParseDeck(row["deck"]),
                    Text = row["text"],
                    Archetypes = staged.Cards
                        .Where(c => c.Character == id && c.Archetype.Length > 0 && !RunTelemetry.NotArchetypes.Contains(c.Archetype))
                        .Select(c => c.Archetype).Distinct().ToList(),
                };
                if (def.Name.Length == 0) throw new FormatException($"{id} 没有名字");
                if (def.MaxHp <= 0) throw new FormatException($"{id} 的最大生命必须大于 0");
                if (def.MaxSanity < MinMaxSanity) throw new FormatException($"{id} 的理智上限 {def.MaxSanity} 低于 {MinMaxSanity}");
                if (def.Gold < 0) throw new FormatException($"{id} 的初始金币不能为负");

                var relic = staged.Relics.FirstOrDefault(r => r.Id == def.StartingRelic);
                if (relic == null) throw new FormatException($"{id} 的起始遗物 {def.StartingRelic} 不存在");
                if (relic.Rarity != RelicRarity.Starter) throw new FormatException($"{id} 的起始遗物 {relic.Id} 的稀有度不是 Starter");
                if (!CharacterLibrary.Allows(relic.Character, id)) throw new FormatException($"{id} 的起始遗物 {relic.Id} 属于角色 {relic.Character}");

                if (def.Trait.Length > 0)
                {
                    var trait = staged.Relics.FirstOrDefault(r => r.Id == def.Trait);
                    if (trait == null) throw new FormatException($"{id} 的特性 {def.Trait} 不存在");
                    if (trait.Rarity != RelicRarity.Trait) throw new FormatException($"{id} 的特性 {trait.Id} 的稀有度不是 Trait");
                    if (!CharacterLibrary.Allows(trait.Character, id)) throw new FormatException($"{id} 的特性 {trait.Id} 属于角色 {trait.Character}");
                }

                var dream = staged.Cards.FirstOrDefault(c => c.Id == def.DreamCard);
                if (dream == null) throw new FormatException($"{id} 的梦中之牌 {def.DreamCard} 不存在");
                if (!CharacterLibrary.Allows(dream.Character, id)) throw new FormatException($"{id} 的梦中之牌 {dream.Id} 属于角色 {dream.Character}");

                if (def.Deck.Count == 0) throw new FormatException($"{id} 的起始牌组是空的");
                foreach (var cardId in def.Deck.Distinct())
                {
                    var card = staged.Cards.FirstOrDefault(c => c.Id == CardLibrary.StripUpgrade(cardId, out _));
                    if (card == null) throw new FormatException($"{id} 的起始牌组里有不存在的牌 {cardId}");
                    if (!CharacterLibrary.Allows(card.Character, id)) throw new FormatException($"{id} 的起始牌组里的 {cardId} 属于角色 {card.Character}");
                }
                output.Add(def);
            });
            if (!staged.SeenCharacters.Contains(CharacterIds.Investigator))
                errors.Add($"{table.FileName}：缺少默认角色 {CharacterIds.Investigator}（RunConfig 和旧存档默认用它）");
        }

        // ------------------------------------------------------------------ echoes.csv

        static void LoadEchoes(CsvTable table, List<string> errors, Staged staged)
        {
            var output = staged.Echoes;
            var ids = new List<string>();
            EachRow(table, errors, row =>
            {
                string id = row["id"];
                ids.Add(id);
                if (id.Length == 0) throw new FormatException("id 为空");
                if (output.Any(d => d.Id == id)) throw new FormatException($"回响 ID 重复：{id}");
                if (!EchoClasses.Factories.TryGetValue(id, out var factory)) throw new FormatException($"{id} 没有对应的类（在 EchoClasses 里注册）");

                var def = new EchoDef
                {
                    Id = id,
                    Name = row["name"],
                    Tier = row.Enum<EchoTier>("tier"),
                    Character = row["character"],
                    Tendency = row["tendency"],
                    Vars = Csv.ParseVars(row["vars"]),
                    Text = row["text"],
                    Factory = factory,
                };
                if (def.Name.Length == 0) throw new FormatException($"{id} 没有名字");
                if (def.Text.Length == 0) throw new FormatException($"{id} 没有效果文本");

                // 角色表没载入时不查归属和倾向（同 CheckOwners），免得一条错误刷满屏
                if (staged.SeenCharacters.Count > 0)
                {
                    if (def.Character.Length > 0 && !staged.SeenCharacters.Contains(def.Character))
                        throw new FormatException($"{id} 的 character 列是不存在的角色 {def.Character}");
                    // 专属回响的倾向只能是本角色的流派；通用回响可以是任何角色的流派（对别的角色按中立算）
                    var allowed = staged.Characters.Where(c => def.Character.Length == 0 || c.Id == def.Character)
                        .SelectMany(c => c.Archetypes).Distinct().Prepend(EchoDef.Neutral).ToList();
                    if (!allowed.Contains(def.Tendency))
                        throw new FormatException($"{id} 的倾向「{def.Tendency}」不对，可选：{string.Join(" / ", allowed)}");
                }
                CheckPlaceholders($"{row.Where}（{id}）", def.Text, def.Vars, errors);

                def.AsRelic = new RelicDef
                {
                    Id = id,
                    Name = def.Name,
                    Rarity = RelicRarity.Echo,
                    Character = def.Character,
                    Tendency = def.Tendency,
                    Vars = def.Vars,
                    Text = def.Text,
                    Factory = factory,
                };
                output.Add(def);
            });
            CheckClasses(table.FileName, ids, EchoClasses.Factories.Keys, errors);
        }

        /// <summary>「B01*5;B02*3;C01」：ID*N 表示 N 张，按书写顺序展开。</summary>
        static List<string> ParseDeck(string text)
        {
            var deck = new List<string>();
            foreach (var item in Csv.ParseList(text))
            {
                int star = item.IndexOf('*');
                if (star < 0)
                {
                    deck.Add(item);
                    continue;
                }
                string id = item.Substring(0, star).Trim();
                if (!int.TryParse(item.Substring(star + 1).Trim(), out int n) || n <= 0 || id.Length == 0)
                    throw new FormatException($"牌组写法错误：{item}（应为 ID 或 ID*张数）");
                for (int i = 0; i < n; i++) deck.Add(id);
            }
            return deck;
        }

        /// <summary>卡牌 / 遗物 / 药水表的 character 列只能填角色表里有的 id（空 = 通用）。角色表本身没载入时不查，免得一条错误刷满屏。</summary>
        static void CheckOwners(Staged staged, List<string> errors)
        {
            var known = staged.SeenCharacters;
            if (known.Count == 0) return;
            void Check(string file, string id, string owner)
            {
                if (owner.Length > 0 && !known.Contains(owner)) errors.Add($"{file}：{id} 的 character 列是不存在的角色 {owner}");
            }
            foreach (var d in staged.Cards) Check("cards.csv", d.Id, d.Character);
            foreach (var d in staged.Relics) Check("relics.csv", d.Id, d.Character);
            foreach (var d in staged.Potions) Check("potions.csv", d.Id, d.Character);
        }
    }
}
