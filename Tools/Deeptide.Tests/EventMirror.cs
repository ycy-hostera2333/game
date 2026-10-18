using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Deeptide.Core;

namespace Deeptide.Tests
{
    /// <summary>
    /// 只读事件流来重建战斗状态（策划/09 M1.7 的验收工具）。表现层的 BattleDirector 要做的事情和它一样：
    /// 按顺序消费状态事件，维护一份自己的数值。和内核状态比对一致，说明事件流是完整的。
    /// 抽牌堆的顺序是隐藏信息，只比对内容；手牌比对顺序。
    /// </summary>
    sealed class EventMirror
    {
        sealed class Unit
        {
            public string DefId, Name;
            public int Hp, MaxHp, Block, Fear, Will;
            public bool Stunned, Minion;
            public string Intent;
            public readonly SortedDictionary<StatusId, int> Statuses = new SortedDictionary<StatusId, int>();
            public readonly SortedDictionary<string, int> Counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        }

        readonly Unit _player = new Unit();
        int _energy, _energyNext, _sanity, _maxSanity, _turn;
        readonly List<Unit> _enemies = new List<Unit>();
        readonly Dictionary<int, string> _cardIds = new Dictionary<int, string>();
        readonly Dictionary<int, int> _tempCost = new Dictionary<int, int>();
        readonly List<int> _hand = new List<int>();
        readonly HashSet<int> _draw = new HashSet<int>(), _discard = new HashSet<int>(), _exhaust = new HashSet<int>();
        readonly SortedDictionary<string, int> _powers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<int, string> _potions = new Dictionary<int, string>();
        int _cursor;

        Unit U(int entity) => entity == BattleEvent.PlayerEntity ? _player : _enemies[entity];

        HashSet<int> Set(PileKind pile)
        {
            switch (pile)
            {
                case PileKind.Draw: return _draw;
                case PileKind.Discard: return _discard;
                case PileKind.Exhaust: return _exhaust;
                default: return null;
            }
        }

        public void Consume(BattleLog log)
        {
            for (; _cursor < log.Events.Count; _cursor++) Apply(log.Events[_cursor]);
        }

        void Apply(BattleEvent ev)
        {
            switch (ev.Type)
            {
                case BattleEventType.PlayerInit:
                    _player.Hp = ev.Value;
                    _player.MaxHp = ev.Value2;
                    _sanity = _maxSanity = ev.Amount;
                    break;
                case BattleEventType.EnemySpawned:
                    if (ev.Entity != _enemies.Count) throw new InvalidOperationException("敌人编号不连续");
                    _enemies.Add(new Unit { DefId = ev.Id, Name = ev.Actor, Hp = ev.Value, MaxHp = ev.Value2, Will = ev.Amount, Minion = ev.Flag });
                    break;
                case BattleEventType.TurnStarted: _turn = ev.Value; break;
                case BattleEventType.HpChanged:
                    U(ev.Entity).Hp = ev.Value;
                    U(ev.Entity).MaxHp = ev.Value2;
                    break;
                case BattleEventType.BlockChanged: U(ev.Entity).Block = ev.Value; break;
                case BattleEventType.EnergyChanged:
                    _energy = ev.Value;
                    _energyNext = ev.Value2;
                    break;
                case BattleEventType.SanityChanged:
                    _sanity = ev.Value;
                    _maxSanity = ev.Value2;
                    break;
                case BattleEventType.StatusChanged:
                    if (ev.Value == 0) U(ev.Entity).Statuses.Remove(ev.Status);
                    else U(ev.Entity).Statuses[ev.Status] = ev.Value;
                    break;
                case BattleEventType.FearChanged:
                    U(ev.Entity).Fear = ev.Value;
                    U(ev.Entity).Will = ev.Value2;
                    break;
                case BattleEventType.StunChanged: U(ev.Entity).Stunned = ev.Value != 0; break;
                case BattleEventType.IntentChosen: U(ev.Entity).Intent = ev.Id; break;
                case BattleEventType.CounterChanged: U(ev.Entity).Counters[ev.Id] = ev.Value; break;
                case BattleEventType.PowerChanged: _powers[ev.Id] = ev.Value; break;
                case BattleEventType.PotionChanged: _potions[ev.Value] = ev.Id; break;
                case BattleEventType.CardCostChanged:
                    if (ev.Value < 0) _tempCost.Remove(ev.CardUid);
                    else _tempCost[ev.CardUid] = ev.Value;
                    break;
                case BattleEventType.CardMoved:
                    _cardIds[ev.CardUid] = ev.Id;
                    if (ev.From == PileKind.Hand) _hand.Remove(ev.CardUid);
                    else Set(ev.From)?.Remove(ev.CardUid);
                    if (ev.To == PileKind.Hand) _hand.Insert(Math.Min(ev.Value, _hand.Count), ev.CardUid);
                    else Set(ev.To)?.Add(ev.CardUid);
                    break;
                case BattleEventType.PileShuffled:
                    foreach (var uid in _discard) _draw.Add(uid);
                    _discard.Clear();
                    break;
            }
        }

