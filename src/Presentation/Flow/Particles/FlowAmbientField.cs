using System;
using FluxForge.Presentation.Flow.Visual;

namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>
    /// 一颗常驻环境粒子的渲染快照。**纯值类型、零 Unity 依赖**。
    ///
    /// 坐标单位是「格」（1.0 = 一格 = 16 个设计像素），Y 轴向下，与
    /// <c>docs/flow-visual/final/SPEC.md</c> 的画布约定一致。
    /// 颜色是 8 位 RGB；出生时的 alpha 与尺寸随粒子一起保存，
    /// 这样 modifier 改过的 <see cref="FlowParticleProfile.AlphaStart"/> / <see cref="FlowParticleProfile.SizeCells"/>
    /// 才能真正生效（绘制层不再读全局 tuning 常量）。
    /// </summary>
    public struct FlowAmbientParticle
    {
        /// <summary>格坐标 X。</summary>
        public float X;

        /// <summary>格坐标 Y（向下为正）。</summary>
        public float Y;

        /// <summary>速度 X（格/秒）。</summary>
        public float Vx;

        /// <summary>速度 Y（格/秒，向下为正）。</summary>
        public float Vy;

        /// <summary>剩余寿命（秒），≤ 0 时由 <see cref="FlowAmbientField.Advance"/> 移除。</summary>
        public float Life;

        /// <summary>出生时的总寿命（秒），用于计算寿命比例。</summary>
        public float MaxLife;

        /// <summary>红通道，0–255。</summary>
        public byte R;

        /// <summary>绿通道，0–255。</summary>
        public byte G;

        /// <summary>蓝通道，0–255。</summary>
        public byte B;

        /// <summary>
        /// 出生时的 alpha（0–255）。绘制层按 <c>Life / MaxLife × (Alpha / 255)</c> 衰减；
        /// 与 <see cref="FlowParticleProfile.AlphaStart"/> 对应，modifier 改 profile 后能落到这里。
        /// </summary>
        public byte Alpha;

        /// <summary>
        /// 设计像素尺寸（1–4）。默认 1（= 1/16 格）；与 <see cref="FlowParticleProfile.SizeCells"/> 对应。
        /// </summary>
        public byte SizePx;
    }

    /// <summary>
    /// 常驻环境粒子的纯逻辑场：生成、积分、寿命衰减、swap-remove。
    /// **零 Unity 依赖、零 LINQ / 零闭包**，稳态 Spawn/Advance 不产生托管分配。
    ///
    /// 设计取舍：
    ///   · 内部用并行数组存粒子及其「出生点/出生半径/出生序号」，避免给
    ///     <see cref="FlowAmbientParticle"/> 增加渲染层不需要的字段；
    ///   · 满容量时覆盖出生序号最小的粒子（最老），而不是直接丢弃新粒子——
    ///     新粒子是当前流源的活性信号，丢新粒子会让画面「卡住」；
    ///   · 随机数用构造函数注入种子的 <see cref="Random"/> 实例，测试可复现，
    ///     也避免碰到 Unity 的全局随机状态。
    ///
    /// 坐标原点：<see cref="Spawn"/> 的 <c>cellX/cellY</c> 是**格左上角**；
    /// 本体包围盒按 SPEC §1 的 16×16 像素坐标换算到格，再整体平移到该格。
    /// </summary>
    public sealed class FlowAmbientField
    {
        /// <summary>一格的设计像素数（SPEC §0）。</summary>
        private const int GridPixels = FlowPixelMath.CellPixels;

        /// <summary>边缘生成的概率：30% 在包围盒最外 1px，其余在内部。</summary>
        private const double EdgeSpawnChance = 0.30d;

        /// <summary>本体在 16×16 tile 内的像素中心（SPEC §1：中心 7.5/7.5，浮点坐标 8/8）。</summary>
        private const float GridCenterPixel = 8f;

        private readonly FlowAmbientParticle[] _particles;

        /// <summary>每颗粒子的出生点（格），用于判断扩散距离。</summary>
        private readonly float[] _originX;
        private readonly float[] _originY;

        /// <summary>每颗粒子出生时使用的扩散半径（格）；profile 之后变化不影响已存在的粒子。</summary>
        private readonly float[] _spreadRadius;

        /// <summary>出生序号，用于容量满时找最老的一颗（swap-remove 后仍能正确比较）。</summary>
        private readonly long[] _spawnOrder;

        private readonly Random _random;

        private int _count;
        private long _nextSpawnOrder;
        private FlowContent _spawnContent = FlowContent.Large;

        /// <summary>
        /// 构造一个粒子场。
        /// </summary>
        /// <param name="capacity">最大存活粒子数，必须 &gt; 0。</param>
        /// <param name="seed">随机种子；同一种子 + 同一调用序列可复现。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> ≤ 0。</exception>
        public FlowAmbientField(int capacity = 256, int seed = 1)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "粒子容量必须大于 0。");
            }

            _particles = new FlowAmbientParticle[capacity];
            _originX = new float[capacity];
            _originY = new float[capacity];
            _spreadRadius = new float[capacity];
            _spawnOrder = new long[capacity];
            _random = new Random(seed);
        }

        /// <summary>当前存活粒子数。</summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>
        /// 当前流源的含量档，决定 <see cref="Spawn"/> 使用的本体包围盒。
        /// 只影响之后新生成的粒子；已存在的粒子保留出生时的位置与尺寸。
        ///
        /// **契约补充说明**：冻结的 <see cref="Spawn"/> 签名没有 FlowContent 参数，
        /// 因此这里提供一个可写属性（默认 <see cref="FlowContent.Large"/>）。
        /// 单流源调用方在含量档变化时同步本属性；多流源共用同一个场时，
        /// 建议每源一个场实例，避免在 Spawn 之间来回切换。越界枚举值按 large 兜底。
        /// </summary>
        public FlowContent SpawnContent
        {
            get { return _spawnContent; }
            set { _spawnContent = value; }
        }

        /// <summary>
        /// 读取第 <paramref name="index"/> 颗粒子的快照（值拷贝，外部改动不会影响场内状态）。
        /// 索引按当前存活数组计，swap-remove 后顺序会变化，调用方不要跨 Advance 缓存索引。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">索引不在 0..Count-1。</exception>
        public FlowAmbientParticle Get(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _particles[index];
        }

        /// <summary>
        /// 在流源所在格生成一颗粒子。
        ///
        /// 位置：70% 落在本体包围盒内部（离最外 1px 至少 1px），30% 落在包围盒最外 1px；
        /// 包围盒固定使用 SPEC §1 的**右向基准**（空 6×1、少 8×6、中 8×8、多 12×8），
        /// 不随 <paramref name="dir"/> 旋转——粒子是圆形散布，方向只影响后偏，不影响出生区域。
        /// 方向：以格中心为圆心的径向单位向量，加上 <see cref="FlowParticleProfile.BackwardBias"/>
        /// 倍的运动反方向，再归一化；速度大小落在 [SpeedMin, SpeedMax]。
        /// 寿命：落在 [LifeMin, LifeMax]；颜色原样使用 <paramref name="color"/>。
        ///
        /// 容量满时覆盖最老的一颗；稳态 0 分配。
        /// </summary>
        public void Spawn(
            in FlowParticleProfile profile,
            double cellX,
            double cellY,
            int dir,
            in FlowColor color)
        {
            int x0;
            int x1;
            int y0;
            int y1;
            GetBounds(_spawnContent, out x0, out x1, out y0, out y1);

            int pixelX;
            int pixelY;
            PickPixel(x0, x1, y0, y1, out pixelX, out pixelY);

            float positionX = (float)(cellX + (pixelX + 0.5d) / GridPixels);
            float positionY = (float)(cellY + (pixelY + 0.5d) / GridPixels);

            // 径向：从格中心指向出生像素。
            // 前提：出生像素坐标取半整数（pixel + 0.5），格中心是整数 8，
            // 所以两个分量恒为非零的 .5 值，径向长度至少 0.5，不存在零长度需要兜底。
            float radialX = pixelX + 0.5f - GridCenterPixel;
            float radialY = pixelY + 0.5f - GridCenterPixel;
            float radialLength = MathF.Sqrt(radialX * radialX + radialY * radialY);
            radialX /= radialLength;
            radialY /= radialLength;

            // 运动反方向从 FlowDirMath 取：越界方向按 right 兜底。
            FlowDir flowDir = (FlowDir)dir;
            float backwardX = FlowDirMath.BackwardX(flowDir);
            float backwardY = FlowDirMath.BackwardY(flowDir);

            float directionX = radialX + backwardX * profile.BackwardBias;
            float directionY = radialY + backwardY * profile.BackwardBias;
            float directionLength = MathF.Sqrt(directionX * directionX + directionY * directionY);

            // 前提：径向单位向量不可能正好落在坐标轴上（两个分量都是 .5 的半整数），
            // 因此不论 BackwardBias 取何值，径向与后向都不可能完全抵消，长度恒 > 0。
            directionX /= directionLength;
            directionY /= directionLength;

            float speed = NextRange(profile.SpeedMin, profile.SpeedMax);
            float life = NextRange(profile.LifeMin, profile.LifeMax);

            int index;
            if (_count < _particles.Length)
            {
                index = _count;
                _count++;
            }
            else
            {
                index = FindOldestIndex();
            }

            FlowAmbientParticle particle = new FlowAmbientParticle
            {
                X = positionX,
                Y = positionY,
                Vx = directionX * speed,
                Vy = directionY * speed,
                Life = life,
                MaxLife = life,
                R = color.R,
                G = color.G,
                B = color.B,
                // 出生外观随粒子固化：绘制层不再读全局 tuning，modifier 改 profile 才有效。
                Alpha = ToByteAlpha(profile.AlphaStart),
                SizePx = ToSizePixels(profile.SizeCells),
            };

            _particles[index] = particle;
            _originX[index] = positionX;
            _originY[index] = positionY;
            _spreadRadius[index] = profile.SpreadRadius;
            _spawnOrder[index] = ++_nextSpawnOrder;
        }

        /// <summary>
        /// 推进一帧：位置积分 → 寿命衰减 → 死亡移除。
        ///
        /// 粒子离出生点超过出生时的 <see cref="FlowParticleProfile.SpreadRadius"/> 后，
        /// 每秒额外扣除 <c>超出距离 × FlowParticleTuning.SpreadFadeRate</c> 的寿命，
        /// 表现为「越散越淡、越快消失」；寿命 ≤ 0 即移除。
        ///
        /// 用 swap-remove 维持紧凑数组：移除第 i 颗时把最后一颗搬过来，再原地继续检查，
        /// 因此单次 Advance 是 O(n)、0 分配。deltaTime ≤ 0 或 NaN 时直接返回，暂停不老化。
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!(deltaTime > 0f))
            {
                return;
            }

            int index = 0;
            while (index < _count)
            {
                ref FlowAmbientParticle particle = ref _particles[index];

                particle.X += particle.Vx * deltaTime;
                particle.Y += particle.Vy * deltaTime;

                float offsetX = particle.X - _originX[index];
                float offsetY = particle.Y - _originY[index];
                float distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);

                float lifeLoss = deltaTime;
                float overshoot = distance - _spreadRadius[index];
                if (overshoot > 0f)
                {
                    lifeLoss += overshoot * FlowParticleTuning.SpreadFadeRate * deltaTime;
                }

                particle.Life -= lifeLoss;

                if (particle.Life <= 0f)
                {
                    RemoveAt(index);
                    continue;
                }

                index++;
            }
        }

        /// <summary>清空所有存活粒子（不改随机序列，也不改 <see cref="SpawnContent"/>）。</summary>
        public void Clear()
        {
            _count = 0;
            _nextSpawnOrder = 0L;
        }

        /// <summary>
        /// 按含量档取本体包围盒（16×16 tile 像素，半开区间 [x0,x1) × [y0,y1)）。
        /// 固定使用 SPEC §1 的右向基准：空 6×1 @(5,7)、少 8×6 @(4,5)、
        /// 中 8×8 @(4,4)、多 12×8 @(2,4)。粒子是圆形散布，不随方向旋转 bbox。
        /// </summary>
        private static void GetBounds(
            FlowContent content,
            out int x0,
            out int x1,
            out int y0,
            out int y1)
        {
            switch (content)
            {
                case FlowContent.Empty:
                    x0 = 5;
                    x1 = 11;
                    y0 = 7;
                    y1 = 8;
                    break;

                case FlowContent.Small:
                    x0 = 4;
                    x1 = 12;
                    y0 = 5;
                    y1 = 11;
                    break;

                case FlowContent.Mid:
                    x0 = 4;
                    x1 = 12;
                    y0 = 4;
                    y1 = 12;
                    break;

                default:
                    // Large 以及越界枚举值。
                    x0 = 2;
                    x1 = 14;
                    y0 = 4;
                    y1 = 12;
                    break;
            }
        }

        /// <summary>
        /// 选出生像素：30% 走最外 1px，70% 走内部（离边界至少 1px）。
        /// 空档是 6×1 的干痕，没有内部像素，此时全部落到边缘（语义上整条都是边缘）。
        /// </summary>
        private void PickPixel(int x0, int x1, int y0, int y1, out int pixelX, out int pixelY)
        {
            int width = x1 - x0;
            int height = y1 - y0;
            bool edge = _random.NextDouble() < EdgeSpawnChance;

            if (!edge && width > 2 && height > 2)
            {
                pixelX = x0 + 1 + _random.Next(width - 2);
                pixelY = y0 + 1 + _random.Next(height - 2);
                return;
            }

            PickEdgePixel(x0, x1, y0, y1, out pixelX, out pixelY);
        }

        /// <summary>在包围盒最外 1px 上选一点：先等概率选边，再沿该边等概率选像素。</summary>
        private void PickEdgePixel(int x0, int x1, int y0, int y1, out int pixelX, out int pixelY)
        {
            int width = x1 - x0;
            int height = y1 - y0;

            switch (_random.Next(4))
            {
                case 0: // 左边
                    pixelX = x0;
                    pixelY = y0 + _random.Next(height);
                    break;

                case 1: // 右边
                    pixelX = x1 - 1;
                    pixelY = y0 + _random.Next(height);
                    break;

                case 2: // 上边
                    pixelX = x0 + _random.Next(width);
                    pixelY = y0;
                    break;

                default: // 下边
                    pixelX = x0 + _random.Next(width);
                    pixelY = y1 - 1;
                    break;
            }
        }

        /// <summary>
        /// 在 [min,max] 内均匀采样。max ≤ min 时返回 min（负数钳到 0），
        /// 让 modifier 的脏输出退化成常量而不是 NaN。热路径无分配。
        /// </summary>
        private float NextRange(float min, float max)
        {
            if (!(max > min))
            {
                return min > 0f ? min : 0f;
            }

            return min + (float)_random.NextDouble() * (max - min);
        }

        /// <summary>alpha 0–1 → 0–255（四舍五入并夹紧）。</summary>
        private static byte ToByteAlpha(float alpha)
        {
            if (!(alpha > 0f))
            {
                return 0;
            }

            if (alpha >= 1f)
            {
                return 255;
            }

            return (byte)MathF.Round(alpha * 255f);
        }

        /// <summary>尺寸（格）→ 设计像素（1–4），1 像素 = 1/16 格。</summary>
        private static byte ToSizePixels(float sizeCells)
        {
            if (!(sizeCells > 0f))
            {
                return 1;
            }

            int pixels = (int)MathF.Round(sizeCells * GridPixels);
            if (pixels < 1)
            {
                return 1;
            }

            return pixels > 4 ? (byte)4 : (byte)pixels;
        }

        /// <summary>找出生序号最小的一颗；仅在容量已满时调用，容量 ≤ 256 时线性扫描足够。</summary>
        private int FindOldestIndex()
        {
            int oldest = 0;
            long oldestOrder = _spawnOrder[0];

            for (int i = 1; i < _count; i++)
            {
                if (_spawnOrder[i] < oldestOrder)
                {
                    oldest = i;
                    oldestOrder = _spawnOrder[i];
                }
            }

            return oldest;
        }

        /// <summary>
        /// swap-remove：把最后一颗搬到被移除的位置，计数减一。只在 <see cref="Advance"/> 内部调用，
        /// 同时搬运出生点/半径/序号三个并行数组，保证索引始终对齐。
        /// </summary>
        private void RemoveAt(int index)
        {
            int last = _count - 1;
            if (index != last)
            {
                _particles[index] = _particles[last];
                _originX[index] = _originX[last];
                _originY[index] = _originY[last];
                _spreadRadius[index] = _spreadRadius[last];
                _spawnOrder[index] = _spawnOrder[last];
            }

            _count = last;
        }
    }
}
