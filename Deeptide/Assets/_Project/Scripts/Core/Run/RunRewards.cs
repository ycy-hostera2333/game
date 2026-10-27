using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Potions;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;

namespace Deeptide.Core.Run
{
    /// <summary>奖励、宝箱和商店（策划/06 §3–5）。卡牌奖励用 reward 流，商店用 shop 流。</summary>
    public sealed partial class RunEngine
    {
        /// <summary>可以出现在奖励 / 商店里的角色牌：本角色和通用的，不含状态牌和诅咒。</summary>
        public IEnumerable<CardDef> CharacterCards(Rarity rarity) =>
            CardLibrary.All.Where(d => d.Rarity == rarity && d.Type != CardType.Curse && d.Type != CardType.Status && Mine(d.Character));

        /// <summary>归属列 owner 的东西能不能在这一局出现（通用的，或者属于当前角色的，策划/14 §1.1）。</summary>
        bool Mine(string owner) => CharacterLibrary.Allows(owner, State.Character);

        /// <summary>
        /// 抽一张卡的稀有度（策划/06 §4.1）：普通战斗 60 / 37 / 3，精英 50 / 40 / 10；
        /// 稀有率 = 基础稀有率 + 保底偏移（下限 0）。出普通卡偏移 +1，出稀有卡重置为 −5。
        /// </summary>
        internal Rarity RollCardRarity(Pcg32 rng, bool elite, bool usePity)
        {
            int rare = elite ? 10 : 3, uncommon = elite ? 40 : 37;
            if (usePity) rare = Math.Max(0, rare + State.RarePity);
            int roll = rng.Percent();
            if (roll < rare)
            {
                if (usePity) State.RarePity = -5;
                return Rarity.Rare;
            }
            if (roll < rare + uncommon) return Rarity.Uncommon;
            if (usePity) State.RarePity++;
            return Rarity.Common;
        }

        /// <summary>生成 count 张互不相同的卡牌奖励。</summary>
        internal List<string> RollCardChoices(Pcg32 rng, int count, bool elite)
        {
            var result = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var rarity = RollCardRarity(rng, elite, true);
                var pool = CharacterCards(rarity).Where(d => !result.Contains(d.Id)).ToList();
                if (pool.Count == 0) pool = CharacterCards(Rarity.Common).Where(d => !result.Contains(d.Id)).ToList();
                result.Add(pool[rng.NextInt(pool.Count)].Id);
            }
            return result;
        }

