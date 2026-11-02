using System.Collections.Generic;
using FluxForge.Presentation.Flow.Visual;

namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>单个视觉视图的粒子状态与逐帧算法。</summary>
    internal sealed class FlowParticleSystem
    {
        /// <summary>每源独立粒子场与发射余量。</summary>
        private sealed class ParticleSlot
        {
            public FlowAmbientField Field;
            public float Accum;
        }

        private readonly Dictionary<FlowEntity, ParticleSlot> _particles
            = new Dictionary<FlowEntity, ParticleSlot>();

        private readonly List<FlowAmbientField> _fading = new List<FlowAmbientField>();
        private int _seed;

        public void Add(FlowEntity entity)
        {
            ParticleSlot slot = new ParticleSlot();
            slot.Field = new FlowAmbientField(32, _seed++);
            slot.Accum = 0f;
            _particles[entity] = slot;
        }

        public void Remove(FlowEntity entity)
        {
            ParticleSlot slot = _particles[entity];
            _particles.Remove(entity);
            _fading.Add(slot.Field);
        }

        public void Update(FlowEntity entity, in FlowFrameInput snapshot, float deltaTime)
        {
            ParticleSlot slot = _particles[entity];
            FlowParticleProfile profile = FlowParticleRules.Build(in snapshot);

            slot.Field.SpawnContent = snapshot.Content;
            slot.Field.Advance(deltaTime);

            // 先积分旧粒子，再按余量出生。
            slot.Accum += profile.SpawnRate * deltaTime;
            while (slot.Accum >= 1f)
            {
                slot.Accum -= 1f;
                slot.Field.Spawn(
                    profile,
                    snapshot.X,
                    snapshot.Y,
                    snapshot.Direction,
                    FlowParticleRules.ColorFor(in snapshot));
            }
        }

        public void CompleteFrame(float deltaTime, List<FlowAmbientParticle> output)
        {
            AdvanceFading(deltaTime);
            CollectDots(output);
        }

        public void ClearAmbient()
        {
            foreach (ParticleSlot slot in _particles.Values)
            {
                slot.Field.Clear();
            }

            for (int i = 0; i < _fading.Count; i++)
            {
                _fading[i].Clear();
            }

            _fading.Clear();
        }

        public void Reset()
        {
            _particles.Clear();
            _fading.Clear();
        }

        private void AdvanceFading(float deltaTime)
        {
            // 倒序移除，避免跳过尚未检查的场。
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                FlowAmbientField field = _fading[i];
                field.Advance(deltaTime);
                if (field.Count == 0)
                {
                    _fading.RemoveAt(i);
                }
            }
        }

        private void CollectDots(List<FlowAmbientParticle> output)
        {
            output.Clear();
            foreach (ParticleSlot slot in _particles.Values)
            {
                AppendDots(slot.Field, output);
            }

            for (int i = 0; i < _fading.Count; i++)
            {
                AppendDots(_fading[i], output);
            }
        }

        private static void AppendDots(FlowAmbientField field, List<FlowAmbientParticle> output)
        {
            int count = field.Count;
            for (int i = 0; i < count; i++)
            {
                output.Add(field.Get(i));
            }
        }
    }
}
