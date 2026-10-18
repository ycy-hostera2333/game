using System;
using System.Collections.Generic;

namespace Deeptide.Core.Random
{
    /// <summary>
    /// PCG32（XSH-RR）伪随机数发生器。
    /// 不用 System.Random：它的实现可能随运行时版本变化，存档和回放无法保证可复现（策划/08 §5）。
    /// </summary>
    public sealed class Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        readonly ulong _increment;

        public Pcg32(ulong seed, ulong stream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        Pcg32(ulong state, ulong increment, bool raw)
        {
            _state = state;
            _increment = increment;
        }

        /// <summary>从存档里的原始状态恢复（State / Increment 两个值）。</summary>
        public static Pcg32 FromRaw(ulong state, ulong increment) => new Pcg32(state, increment, true);

        public ulong State => _state;
        public ulong Increment => _increment;

        /// <summary>SplitMix64：从一个种子派生出互不相关的子种子（每场战斗的种子 = 派生(局种子, 战斗序号)）。</summary>
        public static ulong Mix(ulong seed, ulong salt)
        {
            ulong z = unchecked(seed + 0x9E3779B97F4A7C15UL * (salt + 1));
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        /// <summary>[0, 100) 内的整数，用于百分比判定。</summary>
        public int Percent() => NextInt(100);

        /// <summary>从列表里等概率挑一个。</summary>
        public T Pick<T>(IReadOnlyList<T> list) => list[NextInt(list.Count)];

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>[0, maxExclusive) 内的无偏整数（拒绝采样）。</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint bound = (uint)maxExclusive;
            uint threshold = (uint)((0x1_0000_0000UL - bound) % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>[minInclusive, maxInclusive] 内的整数。</summary>
        public int Range(int minInclusive, int maxInclusive) => minInclusive + NextInt(maxInclusive - minInclusive + 1);

        /// <summary>Fisher–Yates 洗牌。</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
