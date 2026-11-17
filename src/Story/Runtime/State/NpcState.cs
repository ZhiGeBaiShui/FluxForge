namespace FluxForge.Story
{
    /// <summary>
    /// 游戏内 NPC 状态适配器。只通过 modData 持久化，不依赖具体 NPC 类。
    /// </summary>
    internal sealed class NpcState : IStoryState
    {
        private const string Prefix = "fluxforge.story.";

        private readonly NpcDefinition _definition;

        internal NpcState(NpcDefinition definition)
        {
            _definition = definition;
        }

        public int VisitCount
        {
            get
            {
                string raw;
                if (!PlayerStore.Instance.modData.TryGetValue(VisitCountKey, out raw))
                {
                    return 0;
                }

                return int.Parse(raw);
            }
        }

        public int LastVisitDay
        {
            get
            {
                string raw;
                if (!PlayerStore.Instance.modData.TryGetValue(
                    LastVisitDayKey,
                    out raw))
                {
                    return -1;
                }

                return int.Parse(raw);
            }
        }

        private string VisitCountKey
        {
            get
            {
                return _definition.VisitCountKey
                    ?? Prefix + _definition.Identifier + ".visits";
            }
        }

        private string LastVisitDayKey
        {
            get
            {
                return Prefix + _definition.Identifier + ".lastVisitDay";
            }
        }

        internal void SetLastVisitDay(int day)
        {
            PlayerStore.Instance.modData[LastVisitDayKey] = day.ToString();
        }

        internal void IncrementVisitCount()
        {
            PlayerStore.Instance.modData[VisitCountKey]
                = (VisitCount + 1).ToString();
        }

        public bool HasFlag(string key)
        {
            return PlayerStore.Instance.modData.ContainsKey(key);
        }

        public void MarkFlag(string key)
        {
            PlayerStore.Instance.modData[key] = "1";
        }

        public bool HasTag(string tag)
        {
            return PlayerStore.Instance.modData.ContainsKey(TagKey(tag));
        }

        public void SetTag(string tag, bool enabled)
        {
            string key = TagKey(tag);
            if (enabled)
            {
                PlayerStore.Instance.modData[key] = "1";
            }
            else
            {
                PlayerStore.Instance.modData.Remove(key);
            }
        }

        public int SeenCount(string eventId)
        {
            string raw;
            if (!PlayerStore.Instance.modData.TryGetValue(
                SeenKey(eventId),
                out raw))
            {
                return 0;
            }

            return int.Parse(raw);
        }

        public void MarkSeen(string eventId)
        {
            PlayerStore.Instance.modData[SeenKey(eventId)]
                = (SeenCount(eventId) + 1).ToString();
        }

        private string TagKey(string tag)
        {
            return Prefix + _definition.Identifier + ".tag." + tag;
        }

        private string SeenKey(string eventId)
        {
            return Prefix + _definition.Identifier + ".seen." + eventId;
        }
    }
}
