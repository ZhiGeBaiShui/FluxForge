using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 属性注册表的固定契约：数量、种类、顺序、唯一性与槽位有效性。
    /// </summary>
    public class FlowPropertyRegistryTests
    {
        [Fact]
        public void Count_IsThirtyEight()
        {
            Assert.Equal(38, FlowPropertyRegistry.Count);
            Assert.Equal(38, FlowPropertyRegistry.All.Count);
        }

        [Fact]
        public void All_HasThirtySixLiquidsAndOneForeignAndOneScalar()
        {
            int liquids = 0;
            int foreign = 0;
            int scalars = 0;
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                if (def.Kind == FlowPropertyKind.Liquid)
                {
                    liquids++;
                }
                else if (def.Kind == FlowPropertyKind.Foreign)
                {
                    foreign++;
                }
                else if (def.Kind == FlowPropertyKind.Scalar)
                {
                    scalars++;
                }
            }

            Assert.Equal(36, liquids);
            Assert.Equal(1, foreign);
            Assert.Equal(1, scalars);
        }

        [Fact]
        public void Rules_MatchPropertyKind()
        {
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                if (def.Kind == FlowPropertyKind.Liquid
                    || def.Kind == FlowPropertyKind.Foreign)
                {
                    Assert.Equal(SplitRule.Divide, def.Split);
                    Assert.Equal(MergeRule.Add, def.Merge);
                }
                else
                {
                    Assert.Equal(SplitRule.Keep, def.Split);
                    Assert.Equal(MergeRule.WeightedAverage, def.Merge);
                }
            }
        }

        [Fact]
        public void Ids_AreUnique()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                Assert.True(ids.Add(def.Id), "重复的属性 Id：" + def.Id);
            }
        }

        [Fact]
        public void Order_MatchesGameLiquidInit_AndEndsWithForeignThenTemperature()
        {
            Assert.Equal("water", FlowPropertyRegistry.All[0].Id);
            Assert.Equal("nomore", FlowPropertyRegistry.All[35].Id);
            Assert.Equal("foreign", FlowPropertyRegistry.All[36].Id);
            Assert.Equal(FlowPropertyKind.Foreign, FlowPropertyRegistry.All[36].Kind);
            Assert.Equal("container_temp", FlowPropertyRegistry.All[37].Id);
            Assert.Equal(FlowPropertyKind.Scalar, FlowPropertyRegistry.All[37].Kind);
        }

        [Fact]
        public void Keys_AreOneBased_AndReadWriteOwnSlot()
        {
            FlowProperties p = new FlowProperties();

            for (int i = 0; i < FlowPropertyRegistry.Count; i++)
            {
                FlowPropertyDef def = FlowPropertyRegistry.All[i];
                Assert.Equal(i + 1, def.Key.Index);
                p.Set(def.Key, i + 1);
            }

            for (int i = 0; i < FlowPropertyRegistry.Count; i++)
            {
                FlowPropertyDef def = FlowPropertyRegistry.All[i];
                Assert.Equal(i + 1, p.Get(def.Key));
            }
        }

        [Fact]
        public void FlowProps_Fields_AreRegisteredAndValid()
        {
            FieldInfo[] fields = typeof(FlowProps).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            Assert.Equal(FlowPropertyRegistry.Count, fields.Length);

            foreach (FieldInfo field in fields)
            {
                FlowPropertyKey key = (FlowPropertyKey)field.GetValue(null);
                Assert.InRange(key.Index, 1, FlowPropertyRegistry.Count);
            }
        }

        [Fact]
        public void FlowProps_FieldNamesMatchRegistryIds()
        {
            FieldInfo[] fields = typeof(FlowProps).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            Assert.Equal(38, fields.Length);

            HashSet<string> ids = new HashSet<string>();
            foreach (FieldInfo field in fields)
            {
                Assert.Equal(typeof(FlowPropertyKey), field.FieldType);

                // 温度字段名保持 Temperature，属性 id 按游戏 tag 约定命名为 container_temp。
                string id = field.Name == nameof(FlowProps.Temperature)
                    ? "container_temp"
                    : ToSnakeCase(field.Name);
                FlowPropertyKey key = (FlowPropertyKey)field.GetValue(null);

                Assert.Equal(FlowPropertyRegistry.Get(id).Key.Index, key.Index);
                Assert.True(ids.Add(id), "字段名映射到重复的属性 Id：" + id);
            }

            Assert.Equal(38, ids.Count);
        }

        [Fact]
        public void Get_Water_AndFlowPropsWater_AddressSameSlot()
        {
            FlowPropertyDef water = FlowPropertyRegistry.Get("water");

            Assert.Equal("water", water.Id);
            Assert.Equal(FlowProps.Water.Index, water.Key.Index);

            FlowProperties p = new FlowProperties();
            p.Set(FlowPropertyRegistry.Key("water"), 12.5d);

            Assert.Equal(12.5d, p.Get(FlowProps.Water));
        }

        [Fact]
        public void Get_UnknownId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => FlowPropertyRegistry.Get("not_a_registered_property"));
        }

        /// <summary>PascalCase → snake_case：DreamCapMush → dream_cap_mush。</summary>
        private static string ToSnakeCase(string name)
        {
            StringBuilder builder = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        builder.Append('_');
                    }

                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
