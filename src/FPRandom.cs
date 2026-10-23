using System;
using System.Runtime.CompilerServices;

namespace FixedMath
{
    public class FPRandom
    {
        private uint _seed;

        public FPRandom(uint seed)
        {
            _seed = seed == 0 ? 1 : seed;
        }

        // XorShift
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NextUInt()
        {
            uint x = _seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _seed = x;
            return x;
        }

        // 返回[0, 1)之间的定点数
        public FP NextFP()
        {
            return FP.FromValue((long)NextUInt());
        }

        // 返回 [min, max] 之间的定点数
        public FP Range(FP min, FP max)
        {
            if (min > max) throw new ArgumentException("min不能大于max");
            FP range = max - min;
            return min + range * NextFP();
        }

        // 返回 [min, max) 之间的整数
        public int Range(int min, int max)
        {
            if (min >= max) return min;
            uint diff = (uint)(max - min);
            return min + (int)(NextUInt() % diff);
        }
    }
}
