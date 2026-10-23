using System;
using System.Diagnostics;
using System.Numerics;

namespace FixedMath.Benchmark
{
    class Program
    {
        private const int PerfTestCount = 1_000_000;
        private const int AccTestCount = 100_000;

        static void Main(string[] args)
        {
            Console.WriteLine("FixedMath(Q32.32)测试开始");
            Warmup();

            FPAccuracyTests();
            Console.WriteLine();

            FPPerformanceTests();
            Console.WriteLine();

            VecQuaAccuracyTests();
            Console.WriteLine();

            VecQuaPerformanceTests();
            Console.WriteLine();

            Console.WriteLine("测试完成");
            Console.ReadLine();
        }

        #region 测试方法
        private static FP DoubleToFP(double v) => FP.FromValue((long)(v * 4294967296d));
        private static FPVector3 DoubleToFPV3(Vector3d v) => new FPVector3(DoubleToFP(v.x), DoubleToFP(v.y), DoubleToFP(v.z));
        private static FPQuaternion DoubleToFPQ(Quaterniond q) => new FPQuaternion(DoubleToFP(q.x), DoubleToFP(q.y), DoubleToFP(q.z), DoubleToFP(q.w));

        private static void Warmup()
        {
            FP a = DoubleToFP(1.5), b = DoubleToFP(2.5);
            _ = a * b; _ = FP.Sin(a); _ = FP.Sqrt(b);
            _ = FPVector3.Cross(FPVector3.One, FPVector3.Up);
            _ = FPVector3.Magnitude(FPVector3.One);
            _ = FPQuaternion.Identity * FPVector3.One;
        }

        private static string PadRight(string text, int width)
        {
            int cur = 0;
            foreach (char c in text) cur += (c > 0xFF) ? 2 : 1;
            int padding = width - cur;
            return padding > 0 ? text + new string(' ', padding) : text;
        }
        private static double Next(Random r, double min, double max)
        {
            return min + r.NextDouble() * (max - min);
        }
        private static void LogResult(string name, string range, double max, double avg)
        {
            Console.WriteLine($"| {PadRight(name, 24)} | {PadRight(range, 22)} | {max,20:E4} | {avg,20:E4} |");
        }

        #endregion

        #region 基础数学库测试
        static void FPAccuracyTests()
        {
            Console.WriteLine($"FP精度测试 (采样次数：{AccTestCount})");
            Console.WriteLine($"| {PadRight("运算项目", 24)} | {PadRight("测试范围", 22)} | {"最大误差",20} | {"平均误差",20} |");
            Console.WriteLine("|:-------------------------|:-----------------------|--------------------:|--------------------:|");

            var rnd = new Random(42);
            FPTest1("Sqrt", rnd, 0.001, 1000, Math.Sqrt, a => FP.Sqrt(a).ToDouble());
            FPTest1("Sin", rnd, -Math.PI, Math.PI, Math.Sin, a => FP.Sin(a).ToDouble());
            FPTest1("Cos", rnd, -Math.PI, Math.PI, Math.Cos, a => FP.Cos(a).ToDouble());
            FPTest1("Tan", rnd, -Math.PI / 2 + 0.1, Math.PI / 2 - 0.1, Math.Tan, a => FP.Tan(a).ToDouble());
            FPTest1("Asin", rnd, -1.0, 1.0, Math.Asin, a => FP.Asin(a).ToDouble());
            FPTest1("Acos", rnd, -1.0, 1.0, Math.Acos, a => FP.Acos(a).ToDouble());
            FPTest2("Atan2", rnd, -1.0, 1.0, -1.0, 1.0, Math.Atan2, (y, x) => FP.Atan2(y, x).ToDouble());
            FPTest1("Log2", rnd, 0.001, 1000, Math.Log2, a => FP.Log2(a).ToDouble());
            FPTest1("Ln", rnd, 0.001, 1000, Math.Log, a => FP.Ln(a).ToDouble());
            FPTest1("Log10", rnd, 0.001, 1000, Math.Log10, a => FP.Log10(a).ToDouble());
            FPTest1("Exp2", rnd, -10.0, 10.0, a => Math.Pow(2, a), a => FP.Exp2(a).ToDouble());
            FPTest2("Pow", rnd, 0.1, 10.0, -4.0, 4.0, Math.Pow, (a, b) => FP.Pow(a, b).ToDouble());
            FPTest2("Mul (*)", rnd, -1000.0, 1000.0, -1000.0, 1000.0, (a, b) => a * b, (a, b) => (a * b).ToDouble());
            FPTest2("Div (/)", rnd, -1000.0, 1000.0, 0.1, 1000.0, (a, b) => a / b, (a, b) => (a / b).ToDouble());
        }

