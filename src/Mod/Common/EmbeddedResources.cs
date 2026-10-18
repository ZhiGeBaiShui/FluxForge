using System.IO;
using System.Reflection;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>嵌入资源工具：探测资源名前缀（MSBuild 用 RootNamespace 作前缀）+ 读取资源内容。</summary>
    internal static class EmbeddedResources
    {
        /// <summary>读取嵌入资源（prefix + fileName）；找不到返回 null。</summary>
        public static byte[] Read(string prefix, string fileName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(prefix + fileName))
            {
                if (stream == null)
                {
                    return null;
                }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }

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