        /// <summary>把镜像状态和内核状态各自描述成文本，返回第一处不同；一致返回 null。</summary>
        public string Diff(BattleEngine e)
        {
            var mine = Describe();
            var real = Describe(e);
            if (mine == real) return null;
            var a = mine.Split('\n');
            var b = real.Split('\n');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "<无>", y = i < b.Length ? b[i] : "<无>";
                if (x != y) return $"事件重建：{x}\n内核状态：{y}";
            }
            return "不同";
        }

        string Describe()
        {
            var sb = new StringBuilder();
            sb.Append($"T{_turn} P {_player.Hp}/{_player.MaxHp} B{_player.Block} E{_energy}+{_energyNext} S{_sanity}/{_maxSanity}");
            sb.Append(Statuses(_player.Statuses)).Append('\n');
            sb.Append("pow ").Append(string.Join(",", _powers.Select(kv => $"{kv.Key}={kv.Value}"))).Append('\n');
            sb.Append("pot ").Append(string.Join(",", _potions.OrderBy(kv => kv.Key).Select(kv => kv.Value ?? "-"))).Append('\n');
            sb.Append("hand ").Append(string.Join(",", _hand.Select(Card))).Append('\n');
            sb.Append("draw ").Append(string.Join(",", _draw.OrderBy(x => x).Select(Card))).Append('\n');
            sb.Append("disc ").Append(string.Join(",", _discard.OrderBy(x => x).Select(Card))).Append('\n');
            sb.Append("exh ").Append(string.Join(",", _exhaust.OrderBy(x => x).Select(Card))).Append('\n');
            for (int i = 0; i < _enemies.Count; i++)
            {
                var u = _enemies[i];
                sb.Append($"E{i} {u.DefId} {u.Name} {u.Hp}/{u.MaxHp} B{u.Block} F{u.Fear}/{u.Will}{(u.Stunned ? " stun" : "")}{(u.Minion ? " minion" : "")} I:{(u.Hp > 0 ? u.Intent : "-")}");
                sb.Append(Statuses(u.Statuses)).Append(Counters(u.Counters)).Append('\n');
            }
            return sb.ToString();
        }

        string Card(int uid) => _cardIds[uid] + "#" + uid + (_tempCost.TryGetValue(uid, out var c) ? "$" + c : "");

        static string Describe(BattleEngine e)
        {
            var p = e.Player;
            var statuses = new SortedDictionary<StatusId, int>(p.Statuses.Ordered().ToDictionary(kv => kv.Key, kv => kv.Value));
            string C(Deeptide.Core.Cards.Card c) => c.DeckId + "#" + c.Uid + (c.CostThisTurn >= 0 ? "$" + c.CostThisTurn : "");
            var sb = new StringBuilder();
            sb.Append($"T{e.State.Turn} P {p.Hp}/{p.MaxHp} B{p.Block} E{p.Energy}+{p.EnergyNextTurn} S{p.Sanity}/{p.MaxSanity}");
            sb.Append(Statuses(statuses)).Append('\n');
            sb.Append("pow ").Append(string.Join(",", p.Powers.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => $"{x.Id}={x.Amount}"))).Append('\n');
            sb.Append("pot ").Append(string.Join(",", p.Potions.Select(x => x?.Id ?? "-"))).Append('\n');
            sb.Append("hand ").Append(string.Join(",", p.Hand.Select(C))).Append('\n');
            sb.Append("draw ").Append(string.Join(",", p.DrawPile.OrderBy(x => x.Uid).Select(C))).Append('\n');
            sb.Append("disc ").Append(string.Join(",", p.DiscardPile.OrderBy(x => x.Uid).Select(C))).Append('\n');
            sb.Append("exh ").Append(string.Join(",", p.ExhaustPile.OrderBy(x => x.Uid).Select(C))).Append('\n');
            foreach (var en in e.State.Enemies)
            {
                var st = new SortedDictionary<StatusId, int>(en.Statuses.Ordered().ToDictionary(kv => kv.Key, kv => kv.Value));
                sb.Append($"E{en.Index} {en.Def.Id} {en.Name} {en.Hp}/{en.MaxHp} B{en.Block} F{en.Fear}/{en.Will}{(en.Stunned ? " stun" : "")}{(en.Minion ? " minion" : "")} I:{(en.Hp > 0 ? en.Intent?.Id : "-")}");
                sb.Append(Statuses(st)).Append(Counters(en.Counters)).Append('\n');
            }
            return sb.ToString();
        }

        static string Statuses(SortedDictionary<StatusId, int> s) => string.Concat(s.Select(kv => $" {kv.Key}={kv.Value}"));
        static string Counters(SortedDictionary<string, int> c) => string.Concat(c.Select(kv => $" #{kv.Key}={kv.Value}"));
    }
}
