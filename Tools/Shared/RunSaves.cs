using System.IO;
using Deeptide.Core.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Deeptide.Tools
{
    /// <summary>
    /// 存档读写（策划/08 §6）：Core 只给出纯数据的 RunState，这里用 Newtonsoft 序列化成 JSON。
    /// Unity 端用官方的 com.unity.nuget.newtonsoft-json 包，写法一样。
    /// </summary>
    static class RunSaves
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(RunState state) => JsonConvert.SerializeObject(state, Settings);

        public static RunState FromJson(string json) => JsonConvert.DeserializeObject<RunState>(json, Settings);

        /// <summary>先写临时文件再改名替换，防止写到一半损坏。</summary>
        public static void Write(string path, RunEngine run)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(run.SaveState()));
            File.Move(tmp, path, true);
        }

        public static RunEngine Read(string path, RunConfig config = null) => RunEngine.Load(FromJson(File.ReadAllText(path)), config);
    }
}
