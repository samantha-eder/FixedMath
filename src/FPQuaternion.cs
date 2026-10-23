using System;
using System.Runtime.CompilerServices;

namespace FixedMath
{
    public readonly struct FPQuaternion : IEquatable<FPQuaternion>
    {
        public readonly FP x;
        public readonly FP y;
        public readonly FP z;
        public readonly FP w;

        public FPQuaternion(FP x, FP y, FP z, FP w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static readonly FPQuaternion Identity = new FPQuaternion(FP.Zero, FP.Zero, FP.Zero, FP.One);

        #region 运算符
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPQuaternion operator *(FPQuaternion a, FPQuaternion b)
        {
            //四元数乘法 (Hamilton Product)
            return new FPQuaternion
            (
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FPQuaternion q, FPVector3 v)
        {
            //q*v*q^(-1)
            //四元数旋转向量公式: V' = V + 2w(Q.xyz x V) + 2(Q.xyz x (Q.xyz x V))
            FPVector3 xyz = new FPVector3(q.x, q.y, q.z);
            FPVector3 c = FPVector3.Cross(xyz, v);
            FPVector3 t = c + c;
            return v + (t * q.w) + FPVector3.Cross(xyz, t);
        }

        public static bool operator ==(FPQuaternion a, FPQuaternion b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }
        public static bool operator !=(FPQuaternion a, FPQuaternion b)
        {
            return a.x != b.x || a.y != b.y || a.z != b.z || a.w != b.w;
        }
        #endregion

        #region 数学库-基础
        public static FP Dot(FPQuaternion a, FPQuaternion b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        }
        public static FPQuaternion Normalize(FPQuaternion q)
        {
            FP mag = FP.Sqrt(Dot(q, q));
            if (mag > FP.Zero)
            {
                return new FPQuaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            }
            return Identity;
        }
        //四元数的逆
        //对于单位四元数 其逆严格等于共轭
        public static FPQuaternion Inverse(FPQuaternion q)
        {
            return new FPQuaternion(-q.x, -q.y, -q.z, q.w);
        }
        #endregion

        #region 数学库-旋转
        //绕轴旋转
        public static FPQuaternion AngleAxis(FP angle, FPVector3 axis)
        {
            //弧度 = 角度 * Deg2Rad
            //半角 = 弧度 * 0.5
            axis = FPVector3.Normalize(axis);
            if (axis == FPVector3.Zero) return Identity;

            angle = angle * FP.Deg2Rad * FP.Half;

            FP.SinCos(angle, out FP s, out FP c);
            return new FPQuaternion(axis.x * s, axis.y * s, axis.z * s, c);
        }
        //欧拉角转四元数 (Unity的Z-X-Y旋转顺序)
        public static FPQuaternion Euler(FP x, FP y, FP z)
        {
            //x = cy_sx cz + sy_cx sz 
            //y = sy_cx cz - cy_sx sz 
            //z = cy_cx sz - sy_sx cz 
            //w = cy_cx cz + sy_sx sz

            FP h = FP.Deg2Rad * FP.Half;
            FP.SinCos(x * h, out FP sx, out FP cx);
            FP.SinCos(y * h, out FP sy, out FP cy);
            FP.SinCos(z * h, out FP sz, out FP cz);

            FP cy_sx = cy * sx;
            FP sy_cx = sy * cx;
            FP cy_cx = cy * cx;
            FP sy_sx = sy * sx;

            return new FPQuaternion
            (
                cy_sx * cz + sy_cx * sz,
                sy_cx * cz - cy_sx * sz,
                cy_cx * sz - sy_sx * cz,
                cy_cx * cz + sy_sx * sz
            );
        }
        public static FPQuaternion Euler(FPVector3 euler)
        {
            return Euler(euler.x, euler.y, euler.z);
        }
        #endregion

        #region 数学库-插值
        public static FPQuaternion Lerp(FPQuaternion a, FPQuaternion b, FP t)
        {
            t = FP.Clamp(t, FP.Zero, FP.One);

            if (Dot(a, b) < FP.Zero)
            {
                b = new FPQuaternion(-b.x, -b.y, -b.z, -b.w);
            }

            return Normalize(new FPQuaternion
            (
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t,
                a.w + (b.w - a.w) * t
            ));
        }
        public static FPQuaternion Slerp(FPQuaternion a, FPQuaternion b, FP t)
        {
            // Q(t) = [sin((1-t) * θ) / sin(θ)]*a + [sin(t*θ) / sin(θ)]*b

            t = FP.Clamp(t, FP.Zero, FP.One);
            FP cos = Dot(a, b);

            // 走最短路径
            if (cos < FP.Zero)
            {
                b = new FPQuaternion(-b.x, -b.y, -b.z, -b.w);
                cos = -cos;
            }

            // 如果两个四元数非常接近 降级为 Lerp 以避免除以 0
            if (cos >= FP.One - FP.FromValue(429496L)) // 近似 0.9999
            {
                return Normalize(new FPQuaternion
                (
                    a.x + (b.x - a.x) * t,
                    a.y + (b.y - a.y) * t,
                    a.z + (b.z - a.z) * t,
                    a.w + (b.w - a.w) * t
                ));
            }

            FP sin = FP.Sqrt(FP.One - cos * cos);
            FP theta = FP.Atan2(sin, cos);


            FP ratioA = FP.Sin((FP.One - t) * theta) / sin;
            FP ratioB = FP.Sin(t * theta) / sin;

            return new FPQuaternion(
                a.x * ratioA + b.x * ratioB,
                a.y * ratioA + b.y * ratioB,
                a.z * ratioA + b.z * ratioB,
                a.w * ratioA + b.w * ratioB
            );
        }
        //返回两个四元数之间的夹角度数
        public static FP Angle(FPQuaternion a, FPQuaternion b)
        {
            FP dot = Dot(a, b);
            dot = FP.Min(FP.Abs(dot), FP.One);

            // 夹角 = 2 * Acos(dot)
            FP h = FP.Acos(dot) * FP.Rad2Deg;
            return h + h;
        }
        #endregion

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + x.GetHashCode();
                hash = hash * 23 + y.GetHashCode();
                hash = hash * 23 + z.GetHashCode();
                hash = hash * 23 + w.GetHashCode();
                return hash;
            }
        }
        public override string ToString()
        {
            return $"({x}, {y}, {z}, {w})";
        }
        public override bool Equals(object obj)
        {
            return obj is FPQuaternion other && this == other;
        }

        public bool Equals(FPQuaternion other)
        {
            return this == other;
        }
    }
}