using System.Reflection;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>嵌入资源路径工具（探测资源名前缀——MSBuild 用 RootNamespace 作前缀）。</summary>
    internal static class EmbeddedResources
    {
        /// <summary>
        /// 探测资源前缀：找以 ".assets.文件名" 结尾的资源名，反推前缀。
        /// 例：资源 "ProbablyBagMod.assets.items.json" + fileName "items.json"
        ///     → 返回 "ProbablyBagMod.assets."（供拼其它文件名）。
        /// </summary>
        public static string FindPrefix(Assembly assembly, string fileName)
        {
            string suffix = ".assets." + fileName;
            string[] names = assembly.GetManifestResourceNames();

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(suffix))
                {
                    return names[i].Substring(0, names[i].Length - fileName.Length);
                }
            }

            MelonLogger.Warning("[嵌入资源] 未找到 " + fileName + "，现有资源："
                + string.Join(", ", names));
            return assembly.GetName().Name + ".assets.";
        }
    }
}
