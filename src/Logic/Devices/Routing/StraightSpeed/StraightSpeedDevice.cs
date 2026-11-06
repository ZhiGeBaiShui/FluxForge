namespace FluxForge
{
    /// <summary>
    /// 直通变速装置：只沿自身轴线对经过的流源加减速，不改变方向、不截停。
    ///
    /// 贴图初始朝向 Facing = 0 时，轴线是竖直的：
    ///   · 顺着箭头进入（局部 direction 0，从下往上）→ 加速
    ///   · 逆着箭头进入（局部 direction 2，从上往下）→ 减速
    /// 旋转后按同一套局部方向换算。
    ///
    /// 速度改变量、速度上限、速度下限都由构造参数给出；没有默认值，也没有无参构造。
    /// </summary>
    public sealed class StraightSpeedDevice : IFlowRouter, IFacing
    {
        private readonly double _speedChange;
        private readonly double _maxSpeed;
        private readonly double _minSpeed;
        private int _facing;

        /// <summary>按速度改变基础值、上限、下限初始化。</summary>
        public StraightSpeedDevice(
            double speedChange,
            double maxSpeed,
            double minSpeed)
        {
            _speedChange = speedChange;
            _maxSpeed = maxSpeed;
            _minSpeed = minSpeed;
        }

        /// <summary>朝向（0=上 1=右 2=下 3=左）；只影响局部轴线方向。</summary>
        public int Facing
        {
            get { return _facing; }
            set { _facing = value; }
        }

        /// <inheritdoc/>
        public bool Flipped { get; set; }

        /// <summary>每次经过时的速度改变基础值。</summary>
        public double SpeedChange
        {
            get { return _speedChange; }
        }

        /// <summary>加速后的速度上限。</summary>
        public double MaxSpeed
        {
            get { return _maxSpeed; }
        }

        /// <summary>减速后的速度下限。</summary>
        public double MinSpeed
        {
            get { return _minSpeed; }
        }

        /// <summary>
        /// 只处理沿局部轴线进入的流源；横向经过的流源原样放行。
        /// 恒返回 false：变速不截停流源，由运动层继续推进。
        /// </summary>
        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            int localDirection = (incoming.Motion.Direction - _facing + 4) % 4;

            double current = incoming.Motion.Speed;
            double next;

            if (localDirection == 0)
            {
                next = System.Math.Min(_maxSpeed, current + _speedChange);
            }
            else if (localDirection == 2)
            {
                next = System.Math.Max(_minSpeed, current - _speedChange);
            }
            else
            {
                return false;
            }

            // 已经到上下限 / 改动值为 0：对速度没有影响，不播放音效。
            if (next == current)
            {
                return false;
            }

            incoming.Motion.Speed = next;

            FlowAudio.Play(
                localDirection == 0
                    ? FlowSoundIds.SpeedUp
                    : FlowSoundIds.SpeedDown,
                this,
                System.Math.Abs(_speedChange));

            return false;
        }
    }
}
