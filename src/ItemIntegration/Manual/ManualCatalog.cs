using System;
using System.Collections.Generic;

namespace FluxForge.Mod
{
    internal static class ManualCatalog
    {
        private static readonly List<(string Tag, Action Open)> Manuals = new()
        {
            ("device_guide", DeviceManual.Open)
        };

        internal static void Open(GameItem item)
        {
            foreach (var manual in Manuals)
            {
                if (item.IsTag(manual.Tag))
                {
                    manual.Open();
                    return;
                }
            }

            throw new InvalidOperationException("手册未配置已注册的手册 tag：" + item.identifier);
        }
    }
}
