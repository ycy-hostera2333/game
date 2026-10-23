using System.IO;
using System.Linq;
using Deeptide.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deeptide.View
{
    /// <summary>每个场景入口都要做的两件事：载入 CSV（场景里引用的 TextAsset，按文件名匹配）、保证有 EventSystem。</summary>
    public static class SceneSetup
    {
        /// <summary>载入游戏数据；已经载入过（从别的场景切过来）就跳过。校验错误逐条打到 Console 再抛出。</summary>
        public static void LoadData(TextAsset[] files, string owner)
        {
            if (GameData.IsLoaded) return;
            var source = DataSource.From(fileName =>
            {
                string key = Path.GetFileNameWithoutExtension(fileName);
                var asset = files?.FirstOrDefault(a => a != null && a.name == key);
                if (asset == null) throw new FileNotFoundException($"{owner} 缺少数据文件 {fileName}");
                return asset.text;
            });
            try
            {
                GameData.Load(source);
            }
            catch (DataValidationException ex)
            {
                foreach (var error in ex.Errors) Debug.LogError(error);
                throw;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }
    }
}
