namespace FluxForge.Story.Tests
{
    // 原版ModLoader按物理行、首个逗号和ParseCsvValue读取；测试使用相同文件格式。
    internal static class NativeLocalizationData
    {
        internal static Dictionary<string, string> Read(string locale)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Localization", locale + ".csv");
            Dictionary<string, string> entries = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                int comma = line.IndexOf(',');
                string key = line.Substring(0, comma).Trim();
                string value = line.Substring(comma + 1).Trim();
                value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"").Replace("\\n", "\n");
                entries.Add(key, value);
            }
            return entries;
        }
    }
}
