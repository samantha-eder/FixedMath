using System;
using System.Runtime.CompilerServices;

namespace FixedMath
{
    public readonly struct FP : IComparable<FP>, IEquatable<FP>
    {
        public readonly long Value;
        public const int SHIFT = 32;
        private const long INT_MASK = ~0xFFFFFFFFL;
        private const long DEC_MASK = 0xFFFFFFFFL;

        private FP(long Value)
        {
            this.Value = Value;
        }

        public static FP FromInt(int value)
        {
            return FromValue((long)value << 32);
        }
        [Obsolete("警告：运行时使用浮点数转换会破坏帧同步跨平台确定性 仅限编辑器或非同步逻辑使用")]
        public static FP FromFloat(float value)
        {
            return FromValue((long)(value * 4294967296d));
        }
        public static FP FromString(string str)
        {
            if (string.IsNullOrEmpty(str)) return Zero;
            ReadOnlySpan<char> span = str.AsSpan();
            bool neg = span[0] == '-';

            //切去负号
            if (neg) span = span.Slice(1);
            int dot = span.IndexOf('.');

            //解析整数部分并左移32位
            ulong v_int = 0;
            ulong v_dec = 0;
            if (dot == -1)
            {
                //如果没有小数
                ulong temp = ulong.Parse(span);
                if (temp > 0x80000000UL) ThrowOverflow("字符串转化为定点数溢出");
                v_int = temp << 32;
            }
            else
            {
                //先转化整数部分
                ulong temp = ulong.Parse(span.Slice(0, dot));
                if (temp > 0x80000000UL) ThrowOverflow("字符串转化为定点数溢出");
                v_int = temp << 32;

                //转化小数部分
                var decSpan = span.Slice(dot + 1);
                if (decSpan.Length > 0)
                {
                    //处理小数补零
                    Span<char> s = stackalloc char[9]; s.Fill('0');
                    decSpan.Slice(0, Math.Min(decSpan.Length, 9)).CopyTo(s);

                    //赋值给v_dec
                    v_dec = (ulong.Parse(s) << 32) / 1000000000L;
                }
            }
            //将整数与小数相加 并检查正负边界
            //正数最大为 0x7FFFFFFFFFFFFFFF
            //负数最大绝对值为 0x8000000000000000 (即 -2147483648.0)
            ulong result = v_int + v_dec;
            ulong max = neg ? 0x8000000000000000UL : 0x7FFFFFFFFFFFFFFFUL;
            if (result > max) ThrowOverflow("字符串转化为定点数溢出");

            return FromValue(neg ? -(long)result : (long)result);
        }
        public static FP FromValue(long value)
        {
            return new FP(value);
        }

        #region 常量定义
        public static readonly FP Zero = FromValue(0L);
        public static readonly FP One = FromValue(1L << 32);
        public static readonly FP NegOne = FromValue(-(1L << 32));
        public static readonly FP Half = FromValue(1L << 31);

        //Pi
        public static readonly FP Pi = FromValue(13493037704L);
        public static readonly FP Pi2 = FromValue(26986075409L);
        public static readonly FP PiHalf = FromValue(6746518852L);
        public static readonly FP PiInv = FromValue(1367130551L);

        //弧度转角度 180/Pi  角度转弧度 Pi/180
        public static readonly FP Rad2Deg = FromValue(246083499198L);
        public static readonly FP Deg2Rad = FromValue(74961320L);

        //自然常数E
        public static readonly FP E = FromValue(11674931555L);
        public static readonly FP Ln_2 = FromValue(2977044471L);
        public static readonly FP Log10_2 = FromValue(1292913986L);

        //精度极限
        public static readonly FP MinValue = FromValue(long.MinValue);
        public static readonly FP MaxValue = FromValue(long.MaxValue);
        public static readonly FP Epsilon = FromValue(1L);
        #endregion

        #region 加减乘除运算符
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator +(FP a, FP b)
        {
            return FromValue(a.Value + b.Value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP a, FP b)
        {
            return FromValue(a.Value - b.Value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP a)
        {
            return FromValue(-a.Value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator *(FP a, FP b)
        {
            unchecked
            {
                //判断乘法的正负
                long av = a.Value;
                long bv = b.Value;
                bool neg = (av ^ bv) < 0;

                //将64位拆分为高32位和低32位
                ulong A = (ulong)(av >= 0 ? av : -av);
                ulong B = (ulong)(bv >= 0 ? bv : -bv);

                //利用乘法分配律计算最终结果
                //(a_h*2^32 + a_l) * (b_h*2^32 + b_l) / 2^32
                ulong a_h = A >> 32;
                ulong a_l = A & DEC_MASK;
                ulong b_h = B >> 32;
                ulong b_l = B & DEC_MASK;

                ulong m1 = a_h * b_l;
                ulong m2 = a_l * b_h;

                ulong sum_l = ((a_l * b_l) >> 32) + (m1 & DEC_MASK) + (m2 & DEC_MASK);
                ulong sum_h = (a_h * b_h) + (m1 >> 32) + (m2 >> 32) + (sum_l >> 32);


                ulong max_h = neg ? 0x80000000UL : 0x7FFFFFFFUL;
                if (sum_h > max_h || (sum_h == 0x80000000UL && (sum_l & DEC_MASK) != 0)) ThrowOverflow("定点数乘法溢出");

                ulong r_all = (sum_h << 32) | (sum_l & DEC_MASK);

                long result = neg ? -(long)r_all : (long)r_all;
                return FromValue(result);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator /(FP a, FP b)
        {
            if (b.Value == 0) throw new DivideByZeroException("定点数除数为0");

            long av = a.Value;
            long bv = b.Value;

            //判断除法的正负 a/b
            bool neg = (av < 0) ^ (bv < 0);
            ulong A = (ulong)(av >= 0 ? av : -av); //被除数
            ulong B = (ulong)(bv >= 0 ? bv : -bv); //除数

            //计算 (A * 2^32) / B
            //将A拆开构造一个128位的被除数
            ulong a_h = A >> 32;
            ulong a_l = A << 32;
            ulong quotient = 0;

            //二进制长除法
            for (int i = 0; i < 64; i++)
            {
                //滑动窗口的最高位
                //用来判断接下来的左移是否会溢出ulong
                ulong bit = a_h >> 63;
                //整体左移1位
                a_h = (a_h << 1) | (a_l >> 63);
                a_l <<= 1;

                if (bit != 0 || a_h >= B)
                {
                    a_h -= B;
                    quotient = (quotient << 1) | 1;
                }
                else { quotient = quotient << 1; }
            }

            //判断结果是否溢出
            ulong max = neg ? 0x8000000000000000UL : 0x7FFFFFFFFFFFFFFFUL;
            if (quotient > max) ThrowOverflow("定点数除法溢出");
            long result = neg ? -(long)quotient : (long)quotient;

            return FromValue(result);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOverflow(string message)
        {
            throw new OverflowException(message);
        }
        #endregion

        #region 比较运算符
        public static bool operator ==(FP a, FP b) => a.Value == b.Value;
        public static bool operator !=(FP a, FP b) => a.Value != b.Value;
        public static bool operator <(FP a, FP b) => a.Value < b.Value;
        public static bool operator >(FP a, FP b) => a.Value > b.Value;
        public static bool operator <=(FP a, FP b) => a.Value <= b.Value;
        public static bool operator >=(FP a, FP b) => a.Value >= b.Value;
        #endregion

        #region 数学库-基础
        public static int Sign(FP a)
        {
            if (a.Value > 0) return 1;
            if (a.Value < 0) return -1;
            return 0;
        }
        public static FP Abs(FP a)
        {
            //右移63位：正数得0 负数得-1
            //无分支指令 比 Math.Abs 快且没有 MinValue 检查开销
            long mask = a.Value >> 63;

            //当a是正数 mask=0 (a + 0) ^ 0 = 0
            //当a是负数 mask=-1 (a + -1)^(-1) = ~(a-1) = -a
            return FromValue((a.Value + mask) ^ mask);
        }
        public static FP Ceil(FP a)
        {
            return FromValue((a.Value + DEC_MASK) & INT_MASK);
        }
        public static FP Floor(FP a)
        {
            return FromValue(a.Value & INT_MASK);
        }
        public static FP Round(FP a)
        {
            //右移63位：正数得0 负数得-1
            //正数加 0.5
            //负数加 0.49999999976 即0.5减去最小精度单位1 也就是 Half.Value - 1
            long offset = Half.Value + (a.Value >> 63);
            return FromValue((a.Value + offset) & INT_MASK);
        }
        public static FP Min(FP a, FP b)
        {
            return a.Value < b.Value ? a : b;
        }
        public static FP Max(FP a, FP b)
        {
            return a.Value > b.Value ? a : b;
        }
        public static FP Mod(FP a, FP b)
        {
            if (b.Value == 0)
            {
                throw new DivideByZeroException("Mod by zero");
            }
            long rem = a.Value % b.Value;
            if (rem != 0 && ((rem < 0) ^ (b.Value < 0)))
            {
                rem += b.Value;
            }
            return FromValue(rem);
        }
        public static FP Lerp(FP a, FP b, FP t)
        {
            t = Clamp(t, FP.Zero, FP.One);
            return a + (b - a) * t;
        }
        public static FP Clamp(FP value, FP min, FP max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        #endregion

        #region 数学库-扩展
        public static FP Sqrt(FP a)
        {
            if (a.Value <= 0) return Zero;

            //逐位逼近法计算a的平方根
            ulong num = (ulong)a.Value;
            ulong rem = 0;
            ulong root = 0;

            //每次从num的最高位取两个bit移入余数rem中
            //从最高组一直处理到最低组共32次
            for (int i = 0; i < 32; i++)
            {
                //将当前值左移一位准备确定下一位的值
                root <<= 1;

                //将旧余数放大4倍 (左移2位) 并拼上新拉下来的2位数
                //得到新的需要计算的残差rem
                rem = (rem << 2) | (num >> 62);

                //将剩下的数往前推进两位
                //相当于开始测试下一组的两个bit位
                num <<= 2;

                //由于开始时已经将root<<1
                //此时4Q+1 只需要让root<<即可
                ulong test = (root << 1) | 1;

                //如果够减 直接把末尾那个刚才补的0变成1
                //相当于将当前位置为1
                if (rem >= test)
                {
                    rem -= test;
                    root |= 1UL;
                }
            }
            //补全Q32.32的最后16位小数精度
            for (int i = 0; i < 16; i++)
            {
                root <<= 1;
                //在残差后面补两个0 
                //相当于在原数num后补0 继续计算精度
                rem <<= 2;

                ulong test = (root << 1) | 1;
                if (rem >= test)
                {
                    rem -= test;
                    root |= 1;
                }
            }
            return FromValue((long)root);
        }

        //对数
        public static FP Log2(FP a)
        {
            if (a.Value <= 0)
            {
                throw new ArgumentOutOfRangeException("对数的参数小于等于0");
            }

            ulong num = (ulong)a.Value;

            //寻找最高位的1
            int msb = 0;
            ulong temp = num;
            if (temp >= (1UL << 32)) { temp >>= 32; msb += 32; }
            if (temp >= (1UL << 16)) { temp >>= 16; msb += 16; }
            if (temp >= (1UL << 8)) { temp >>= 8; msb += 8; }
            if (temp >= (1UL << 4)) { temp >>= 4; msb += 4; }
            if (temp >= (1UL << 2)) { temp >>= 2; msb += 2; }
            if (temp >= (1UL << 1)) { msb += 1; }

            //num = x * 2^y
            //log(num) = y + log(x)  x∈[1,2)
            //整数部分的值就是最高位1的位数-32 代表
            int res_int = msb - 32;

            //将小数部分x的值归一化到[1.0, 2.0)
            //第 62 位代表 1.0 第 63 位代表 2.0 保留全部小数精度
            ulong x = num << (62 - msb);
            ulong two = 1UL << 63;

            //逼近小数
            //log(x) = 0.b1b2b3b4... = b1*2^(-1) + b2*2^(-2) + b3*2^(-3)...
            //2log(x) = log(x^2) = b1+0.b2b3b4... 
            //若x^2 > 2 即 b1 = 1 那么log(x^2)-1 = 0.b2b3b4... 
            //log(x^2/2) = 0.b2b3b4... 令新的x = x^2/2 重复判断即可
            long res_dec = 0;
            for (int i = 31; i >= 0; i--)
            {
                //64位ulong相乘
                // x = h * 2^32 + l
                // x^2 >> 62 = (h * 2^32 + l)^2 >> 62
                //           = ((h * h) << 2) + ((h * l) >> 29) + ((l * l) >> 62)
                ulong h = x >> 32;
                ulong l = x & DEC_MASK;

                x = ((h * h) << 2) + (((h * l) + ((l * l) >> 33)) >> 29);
                if (x >= two)
                {
                    res_dec |= (1L << i);
                    x >>= 1;
                }
            }

            long result = ((long)res_int << 32) | res_dec;
            return FromValue(result);
        }
        public static FP Ln(FP a)
        {
            return Log2(a) * Ln_2;
        }
        public static FP Log10(FP a)
        {
            return Log2(a) * Log10_2;
        }

        //指数
        //Q2.62 格式查找表
        //Exp2Table[i] 代表 2^(2^-(i+1)) * 2^62 保留62位小数精度
        //其中 1.0 = 1UL << 62 = 4611686018427387904UL
        private static readonly ulong[] Exp2Table = new ulong[]
        {
            6521908912666391106UL, 5484249825272419512UL, 5029079263719320435UL, 4815862801830788490UL,
            4712668792719003884UL, 4661903986662671290UL, 4636727017470743990UL, 4624189567668517720UL,
            4617933561212708776UL, 4614808732577250068UL, 4613247111281068008UL, 4612466498810092975UL,
            4612076242109103707UL, 4611881126141011236UL, 4611783571252412754UL, 4611734794581956353UL,
            4611710406440186476UL, 4611698212417665819UL, 4611692115418496524UL, 4611689066921934630UL,
            4611687542674409371UL, 4611686780550835664UL, 4611686399489096040UL, 4611686208958238036UL,
            4611686113692811986UL, 4611686066060099699UL, 4611686042243743740UL, 4611686030335565807UL,
            4611686024381476851UL, 4611686021404432377UL, 4611686019915910140UL, 4611686019171649022UL
        };
        public static FP Exp2(FP a)
        {
            long num = a.Value;
            //如果 x >= 31.0  2^x 将超出Q32.32的表示范围
            //如果 x < -32.0      结果在定点数精度下为0
            if (num >= 133143986176L) ThrowOverflow("指数过大 计算结果溢出 Q32.32 范围");
            if (num < -137438953472L) return Zero;

            //分离整数和小数部分
            int res_int = (int)(num >> 32);
            ulong dec = (ulong)(num & DEC_MASK);

            //计算 2^dec 初始值为1
            ulong res_dec = 1UL << 62;
            for (int i = 31; i >= 0; i--)
            {
                //如果当前dec的二进制小数位为0则跳过 
                if ((dec & (1UL << i)) == 0)
                    continue;

                ulong b = Exp2Table[31 - i];

                //64x64拓展乘法
                ulong a_h = res_dec >> 32;
                ulong a_l = res_dec & DEC_MASK;
                ulong b_h = b >> 32;
                ulong b_l = b & DEC_MASK;

                ulong mid = (a_h * b_l) + (a_l * b_h) + ((a_l * b_l) >> 32);
                res_dec = ((a_h * b_h) << 2) + (mid >> 30);
            }

            //此时的res_dec 是Q2.64的超高精度小数
            //需要转化为Q32.32 并且乘上2^res_int
            //并且right的范围[0,62]
            int right = 30 - res_int;
            return FromValue((long)(res_dec >> right));
        }
        public static FP Pow(FP a, FP b)
        {
            //如果b是整数直接调用快速幂
            if ((b.Value & DEC_MASK) == 0) return Pow(a, (int)(b.Value >> 32));

            //此时b是小数
            if (a.Value < 0) throw new ArithmeticException("负数的小数次方在实数域无解");
            if (a.Value == 0)
            {
                if (b.Value < 0) throw new DivideByZeroException("0的负数次方 错误");
                return FP.Zero;
            }

            //此时底数a>0
            //1的任何次方都是1
            if (a.Value == FP.One.Value) return FP.One;

            //a ^ b = 2 ^ (b * log2(a))
            return Exp2(b * Log2(a));
        }
        public static FP Pow(FP a, int b)
        {
            if (b == 0) return FP.One;
            if (b == 1) return a;
            if (a.Value == FP.One.Value) return FP.One;
            if (a.Value == 0)
            {
                if (b < 0) throw new DivideByZeroException("0的负数次方 错误");
                return FP.Zero;
            }

            //提取绝对值
            //先转long再取负 防止 b == int.MinValue 时直接 -b 溢出
            ulong b_abs = (ulong)(b < 0 ? -(long)b : b);

            FP result = FP.One;
            FP cur = a;
            //快速幂
            while (b_abs > 0)
            {
                if ((b_abs & 1) != 0)
                {
                    result *= cur;
                }
                cur *= cur;
                b_abs >>= 1;
            }

            return b < 0 ? FP.One / result : result;
        }
        #endregion

        #region 数学库-三角函数
        //根据比例法 规定180度为1
        private const long PI_C_Q62 = 1L << 62;
        private const long CORDIC_K_Q62 = 2800459870029452954L;
        private const ulong PI_INV_Q62 = 1467945251641000613;
        //Q2.62高精度Atan表
        private static readonly long[] AtanTable =
        {
            1152921504606846976L, 680609306067436595L,359615265290440518L,182546323762760973L,
            91627395746647414L,   45858365146018108L, 22934778241356564L, 11468088963375447L,
            5734131974037915L,    2867076923938203L,  1433539829095741L,  716770085439067L,
            358385064080944L,     179192534710649L,   89596267689096L,    44798133886269L,
            22399066948350L,      11199533474826L,    5599766737494L,     2799883368757L,
            1399941684380L,       699970842190L,      349985421095L,      174992710547L,
            87496355274L,         43748177637L,       21874088818L,       10937044409L,
            5468522205L,          2734261102L,        1367130551L,        683565275L,
            341782638L
        };
        private static void SinCosInternal(long rad, out FP sin, out FP cos)
        {
            //利用高精度(1/Pi)计算scale
            //将弧度 R(Q32.32) 乘以 1 / Pi(Q2.62) 并右移 32 位 直接得到 Q2.62 格式的 scale
            ulong p_h = PI_INV_Q62 >> 32;
            ulong p_l = PI_INV_Q62 & DEC_MASK;
            ulong r_h = (ulong)(rad >> 32);
            ulong r_l = (ulong)(rad & DEC_MASK);

            //计算 Q2.62 格式的 scale
            //并自动映射到[-2 - 2)之间
            long scale = (long)(((r_h * p_h) << 32) + (r_h * p_l) + (r_l * p_h) + ((r_l * p_l) >> 32));

            //提取出整数部分k 和 小数部分target
            long k = (scale + (1L << 61)) >> 62;
            long target = scale - (k << 62);

            //根据k的奇偶性确定最终的正负
            long x = (k & 1) == 0 ? CORDIC_K_Q62 : -CORDIC_K_Q62;
            long y = 0;

            //循环计算SinCos
            for (int i = 0; i < 33; i++)
            {
                long mask = target >> 63;
                long dx = x >> i;
                long dy = y >> i;

                //x = x - dir * dy; y = y + dir * dx; target -= dir * AtanTable[i];
                //这里用位掩码操作代替 速度更快
                x -= (dy + mask) ^ mask;
                y += (dx + mask) ^ mask;
                target -= (AtanTable[i] + mask) ^ mask;
            }

            //将x y四舍五入回Q32.32定点数
            x = (x + (1L << 29)) >> 30;
            y = (y + (1L << 29)) >> 30;
            cos = FromValue(x);
            sin = FromValue(y);
        }
        private static FP Atan2Internal(long y, long x)
        {
            if (y == 0 && x == 0) return FP.Zero;

            //将 x 和 u 按位或合并 找出两者中较大数的最高位 1 (msb)
            ulong ux = (ulong)(x >= 0 ? x : -x);
            ulong uy = (ulong)(y >= 0 ? y : -y);

            //寻找最高位的msb
            int msb = 0;
            ulong max = ux | uy;
            if (max >= (1UL << 32)) { max >>= 32; msb += 32; }
            if (max >= (1UL << 16)) { max >>= 16; msb += 16; }
            if (max >= (1UL << 8)) { max >>= 8; msb += 8; }
            if (max >= (1UL << 4)) { max >>= 4; msb += 4; }
            if (max >= (1UL << 2)) { max >>= 2; msb += 2; }
            if (max >= (1UL << 1)) { msb += 1; }

            //等比例移位到第60位
            //既留出了顶部3个bit防止CORDIC膨胀溢出
            //又保证了底部60位满精度
            if (msb > 60)
            {
                int shift = msb - 60;
                ux >>= shift;
                uy >>= shift;
            }
            else
            {
                int shift = 60 - msb;
                ux <<= shift;
                uy <<= shift;
            }

            //变换到一三象限并记录偏移值
            long X = (long)ux;
            long Y = y >= 0 ? (long)uy : -(long)uy;
            long offset = 0;
            if (x < 0)
            {
                //若原坐标在二三象限(x < 0)
                //则关于原点中心对称翻转到一四象限
                //X已经取了绝对值 所以只需再把 Y 取反即可
                Y = -Y;
                offset = (y >= 0) ? PI_C_Q62 : -PI_C_Q62;
            }

            //Q2.62下进行 33 轮无分支 CORDIC 逼近
            long angle = 0;
            for (int i = 0; i < 33; i++)
            {
                //将Y逼近0
                //当 Y < 0 时 mask 为 -1 (逆时针转(正方向) angle 减去表值)
                //当 Y >= 0 时 mask 为 0 (顺时针转(负方向) angle 加上表值)
                long mask = Y >> 63;
                long dx = X >> i;
                long dy = Y >> i;

                X += (dy + mask) ^ mask;
                Y -= (dx + mask) ^ mask;
                angle += (AtanTable[i] + mask) ^ mask;
            }

            //象限补偿 并返回弧度
            long total = angle + offset;
            return FromValue((total + (1L << 29)) >> 30) * Pi;
        }

        //三级函数
        public static FP Sin(FP radians)
        {
            SinCosInternal(radians.Value, out FP s, out _);
            return s;
        }
        public static FP Cos(FP radians)
        {
            SinCosInternal(radians.Value, out _, out FP c);
            return c;
        }
        public static FP Tan(FP radians)
        {
            SinCosInternal(radians.Value, out FP s, out FP c);
            if (c.Value == 0) throw new DivideByZeroException("Tan 趋近无穷大");
            return s / c;
        }
        public static void SinCos(FP radians, out FP sin, out FP cos)
        {
            SinCosInternal(radians.Value, out sin, out cos);
        }

        //反三角函数
        public static FP Asin(FP x)
        {
            x = Clamp(x, FP.NegOne, FP.One);
            FP y = Sqrt(FP.One - x * x);
            return Atan2Internal(x.Value, y.Value);
        }
        public static FP Acos(FP x)
        {
            x = Clamp(x, FP.NegOne, FP.One);
            FP y = Sqrt(FP.One - x * x);
            return Atan2Internal(y.Value, x.Value);
        }
        public static FP Atan(FP x)
        {
            return Atan2Internal(x.Value, FP.One.Value);
        }
        public static FP Atan2(FP y, FP x)
        {
            return Atan2Internal(y.Value, x.Value);
        }
        #endregion

        #region 转换
        public int ToInt()
        {
            return (int)((this.Value + ((this.Value >> 63) & DEC_MASK)) >> 32);
        }
        public float ToFloat()
        {
            return this.Value * (1.0f / 4294967296f);
        }
        public double ToDouble()
        {
            return this.Value * (1.0 / 4294967296.0);
        }
        public override string ToString()
        {
            return ToDouble().ToString("F7", System.Globalization.CultureInfo.InvariantCulture);
        }
        #endregion

        public int CompareTo(FP other)
        {
            return Value.CompareTo(other.Value);
        }
        public bool Equals(FP other)
        {
            return Value == other.Value;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public override bool Equals(object other)
        {
            return (other is FP fp) && (Value == fp.Value);
        }
    }
}