        static void FPPerformanceTests()
        {
            Console.WriteLine($"FP性能测试 (运算次数: {PerfTestCount})");
            Console.WriteLine($"| {PadRight("运算项目", 24)} | {"`float`耗时",15} | {"`double`耗时",15} | {"`FP`耗时",18} | {"耗时比 (FP/f)",16} |");
            Console.WriteLine("|:-------------------------|----------------:|----------------:|-------------------:|-----------------:|");

            var rnd = new Random(42);
            float[] fA = new float[PerfTestCount], fB = new float[PerfTestCount];
            double[] dA = new double[PerfTestCount], dB = new double[PerfTestCount];
            FP[] fpA = new FP[PerfTestCount], fpB = new FP[PerfTestCount];

            for (int i = 0; i < PerfTestCount; i++)
            {
                double vA = 0.1 + rnd.NextDouble() * 10.0; double vB = 0.1 + rnd.NextDouble() * 10.0;
                fA[i] = (float)vA; dA[i] = vA; fpA[i] = DoubleToFP(vA);
                fB[i] = (float)vB; dB[i] = vB; fpB[i] = DoubleToFP(vB);
            }

            FPMeasure("Add (+)",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += fA[i] + fB[i]; return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += dA[i] + dB[i]; return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += fpA[i] + fpB[i]; return s.ToDouble(); });

            FPMeasure("Mul (*)",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += fA[i] * fB[i]; return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += dA[i] * dB[i]; return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += fpA[i] * fpB[i]; return s.ToDouble(); });

