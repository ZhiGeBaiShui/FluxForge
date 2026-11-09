using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 冷阱装置：抽取含量最低的液体、按相变阈值施加降温与效率倍率，
    /// 并把抽出的液体连同混合温度记入装置存量。
    /// </summary>
    public class ColdTrapDeviceTests
    {
        private static FlowEntity MakeEntity(double temperature)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, temperature);

            return new FlowEntity(
                new FlowPos(0d, 0d),
                new FlowMotionData(1, 2.5d),
                properties);
        }

        private static FlowProperties MakeStock(double water, double temperature)
        {
            FlowProperties stock = new FlowProperties();
            stock.Set(FlowProps.Water, water);
            stock.Set(FlowProps.Temperature, temperature);
            return stock;
        }

        [Fact]
        public void ExtractsLowestLiquidWithoutCrossing()
        {
            FlowEntity entity = MakeEntity(20d);
            entity.Properties.Set(FlowProps.Water, 1000d);
            entity.Properties.Set(FlowProps.Sugar, 5d);

            // 入口 20℃ 低于阈值 100℃，不跨越；基础量 4 µL 应全部来自最少的糖。
            ColdTrapDevice device = new ColdTrapDevice(10d, 100d, 4, 3d, 1000);

            bool blocked = device.TryReact(entity);

            Assert.False(blocked);
            Assert.Equal(1d, entity.Properties.Get(FlowProps.Sugar));
            Assert.Equal(1000d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(4d, device.Extracted.Get(FlowProps.Sugar));
            Assert.Equal(20d, entity.Properties.Get(FlowProps.Temperature));
            Assert.InRange(entity.HoldSeconds, 0.05d, 0.3000001d);
        }

        [Fact]
        public void CrossingThresholdCoolsFlowAndAppliesEfficiency()
        {
            FlowEntity entity = MakeEntity(50d);
            entity.Properties.Set(FlowProps.Water, 1000d);

            // 50℃ → -10℃，跨过 0℃ 相变线；基础 1 µL × 效率 3 = 3 µL。
            ColdTrapDevice device = new ColdTrapDevice(-60d, 0d, 1, 3d, 1000);

            device.TryReact(entity);

            Assert.Equal(3d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(997d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(-10d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Theory]
        [InlineData(100d)]  // 太热：入口高于阈值，但降温后仍未低于阈值
        [InlineData(-5d)]   // 太冷：入口本身就在阈值以下
        public void NonCrossingUsesBaseAmountAndKeepsTemperature(double inlet)
        {
            FlowEntity entity = MakeEntity(inlet);
            entity.Properties.Set(FlowProps.Water, 1000d);

            ColdTrapDevice device = new ColdTrapDevice(10d, 0d, 2, 3d, 1000);

            device.TryReact(entity);

            Assert.Equal(2d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(inlet, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void NoLiquidExtractsNothingAndDoesNotCool()
        {
            FlowEntity entity = MakeEntity(20d);

            // 若无目标就提前返回，本例原本会跨越 0℃ 阈值并降温。
            ColdTrapDevice device = new ColdTrapDevice(60d, 0d, 10, 3d, 1000);

            bool blocked = device.TryReact(entity);

            Assert.False(blocked);
            Assert.Equal(0d, device.Extracted.SubstanceTotal);
            Assert.Equal(20d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void CapacityCapsExtraction()
        {
            FlowEntity entity = MakeEntity(50d);
            entity.Properties.Set(FlowProps.Water, 1000d);

            // 请求 500 µL，但存量只剩 10 µL 空间。
            ColdTrapDevice device = new ColdTrapDevice(-60d, 0d, 500, 1d, 10);

            device.TryReact(entity);

            Assert.Equal(10d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(990d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(-10d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void FullCapacityExtractsNothingAndDoesNotCool()
        {
            ColdTrapDevice device = new ColdTrapDevice(60d, 0d, 10, 3d, 10);
            device.ReplaceExtracted(MakeStock(10d, 5d));

            FlowEntity entity = MakeEntity(50d);
            entity.Properties.Set(FlowProps.Water, 1000d);

            bool blocked = device.TryReact(entity);

            Assert.False(blocked);
            Assert.Equal(10d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(1000d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(50d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void PureSingleLiquidIsTheTarget()
        {
            FlowEntity entity = MakeEntity(20d);
            entity.Properties.Set(FlowProps.Sugar, 100d);

            // 没有「产物 / 杂质」之分：唯一液体就是目标。
            ColdTrapDevice device = new ColdTrapDevice(10d, 100d, 5, 3d, 1000);

            device.TryReact(entity);

            Assert.Equal(5d, device.Extracted.Get(FlowProps.Sugar));
            Assert.Equal(95d, entity.Properties.Get(FlowProps.Sugar));
        }

        [Fact]
        public void StockTemperatureIsWeightedByVolume()
        {
            ColdTrapDevice device = new ColdTrapDevice(60d, 100d, 50, 1d, 1000);
            device.ReplaceExtracted(MakeStock(100d, 10d));   // 存量 100 µL @10℃

            FlowEntity entity = MakeEntity(30d);
            entity.Properties.Set(FlowProps.Water, 1000d);

            device.TryReact(entity);

            Assert.Equal(150d, device.Extracted.Get(FlowProps.Water));
            // (10×100 + 30×50) / 150 = 16.666…℃
            Assert.Equal(2500d / 150d, device.Extracted.Get(FlowProps.Temperature), 10);
        }

        [Fact]
        public void ChangedFiresOnExtractionOnly()
        {
            ColdTrapDevice device = new ColdTrapDevice(10d, 100d, 2, 1d, 1000);
            int changed = 0;
            device.Changed += _ => changed++;

            FlowEntity withLiquid = MakeEntity(20d);
            withLiquid.Properties.Set(FlowProps.Water, 100d);
            device.TryReact(withLiquid);
            Assert.Equal(1, changed);

            FlowEntity withoutLiquid = MakeEntity(20d);
            device.TryReact(withoutLiquid);
            Assert.Equal(1, changed);
        }

        [Fact]
        public void ReplaceExtractedSwapsStockWithoutNotification()
        {
            ColdTrapDevice device = new ColdTrapDevice(10d, 0d, 1, 1d, 1000);
            int changed = 0;
            device.Changed += _ => changed++;

            device.ReplaceExtracted(MakeStock(42d, 5d));

            Assert.Equal(42d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(5d, device.Extracted.Get(FlowProps.Temperature));
            Assert.Equal(0, changed);
        }

        [Fact]
        public void ExtractionWithoutCrossingPlaysBaseSound()
        {
            FlowEntity entity = MakeEntity(20d);
            entity.Properties.Set(FlowProps.Water, 10d);
            ColdTrapDevice device = new ColdTrapDevice(-5d, 0d, 1, 20d, 1000);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.TryReact(entity);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(FlowSoundIds.Extracted, played);
        }

        [Fact]
        public void ExtractionCrossingThresholdPlaysCrossSound()
        {
            FlowEntity entity = MakeEntity(2d);
            entity.Properties.Set(FlowProps.Water, 10d);
            ColdTrapDevice device = new ColdTrapDevice(-5d, 0d, 1, 20d, 1000);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.TryReact(entity);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(FlowSoundIds.ExtractedCross, played);
        }

        [Fact]
        public void TieOnLowestUsesRegistryOrder()
        {
            FlowEntity entity = MakeEntity(20d);
            entity.Properties.Set(FlowProps.Water, 100d);
            entity.Properties.Set(FlowProps.Sugar, 100d);

            ColdTrapDevice device = new ColdTrapDevice(10d, 100d, 10, 1d, 1000);

            device.TryReact(entity);

            // 水在注册表中先于糖：同量时抽水，糖原样保留。
            Assert.Equal(90d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(100d, entity.Properties.Get(FlowProps.Sugar));
            Assert.Equal(10d, device.Extracted.Get(FlowProps.Water));
            Assert.Equal(0d, device.Extracted.Get(FlowProps.Sugar));
        }
    }
}
