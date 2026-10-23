using System.IO;
using Deeptide.Core.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 存档读写（策划/06 §8、08 §6）：Core 只给出纯数据的 RunState，这里用 Newtonsoft 序列化成 JSON。
    /// 格式和 Tools/Shared/RunSaves.cs 完全一样，控制台的存档和游戏里的存档可以互相读。
    /// </summary>
    public static class RunSaves
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(RunState state) => JsonConvert.SerializeObject(state, Settings);

        public static RunState FromJson(string json) => JsonConvert.DeserializeObject<RunState>(json, Settings);

        /// <summary>先写临时文件再替换，写到一半中断不会损坏旧档。</summary>
        public static void Write(string path, RunEngine run)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(run.SaveState()));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static RunEngine Read(string path, RunConfig config = null) => RunEngine.Load(FromJson(File.ReadAllText(path)), config);

        /// <summary>只读出存档的概要（标题画面的「继续」按钮用），读不了返回 null。</summary>
        public static RunState Peek(string path)
        {
            try
            {
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