        /// <summary>从某个稀有度里随机挑 count 张不同的牌（事件用）。</summary>
        internal List<string> RandomCards(Pcg32 rng, int count, Func<CardDef, bool> filter)
        {
            var pool = CardLibrary.All.Where(d => d.Type != CardType.Curse && d.Type != CardType.Status && d.Rarity != Rarity.Basic && Mine(d.Character) && filter(d)).ToList();
            var result = new List<string>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.NextInt(pool.Count);
                result.Add(pool[k].Id);
                pool.RemoveAt(k);
            }
            return result;
        }

        /// <summary>遗物稀有度：普通 50% / 罕见 33% / 稀有 17%（宝箱、精英、祭坛）。</summary>
        internal static RelicRarity RollRelicRarity(Pcg32 rng)
        {
            int roll = rng.Percent();
            return roll < 50 ? RelicRarity.Common : roll < 83 ? RelicRarity.Uncommon : RelicRarity.Rare;
        }

        /// <summary>随机一件还没有的遗物；这个稀有度拿光了就往高一档找，再找不到返回 null。</summary>
        internal string RollRelic(Pcg32 rng, RelicRarity rarity)
        {
            var order = new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare };
            for (int i = Array.IndexOf(order, rarity); i < order.Length + Array.IndexOf(order, rarity); i++)
            {
                var r = order[i % order.Length];
                var pool = RelicLibrary.All.Where(d => d.Rarity == r && !HasRelic(d.Id) && Mine(d.Character)).ToList();
                if (pool.Count > 0) return pool[rng.NextInt(pool.Count)].Id;
            }
            return null;
        }

        internal string RollPotion(Pcg32 rng)
        {
            var rarity = rng.Percent() < 70 ? PotionRarity.Common : PotionRarity.Uncommon;
            var pool = PotionLibrary.All.Where(d => d.Rarity == rarity && Mine(d.Character)).ToList();
            return pool[rng.NextInt(pool.Count)].Id;
        }

        void AddRelicReward(RelicRarity rarity)
        {
            var id = RollRelic(Rng.Reward, rarity);
            if (id != null) State.Rewards.Add(new RewardItem { Kind = RewardKind.Relic, Id = id });
        }

        /// <summary>
        /// 战斗奖励（策划/06 §4）：金币；精英再给 1 件遗物；药水按掉率（掉了 −10%，没掉 +10%）；卡牌 3 选 1（收藏家的放大镜 +1）。
        /// 生成顺序固定：金币 → 遗物 → 药水 → 卡牌。
        /// </summary>
        void GenerateBattleRewards(bool elite)
        {
            State.Rewards = new List<RewardItem>
            {
                new RewardItem { Kind = RewardKind.Gold, Amount = elite ? Rng.Reward.Range(25, 35) : Rng.Reward.Range(10, 20) },
            };
            if (elite) AddRelicReward(RollRelicRarity(Rng.Reward));
            if (Rng.Reward.Percent() < State.PotionChance)
            {
                State.PotionChance = Math.Max(0, State.PotionChance - 10);
                State.Rewards.Add(new RewardItem { Kind = RewardKind.Potion, Id = RollPotion(Rng.Reward) });
            }
            else State.PotionChance = Math.Min(100, State.PotionChance + 10);
            int count = 3 + (HasRelic(RelicIds.CollectorsLens) ? RelicLibrary.Get(RelicIds.CollectorsLens).Vars["Choices"] : 0);
            State.Rewards.Add(new RewardItem { Kind = RewardKind.Cards, Amount = count, Cards = RollCardChoices(Rng.Reward, count, elite) });
        }

        List<RunOption> RewardOptions()
        {
            var options = new List<RunOption>();
            for (int i = 0; i < State.Rewards.Count; i++)
            {
                var r = State.Rewards[i];
                if (r.Taken) continue;
                int index = i;
                switch (r.Kind)
                {
                    case RewardKind.Gold:
                        options.Add(new RunOption { Label = $"{r.Amount} 金币", Tag = "gold", Do = () => { GainGold(r.Amount); r.Taken = true; } });
                        break;
                    case RewardKind.Relic:
                        var relic = RelicLibrary.Create(r.Id);
                        options.Add(new RunOption { Label = $"遗物：{relic.Name}——{relic.Description}", Tag = "relic:" + r.Id, Do = () => { AddRelic(r.Id); r.Taken = true; } });
                        break;
                    case RewardKind.Potion:
                        var potion = PotionLibrary.Create(r.Id);
                        options.Add(new RunOption
                        {
                            Label = $"药水：{potion.Name}——{potion.Description}",
                            Tag = "potion:" + r.Id,
                            Do = () => { r.Taken = true; AddPotionOrAskDiscard(r.Id, RunPhase.Reward); },
                        });
                        break;
                    case RewardKind.Cards:
                        options.Add(new RunOption
                        {
                            Label = $"卡牌：从 {r.Cards.Count} 张里选 1 张",
                            Tag = "cards",
                            Do = () => OfferCards(r.Cards, "选 1 张牌加入牌组", RunPhase.Reward, index),
                        });
                        break;
                }
            }
            options.Add(new RunOption { Label = "继续前进", Tag = "leave", Do = GoToMap });
            return options;
        }

        // ------------------------------------------------------------------ 商店

        static (int Min, int Max) CardPrice(Rarity r) =>
            r == Rarity.Rare ? (135, 165) : r == Rarity.Uncommon ? (68, 82) : (45, 55);

        static (int Min, int Max) RelicPrice(RelicRarity r) =>
            r == RelicRarity.Rare ? (285, 315) : r == RelicRarity.Uncommon ? (238, 262) : (143, 157);

        static (int Min, int Max) PotionPrice(PotionRarity r) => r == PotionRarity.Uncommon ? (72, 78) : (48, 52);

        /// <summary>
        /// 商店（策划/06 §5）：5 张角色牌（攻击 2 / 技能 2 / 能力 1，随机一张半价）、3 件遗物（其中 1 件商店专属）、3 瓶药水、删卡服务。
        /// </summary>
        ShopState GenerateShop()
        {
            var rng = Rng.Shop;
            var shop = new ShopState();
            foreach (var type in new[] { CardType.Attack, CardType.Attack, CardType.Skill, CardType.Skill, CardType.Power })
            {
                var rarity = RollCardRarity(rng, false, false);
                var taken = shop.Items.Select(x => x.Id).ToList();
                var pool = CharacterCards(rarity).Where(d => d.Type == type && !taken.Contains(d.Id)).ToList();
                // 没有普通能力牌：能力栏位抽到普通时升一档
                for (var r = rarity; pool.Count == 0 && r < Rarity.Rare; )
                {
                    r++;
                    pool = CharacterCards(r).Where(d => d.Type == type && !taken.Contains(d.Id)).ToList();
                }
                if (pool.Count == 0) pool = CharacterCards(rarity).Where(d => !taken.Contains(d.Id)).ToList();
                var def = pool[rng.NextInt(pool.Count)];
                var (min, max) = CardPrice(def.Rarity);
                shop.Items.Add(new ShopItem { Kind = ShopItemKind.Card, Id = def.Id, Price = rng.Range(min, max) });
            }
            var sale = shop.Items[rng.NextInt(shop.Items.Count)];
            sale.Price /= 2;

            var relics = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var rarity = RollRelicRarity(rng);
                var pool = RelicLibrary.All.Where(d => d.Rarity == rarity && !HasRelic(d.Id) && !relics.Contains(d.Id) && Mine(d.Character)).ToList();
                if (pool.Count == 0) pool = RelicLibrary.All.Where(d => d.Rarity != RelicRarity.Starter && d.Rarity != RelicRarity.Shop && !HasRelic(d.Id) && !relics.Contains(d.Id) && Mine(d.Character)).ToList();
                if (pool.Count == 0) continue;
                relics.Add(pool[rng.NextInt(pool.Count)].Id);
            }
            var shopOnly = RelicLibrary.All.Where(d => d.Rarity == RelicRarity.Shop && !HasRelic(d.Id) && Mine(d.Character)).ToList();
            if (shopOnly.Count > 0) relics.Add(shopOnly[rng.NextInt(shopOnly.Count)].Id);
            foreach (var id in relics)
            {
                var (min, max) = RelicPrice(RelicLibrary.Get(id).Rarity);
                shop.Items.Add(new ShopItem { Kind = ShopItemKind.Relic, Id = id, Price = rng.Range(min, max) });
            }

            for (int i = 0; i < 3; i++)
            {
                var id = RollPotion(rng);
                var (min, max) = PotionPrice(PotionLibrary.Get(id).Rarity);
                shop.Items.Add(new ShopItem { Kind = ShopItemKind.Potion, Id = id, Price = rng.Range(min, max) });
            }
            return shop;
        }

        /// <summary>实际售价：典当行的天平打八折（向上取整）。</summary>
        public int PriceOf(int basePrice)
        {
            if (!HasRelic(RelicIds.PawnbrokersScale)) return basePrice;
            int discount = RelicLibrary.Get(RelicIds.PawnbrokersScale).Vars["Discount"];
            return (basePrice * (100 - discount) + 99) / 100;
        }

        /// <summary>删卡价格：75 起，每用一次 +25（整局累计）。</summary>
        public int RemovalPrice => PriceOf(75 + 25 * State.Removals);

        List<RunOption> ShopOptions()
        {
            var shop = State.Shop;
            var options = new List<RunOption>();
            foreach (var item in shop.Items)
            {
                var it = item;
                int price = PriceOf(it.Price);
                string name;
                switch (it.Kind)
                {
                    case ShopItemKind.Card: name = CardLabel(it.Id); break;
                    case ShopItemKind.Relic:
                        var relic = RelicLibrary.Create(it.Id);
                        name = $"遗物 {relic.Name}——{relic.Description}";
                        break;
                    default:
                        var potion = PotionLibrary.Create(it.Id);
                        name = $"药水 {potion.Name}——{potion.Description}";
                        break;
                }
                var opt = new RunOption
                {
                    Label = it.Sold ? $"（已售出）{name}" : $"{price} 金币：{name}",
                    CardId = it.Kind == ShopItemKind.Card ? it.Id : null,
                    Tag = "shop:" + it.Kind.ToString().ToLowerInvariant() + ":" + it.Id,
                    Do = () => Buy(it, price),
                };
                if (it.Sold) { opt.Enabled = false; opt.Reason = "已经卖掉了"; }
                else if (State.Gold < price) { opt.Enabled = false; opt.Reason = "金币不够"; }
                else if (it.Kind == ShopItemKind.Potion && !State.Potions.Contains(null)) { opt.Enabled = false; opt.Reason = "药水栏满了（可以先 drop 一瓶）"; }
                options.Add(opt);
            }
            int removal = RemovalPrice;
            var removable = Enumerable.Range(0, State.Deck.Count).ToList();
            options.Add(new RunOption
            {
                Label = shop.RemovalUsed ? "删卡服务（这次已经用过）" : $"{removal} 金币：从牌组删除 1 张牌",
                Tag = "remove",
                Enabled = !shop.RemovalUsed && State.Gold >= removal && removable.Count > 0,
                Reason = shop.RemovalUsed ? "每次进店只能删一张" : "金币不够",
                Do = () => AskDeckCard(SelectPurpose.RemoveCard, $"删除哪张牌？（{removal} 金币）", RunPhase.Shop, RunPhase.Shop, removable,
                    goldCost: removal, shopRemoval: true),
            });
            options.Add(new RunOption { Label = "离开商店", Tag = "leave", Do = GoToMap });
            return options;
        }

        void Buy(ShopItem it, int price)
        {
            State.Gold -= price;
            it.Sold = true;
            switch (it.Kind)
            {
                case ShopItemKind.Card: State.Deck.Add(it.Id); break;
                case ShopItemKind.Relic: AddRelic(it.Id); break;
                default: TryAddPotion(it.Id); break;
            }
        }
    }
}