            FPMeasure("Div (/)",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += fA[i] / fB[i]; return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += dA[i] / dB[i]; return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += fpA[i] / fpB[i]; return s.ToDouble(); });

            FPMeasure("Sqrt",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += MathF.Sqrt(fA[i]); return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += Math.Sqrt(dA[i]); return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += FP.Sqrt(fpA[i]); return s.ToDouble(); });

            FPMeasure("Sin",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += MathF.Sin(fA[i]); return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += Math.Sin(dA[i]); return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += FP.Sin(fpA[i]); return s.ToDouble(); });

            FPMeasure("Atan2",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += MathF.Atan2(fA[i], fB[i]); return s; },
                () => { double s = 0; for (int i = 0; i < PerfTestCount; i++) s += Math.Atan2(dA[i], dB[i]); return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += FP.Atan2(fpA[i], fpB[i]); return s.ToDouble(); });
        }

        static void FPTest1(string name, Random rnd, double min, double max, Func<double, double> opD, Func<FP, double> opFP)
        {
            double maxErr = 0, sumErr = 0;
            for (int i = 0; i < AccTestCount; i++) { double v = Next(rnd, min, max); double e = Math.Abs(opD(v) - opFP(DoubleToFP(v))); if (e > maxErr) maxErr = e; sumErr += e; }
            LogResult(name, $"[{min:0.##}, {max:0.##}]", maxErr, sumErr / AccTestCount);
        }
        static void FPTest2(string name, Random rnd, double m1, double x1, double m2, double x2, Func<double, double, double> opD, Func<FP, FP, double> opFP)
        {
            double maxErr = 0, sumErr = 0;
            for (int i = 0; i < AccTestCount; i++) { double v1 = Next(rnd, m1, x1), v2 = Next(rnd, m2, x2); double e = Math.Abs(opD(v1, v2) - opFP(DoubleToFP(v1), DoubleToFP(v2))); if (e > maxErr) maxErr = e; sumErr += e; }
            LogResult(name, $"[{m1:0.##}, {x1:0.##}], [...]", maxErr, sumErr / AccTestCount);
        }
        static void FPMeasure(string name, Func<float> aF, Func<double> aD, Func<double> aFP)
        {
            var sw = new Stopwatch();
            sw.Start(); float rF = aF(); sw.Stop(); double msF = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); double rD = aD(); sw.Stop(); double msD = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); double rFP = aFP(); sw.Stop(); double msFP = sw.Elapsed.TotalMilliseconds;
            GC.KeepAlive(rF); GC.KeepAlive(rD); GC.KeepAlive(rFP);
            Console.WriteLine($"| {PadRight(name, 24)} | {msF,15:F2} ms | {msD,15:F2} ms | **{msFP,12:F2} ms** | {msFP / msF,16:F1}x |");
        }
        #endregion

        #region 向量与四元数测试
        static void VecQuaAccuracyTests()
        {
            Console.WriteLine($"向量与四元数精度测试 (采样次数：{AccTestCount})");
            Console.WriteLine($"| {PadRight("运算项目", 24)} | {PadRight("测试范围", 22)} | {"最大误差",20} | {"平均误差",20} |");
            Console.WriteLine("|:-------------------------|:-----------------------|--------------------:|--------------------:|");

            var rnd = new Random(42);
            Vec("V3.Magnitude", rnd, -100, 100, v => v.Magnitude(), v => FPVector3.Magnitude(v).ToDouble());
            Vec2("V3.Cross", rnd, -100, 100, (a, b) => Vector3d.Cross(a, b), (a, b) => FPVector3.Cross(a, b));
            Vec("V3.Normalize", rnd, -1000, 1000, v => v.Normalize(), v => FPVector3.Normalize(v));
            Euler("Quat.Euler(ZXY)", rnd, -360, 360, (x, y, z) => Quaterniond.Euler(x, y, z), (x, y, z) => FPQuaternion.Euler(DoubleToFP(x), DoubleToFP(y), DoubleToFP(z)));
            QuatV3("Quat * Vector3", rnd, (q, v) => q * v, (q, v) => q * v);
            QuatSlerp("Quat.Slerp", rnd, (q1, q2, t) => Quaterniond.Slerp(q1, q2, t), (q1, q2, t) => FPQuaternion.Slerp(q1, q2, DoubleToFP(t)));
        }
        static void VecQuaPerformanceTests()
        {
            Console.WriteLine($"向量与四元数性能测试 (运算次数: {PerfTestCount})");
            Console.WriteLine($"| {PadRight("运算项目", 24)} | {"SIMD float 耗时",18} | {"`FP定点数` 耗时",16} | {"耗时比 (FP/float)",18} |");
            Console.WriteLine("|:-------------------------|-------------------:|-----------------:|-------------------:|");

            var rnd = new Random(42);
            Vector3[] svA = new Vector3[PerfTestCount], svB = new Vector3[PerfTestCount];
            Quaternion[] sqA = new Quaternion[PerfTestCount], sqB = new Quaternion[PerfTestCount];
            FPVector3[] fvA = new FPVector3[PerfTestCount], fvB = new FPVector3[PerfTestCount];
            FPQuaternion[] fqA = new FPQuaternion[PerfTestCount], fqB = new FPQuaternion[PerfTestCount];

            for (int i = 0; i < PerfTestCount; i++)
            {
                float x1 = (float)Next(rnd, -100, 100), y1 = (float)Next(rnd, -100, 100), z1 = (float)Next(rnd, -100, 100);
                float x2 = (float)Next(rnd, -100, 100), y2 = (float)Next(rnd, -100, 100), z2 = (float)Next(rnd, -100, 100);
                svA[i] = new Vector3(x1, y1, z1); fvA[i] = new FPVector3(DoubleToFP(x1), DoubleToFP(y1), DoubleToFP(z1));
                svB[i] = new Vector3(x2, y2, z2); fvB[i] = new FPVector3(DoubleToFP(x2), DoubleToFP(y2), DoubleToFP(z2));
                sqA[i] = Quaternion.CreateFromYawPitchRoll(x1, y1, z1); fqA[i] = FPQuaternion.Euler(DoubleToFP(x1), DoubleToFP(y1), DoubleToFP(z1));
                sqB[i] = Quaternion.CreateFromYawPitchRoll(x2, y2, z2); fqB[i] = FPQuaternion.Euler(DoubleToFP(x2), DoubleToFP(y2), DoubleToFP(z2));
            }

            VecQuaMeasure("V3.Cross",
                () => { Vector3 s = Vector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += Vector3.Cross(svA[i], svB[i]); return s.X; },
                () => { FPVector3 s = FPVector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += FPVector3.Cross(fvA[i], fvB[i]); return s.x.ToDouble(); });
            VecQuaMeasure("V3.Magnitude (Length)",
                () => { float s = 0; for (int i = 0; i < PerfTestCount; i++) s += svA[i].Length(); return s; },
                () => { FP s = FP.Zero; for (int i = 0; i < PerfTestCount; i++) s += FPVector3.Magnitude(fvA[i]); return s.ToDouble(); });
            VecQuaMeasure("V3.Normalize",
                () => { Vector3 s = Vector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += Vector3.Normalize(svA[i]); return s.X; },
                () => { FPVector3 s = FPVector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += FPVector3.Normalize(fvA[i]); return s.x.ToDouble(); });
            VecQuaMeasure("Quat * Vector3",
                () => { Vector3 s = Vector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += Vector3.Transform(svA[i], sqA[i]); return s.X; },
                () => { FPVector3 s = FPVector3.Zero; for (int i = 0; i < PerfTestCount; i++) s += fqA[i] * fvA[i]; return s.x.ToDouble(); });
            VecQuaMeasure("Quat.Slerp",
                () => { Quaternion s = Quaternion.Identity; for (int i = 0; i < PerfTestCount; i++) s *= Quaternion.Slerp(sqA[i], sqB[i], 0.5f); return s.X; },
                () => { FPQuaternion s = FPQuaternion.Identity; for (int i = 0; i < PerfTestCount; i++) s *= FPQuaternion.Slerp(fqA[i], fqB[i], FP.Half); return s.x.ToDouble(); });
        }

        static void Vec(string n, Random r, double m, double x, Func<Vector3d, double> oD, Func<FPVector3, double> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { var vd = new Vector3d(Next(r, m, x), Next(r, m, x), Next(r, m, x)); double e = Math.Abs(oD(vd) - oFP(DoubleToFPV3(vd))); if (e > mE) mE = e; sE += e; }
            LogResult(n, $"[{m}, {x}]", mE, sE / AccTestCount);
        }
        static void Vec(string n, Random r, double m, double x, Func<Vector3d, Vector3d> oD, Func<FPVector3, FPVector3> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { var vd = new Vector3d(Next(r, m, x), Next(r, m, x), Next(r, m, x)); var fp = oFP(DoubleToFPV3(vd)); double e = Vector3d.Distance(oD(vd), new Vector3d(fp.x.ToDouble(), fp.y.ToDouble(), fp.z.ToDouble())); if (e > mE) mE = e; sE += e; }
            LogResult(n, $"[{m}, {x}]", mE, sE / AccTestCount);
        }
        static void Vec2(string n, Random r, double m, double x, Func<Vector3d, Vector3d, Vector3d> oD, Func<FPVector3, FPVector3, FPVector3> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { var v1 = new Vector3d(Next(r, m, x), Next(r, m, x), Next(r, m, x)); var v2 = new Vector3d(Next(r, m, x), Next(r, m, x), Next(r, m, x)); var fp = oFP(DoubleToFPV3(v1), DoubleToFPV3(v2)); double e = Vector3d.Distance(oD(v1, v2), new Vector3d(fp.x.ToDouble(), fp.y.ToDouble(), fp.z.ToDouble())); if (e > mE) mE = e; sE += e; }
            LogResult(n, $"[{m}, {x}], [...]", mE, sE / AccTestCount);
        }
        static void Euler(string n, Random r, double m, double x, Func<double, double, double, Quaterniond> oD, Func<double, double, double, FPQuaternion> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { double v1 = Next(r, m, x), v2 = Next(r, m, x), v3 = Next(r, m, x); var fp = oFP(v1, v2, v3); double e = Quaterniond.Distance(oD(v1, v2, v3), new Quaterniond(fp.x.ToDouble(), fp.y.ToDouble(), fp.z.ToDouble(), fp.w.ToDouble())); if (e > mE) mE = e; sE += e; }
            LogResult(n, $"Euler [Z,X,Y]", mE, sE / AccTestCount);
        }
        static void QuatV3(string n, Random r, Func<Quaterniond, Vector3d, Vector3d> oD, Func<FPQuaternion, FPVector3, FPVector3> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { var q = Quaterniond.Euler(Next(r, -180, 180), Next(r, -180, 180), Next(r, -180, 180)); var v = new Vector3d(Next(r, -100, 100), Next(r, -100, 100), Next(r, -100, 100)); var fp = oFP(DoubleToFPQ(q), DoubleToFPV3(v)); double e = Vector3d.Distance(oD(q, v), new Vector3d(fp.x.ToDouble(), fp.y.ToDouble(), fp.z.ToDouble())); if (e > mE) mE = e; sE += e; }
            LogResult(n, "Random Rots", mE, sE / AccTestCount);
        }
        static void QuatSlerp(string n, Random r, Func<Quaterniond, Quaterniond, double, Quaterniond> oD, Func<FPQuaternion, FPQuaternion, double, FPQuaternion> oFP)
        {
            double mE = 0, sE = 0; for (int i = 0; i < AccTestCount; i++) { var q1 = Quaterniond.Euler(Next(r, -180, 180), Next(r, -180, 180), Next(r, -180, 180)); var q2 = Quaterniond.Euler(Next(r, -180, 180), Next(r, -180, 180), Next(r, -180, 180)); double t = Next(r, 0, 1); var fp = oFP(DoubleToFPQ(q1), DoubleToFPQ(q2), t); double e = Quaterniond.Distance(oD(q1, q2, t), new Quaterniond(fp.x.ToDouble(), fp.y.ToDouble(), fp.z.ToDouble(), fp.w.ToDouble())); if (e > mE) mE = e; sE += e; }
            LogResult(n, "T in [0, 1]", mE, sE / AccTestCount);
        }

        static void VecQuaMeasure(string name, Func<float> aF, Func<double> aFP)
        {
            var sw = new Stopwatch();
            sw.Start(); float rF = aF(); sw.Stop(); double msF = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); double rFP = aFP(); sw.Stop(); double msFP = sw.Elapsed.TotalMilliseconds;
            GC.KeepAlive(rF); GC.KeepAlive(rFP);
            Console.WriteLine($"| {PadRight(name, 24)} | {msF,15:F2} ms | **{msFP,12:F2} ms** | {msFP / msF,16:F1}x |");
        }

        struct Vector3d
        {
            public double x, y, z;
            public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public double Magnitude() => Math.Sqrt(x * x + y * y + z * z);
            public Vector3d Normalize() { double m = Magnitude(); return m > 0 ? new Vector3d(x / m, y / m, z / m) : new Vector3d(0, 0, 0); }
            public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            public static double Distance(Vector3d a, Vector3d b) => Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2) + Math.Pow(a.z - b.z, 2));
        }
        struct Quaterniond
        {
            public double x, y, z, w;
            public Quaterniond(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
            public static Vector3d operator *(Quaterniond q, Vector3d v)
            {
                Vector3d qv = new Vector3d(q.x, q.y, q.z), uv = Vector3d.Cross(qv, v), uuv = Vector3d.Cross(qv, uv);
                return new Vector3d(v.x + (uv.x * q.w + uuv.x) * 2, v.y + (uv.y * q.w + uuv.y) * 2, v.z + (uv.z * q.w + uuv.z) * 2);
            }
            public static Quaterniond Euler(double x, double y, double z)
            {
                double h = (Math.PI / 180.0) * 0.5;
                double cx = Math.Cos(x * h), sx = Math.Sin(x * h), cy = Math.Cos(y * h), sy = Math.Sin(y * h), cz = Math.Cos(z * h), sz = Math.Sin(z * h);
                return new Quaterniond(cy * sx * cz + sy * cx * sz, sy * cx * cz - cy * sx * sz, cy * cx * sz - sy * sx * cz, cy * cx * cz + sy * sx * sz);
            }
            public static Quaterniond Slerp(Quaterniond a, Quaterniond b, double t)
            {
                double cos = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
                if (cos < 0) { b = new Quaterniond(-b.x, -b.y, -b.z, -b.w); cos = -cos; }
                if (cos >= 0.9999) return new Quaterniond(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
                double theta = Math.Acos(cos), sin = Math.Sin(theta);
                double rA = Math.Sin((1 - t) * theta) / sin, rB = Math.Sin(t * theta) / sin;
                return new Quaterniond(a.x * rA + b.x * rB, a.y * rA + b.y * rB, a.z * rA + b.z * rB, a.w * rA + b.w * rB);
            }
            public static double Distance(Quaterniond a, Quaterniond b) => Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2) + Math.Pow(a.z - b.z, 2) + Math.Pow(a.w - b.w, 2));
        }
        #endregion
    }
}