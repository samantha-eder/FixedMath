using System;
using System.Runtime.CompilerServices;

namespace FixedMath
{
    public readonly struct FPVector2 : IEquatable<FPVector2>
    {
        public readonly FP x;
        public readonly FP y;

        public FPVector2(FP x, FP y)
        {
            this.x = x;
            this.y = y;
        }

        #region 常量定义
        public static readonly FPVector2 Zero = new FPVector2(FP.Zero, FP.Zero);
        public static readonly FPVector2 One = new FPVector2(FP.One, FP.One);
        public static readonly FPVector2 Up = new FPVector2(FP.Zero, FP.One);
        public static readonly FPVector2 Down = new FPVector2(FP.Zero, FP.NegOne);
        public static readonly FPVector2 Left = new FPVector2(FP.NegOne, FP.Zero);
        public static readonly FPVector2 Right = new FPVector2(FP.One, FP.Zero);
        #endregion

        #region 运算符
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator +(FPVector2 a, FPVector2 b)
        {
            return new FPVector2(a.x + b.x, a.y + b.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a, FPVector2 b)
        {
            return new FPVector2(a.x - b.x, a.y - b.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a)
        {
            return new FPVector2(-a.x, -a.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FPVector2 a, FP d)
        {
            return new FPVector2(a.x * d, a.y * d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FP d, FPVector2 a)
        {
            return new FPVector2(a.x * d, a.y * d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator /(FPVector2 a, FP d)
        {
            return new FPVector2(a.x / d, a.y / d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector2 a, FPVector2 b)
        {
            return a.x == b.x && a.y == b.y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector2 a, FPVector2 b)
        {
            return a.x != b.x || a.y != b.y;
        }
        #endregion

        #region 数学库-基础
        //点乘与叉乘
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FPVector2 a, FPVector2 b)
        {
            return a.x * b.x + a.y * b.y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cross(FPVector2 a, FPVector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        //向量的模长
        public static FP Magnitude(FPVector2 a)
        {
            ulong x = (ulong)(a.x.Value >= 0 ? a.x.Value : -a.x.Value);
            ulong y = (ulong)(a.y.Value >= 0 ? a.y.Value : -a.y.Value);

            if (x == 0 && y == 0) return FP.Zero;

            //算出x y的128位高精度平方值
            Square128(x, out ulong x_h, out ulong x_l);
            Square128(y, out ulong y_h, out ulong y_l);

            //将这两个数加起来并记录进位信息
            ulong num_l = x_l + y_l;
            ulong c = (num_l < x_l) ? 1UL : 0UL;
            ulong num_h = x_h + y_h + c;

            //对得到的128位超大整数进行开方
            //num_h num_l
            ulong rem_h = 0, rem_l = 0;
            ulong root = 0;

            //每次从num的最高位取两个bit移入余数rem中
            //从最高组一直处理到最低组共64次 得到的root就是Q32.32的开方数
            for (int i = 0; i < 64; i++)
            {
                //将当前值左移一位准备确定下一位的值
                root <<= 1;

                //将旧余数放大4倍 (左移2位) 并拼上新拉下来的2位数
                //得到新的需要计算的残差rem
                rem_h = (rem_h << 2) | (rem_l >> 62);
                rem_l = (rem_l << 2) | (num_h >> 62);

                //将剩下的数往前推进两位
                //相当于开始测试下一组的两个bit位
                num_h = (num_h << 2) | (num_l >> 62);
                num_l = (num_l << 2);

                //由于开始时已经将root<<1
                //此时4Q+1 只需要让root<<即可
                ulong test_h = root >> 63;
                ulong test_l = (root << 1) | 1UL;

                //如果够减 rem >= test
                bool en = false;
                if (rem_h > test_h) en = true;
                else if ((rem_h == test_h) && (rem_l >= test_l)) en = true;

                //把末尾那个刚才补的0变成1
                //相当于将当前位置为1
                if (en)
                {
                    // rem -= test
                    ulong borrow = (rem_l < test_l) ? 1UL : 0UL;
                    rem_h = rem_h - test_h - borrow;
                    rem_l = rem_l - test_l;

                    //将当前位置为1
                    root |= 1UL;
                }
            }

            //得到的root就是严格的Q32.32模长
            if (root > 0x7FFFFFFFFFFFFFFFUL) throw new OverflowException("向量模长超出 Q32.32 最大范围");
            return FP.FromValue((long)root);
        }
        public static FP SquareMagnitude(FPVector2 a)
        {
            return a.x * a.x + a.y * a.y;
        }

        //向量的归一化
        public static FPVector2 Normalize(FPVector2 a)
        {
            ulong ux = (ulong)(a.x.Value >= 0 ? a.x.Value : -a.x.Value);
            ulong uy = (ulong)(a.y.Value >= 0 ? a.y.Value : -a.y.Value);

            ulong max = ux | uy;
            if (max == 0) return Zero;

            //寻找最高位msb
            int msb = 0;
            if (max >= (1UL << 32)) { max >>= 32; msb += 32; }
            if (max >= (1UL << 16)) { max >>= 16; msb += 16; }
            if (max >= (1UL << 8)) { max >>= 8; msb += 8; }
            if (max >= (1UL << 4)) { max >>= 4; msb += 4; }
            if (max >= (1UL << 2)) { max >>= 2; msb += 2; }
            if (max >= (1UL << 1)) { msb += 1; }

            //将向量等比缩放到第32位
            long x, y;
            int shift = 32 - msb;
            if (shift >= 0)
            {
                //msb <= 32 左移放大
                x = a.x.Value << shift;
                y = a.y.Value << shift;
            }
            else
            {
                //msb > 32 右移缩小
                int right = -shift;
                x = a.x.Value >> right;
                y = a.y.Value >> right;
            }

            //用缩放后的坐标构造一个新向量 并求它的模长
            FPVector2 scaled = new FPVector2(FP.FromValue(x), FP.FromValue(y));
            FP mag = Magnitude(scaled);

            return new FPVector2(scaled.x / mag, scaled.y / mag);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Scale(FPVector2 a, FPVector2 b)
        {
            return new FPVector2(a.x * b.x, a.y * b.y);
        }

        //角度
        public static FP Angle(FPVector2 a, FPVector2 b)
        {
            // |Cross(a, b)| = |a||b|sin(θ)
            //  Dot(a, b)    = |a||b|cos(θ)
            // 通过 Atan2 求出 [0, 180] 度夹角
            FP cross = Cross(a, b);
            FP y = cross >= FP.Zero ? cross : -cross;
            FP x = Dot(a, b);
            return FP.Atan2(y, x) * FP.Rad2Deg;
        }
        public static FP SignedAngle(FPVector2 a, FPVector2 b)
        {
            // 2D下直接通过叉乘的正负即可判断左右（逆时针为正，顺时针为负）
            FP cross = Cross(a, b);
            FP y = cross >= FP.Zero ? cross : -cross;
            FP x = Dot(a, b);
            FP angle = FP.Atan2(y, x) * FP.Rad2Deg;

            return cross >= FP.Zero ? angle : -angle;
        }
        #endregion

        #region 数学库-扩展
        //距离与插值
        public static FP Distance(FPVector2 a, FPVector2 b)
        {
            return Magnitude(a - b);
        }
        public static FPVector2 Lerp(FPVector2 a, FPVector2 b, FP t)
        {
            t = FP.Clamp(t, FP.Zero, FP.One);
            return new FPVector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        //反射与投影
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Reflect(FPVector2 dir, FPVector2 nor)
        {
            //v(out)=v(in)−2(v(in) * n)n
            //其中n为归一化的法向量n
            FP dot = Dot(dir, nor);
            return dir - (dot + dot) * nor;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Project(FPVector2 dir, FPVector2 nor)
        {
            //dir在nor方向上的投影
            //如果对nor做归一化则公式化简为 Dot(dir, n) * n
            FPVector2 n = Normalize(nor);
            if (n == Zero)
                return Zero;
            return Dot(dir, n) * n;
        }

        //运动相关
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Min(FPVector2 a, FPVector2 b)
        {
            return new FPVector2(FP.Min(a.x, b.x), FP.Min(a.y, b.y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Max(FPVector2 a, FPVector2 b)
        {
            return new FPVector2(FP.Max(a.x, b.x), FP.Max(a.y, b.y));
        }
        public static FPVector2 Clamp(FPVector2 dir, FP max)
        {
            if (max <= FP.Zero)
                return Zero;

            //进行比较
            FP mag = Magnitude(dir);
            if (mag > max)
            {
                FP scale = max / mag;
                return dir * scale;
            }
            return dir;
        }
        public static FPVector2 Toward(FPVector2 cur, FPVector2 target, FP v)
        {
            FPVector2 to = target - cur;

            FP mag = Magnitude(to);
            //如果已经到达或者距离小于步长 直接返回目标点
            if (mag <= FP.Zero || (v >= FP.Zero && mag <= v))
            {
                return target;
            }
            return cur + Normalize(to) * v;
        }
        #endregion

        private static void Square128(ulong value, out ulong res_h, out ulong res_l)
        {
            ulong v_h = value >> 32;
            ulong v_l = value & 0xFFFFFFFFUL;

            ulong hh = v_h * v_h;
            ulong mm = v_h * v_l;
            ulong ll = v_l * v_l;

            //提取出中间值的整数与小数部分
            ulong m_h = (mm >> 32);
            ulong m_l = (mm & 0xFFFFFFFFUL) << 32;

            //加第一个mm
            //c为进位信息
            ulong c;
            ulong l = ll + m_l;
            c = (l < ll) ? 1UL : 0UL;
            ulong h = hh + m_h + c;

            //加第二个mm
            res_l = l + m_l;
            c = (res_l < l) ? 1UL : 0UL;
            res_h = h + m_h + c;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + x.GetHashCode();
                hash = hash * 23 + y.GetHashCode();
                return hash;
            }
        }
        public override string ToString()
        {
            return $"({x}, {y})";
        }
        public override bool Equals(object obj)
        {
            return obj is FPVector2 other && this == other;
        }

        public bool Equals(FPVector2 other)
        {
            return this == other;
        }
    }
}