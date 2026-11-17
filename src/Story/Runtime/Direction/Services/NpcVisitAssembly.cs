using System;

namespace FluxForge.Story
{
    /// <summary>一次来访的主剧情、交易后剧情和离场前剧情，共用同一个播放器。</summary>
    internal sealed class NpcVisitAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }
        internal StoryOutcome Outcome { get; private set; }
        private readonly StoryPlaybackContext _playback;

        internal NpcVisitAssembly(StoryPlaybackContext playback, StoryEventDefinition main)
        {
            _playback = playback;
            Outcome = main.Outcome;
            Entry = new StageEdge(sequence =>
            {
                sequence.Call(main, playback.Visit);
                sequence.Call(new StageAssembly(AppendTrade));
                sequence.Call(new StageAssembly(AppendEnding));
            });
        }

        private void AppendTrade(StorySequence sequence)
        {
            StoryTradeResult trade = StoryNpcRuntime.TakeTradeCompleted();
            if (trade == null)
            {
                return;
            }
            StoryContext context = new StoryContext(
                _playback.Visit.Definition, _playback.Visit.State,
                StoryTrigger.TradeCompleted, _playback.Visit.VisitKind,
                _playback.Visit.Day, trade);
            StoryEventDefinition definition = Select(context);
            if (definition != null)
            {
                sequence.Call(definition, context);
            }
        }

        private void AppendEnding(StorySequence sequence)
        {
            StoryContext context = new StoryContext(
                _playback.Visit.Definition, _playback.Visit.State,
                StoryTrigger.VisitEnding, _playback.Visit.VisitKind,
                _playback.Visit.Day, null);
            StoryEventDefinition definition = Select(context);
            if (definition != null)
            {
                sequence.Call(definition, context);
                sequence.Do(() =>
                {
                    if (definition.Outcome != StoryOutcome.Default)
                    {
                        Outcome = definition.Outcome;
                    }
                });
            }
        }

        private StoryEventDefinition Select(StoryContext context)
        {
            StoryEvent selected = StoryEventSelector.Select(
                _playback.Content.Events, context, _playback.ConsumedGroups);
            if (selected == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(selected.ExclusionGroup))
            {
                _playback.ConsumedGroups.Add(selected.ExclusionGroup);
            }

            return _playback.Content.FindEvent(selected.Id);
        }

        private sealed class StageAssembly : StoryAssembly
        {
            internal override StoryEdge Entry { get; }
            internal StageAssembly(Action<StorySequence> register)
            {
                Entry = new StageEdge(register);
            }
        }

        private sealed class StageEdge : StoryEdge
        {
            private readonly Action<StorySequence> _register;
            internal StageEdge(Action<StorySequence> register)
                : base(null, null)
            {
                _register = register;
            }

            internal override void Execute(StorySequence sequence)
            {
                _register(sequence);
            }
        }
    }
}
