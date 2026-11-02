using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>嵌入资源工具：探测资源名前缀（MSBuild 用 RootNamespace 作前缀）+ 读取资源内容。</summary>
    internal static class EmbeddedResources
    {
        // Mono 下 Texture2D.LoadImage 是 ImageConversion 的扩展方法，直接调用即可；
        // 目前只有流源蒙版图集走嵌入资源，物品贴图已迁到 mod/Textures/Items。

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
        /// 读取文本资源（JSON 等）并按 UTF-8 解码；缺失返回 null。
        /// 只共享「读字节 + 解码」，解析与校验仍由各调用方自己做——
        /// 图集索引和物品配置的校验逻辑差异很大，强行合并会把错误信息搅在一起。
        /// </summary>
        public static string ReadText(string prefix, string fileName)
        {
            byte[] data = Read(prefix, fileName);
            return data == null ? null : Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// 从嵌入资源读一张 PNG 并解码成 Texture2D。
        ///
        /// 参数固定为像素风需要的三件套：Point（放大不糊）、Clamp（相邻 tile 不串色）、
        /// DontSave（防 Unity 资源管理卸载）。缺失或解码失败都返回 null，
        /// 由调用方决定降级策略（整层停用 / 单张贴图缺失）。
        /// </summary>
        public static Texture2D LoadTexture(string prefix, string fileName)
        {
            byte[] png = Read(prefix, fileName);
            if (png == null)
            {
                return null;
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.DontSave;
                ImageConversion.LoadImage(texture, png);
                return texture;
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[嵌入资源] 贴图解码失败 " + fileName + "：" + e);

                // 解码失败时刚创建的贴图不会再被任何字段引用，先销毁，避免每次重载漏一份
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                return null;
            }
        }

        /// <summary>
        /// 探测资源前缀：找以 ".assets.文件名" 结尾的资源名，反推前缀。
        /// 例：资源 "FluxForge.assets.items.json" + fileName "items.json"
        ///     → 返回 "FluxForge.assets."（供拼其它文件名）。
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

            FFModEntry.Log.Warning("[嵌入资源] 未找到 " + fileName + "，现有资源："
                + string.Join(", ", names));
            return assembly.GetName().Name + ".assets.";
        }
    }
}
