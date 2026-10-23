using System;
using System.IO;
using Deeptide.Core.Run;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>
    /// Run 场景的入口（策划/08 §8）：载入 CSV，先显示标题画面（继续 / 新的一局 / 单场战斗），选了之后建 RunScreen。
    /// 存档写在 <c>Application.persistentDataPath/run.json</c>（06 §8）：存档点变化时自动写；退出到标题、关掉游戏时再存一次当前状态；
    /// 一局结束（通关或倒下）删档。战斗中退出，读档后这场战斗从头开始。
    /// 主菜单场景还没做，标题画面先放在这里。
    /// </summary>
    public sealed class RunBootstrap : MonoBehaviour
    {
        [Tooltip("Data/ 下的 CSV（cards、enemies、relics、potions、encounters、events），按文件名匹配")]
        public TextAsset[] DataFiles;

        /// <summary>测试用：换一个存档路径，不碰玩家的存档。</summary>
        public static string SavePathOverride;
        public static string SavePath => !string.IsNullOrEmpty(SavePathOverride) ? SavePathOverride : Path.Combine(Application.persistentDataPath, "run.json");

        public RunScreen Screen { get; private set; }
        /// <summary>标题画面（没在显示时为 null）。</summary>
        public RectTransform Title { get; private set; }
        /// <summary>「新的一局」用的种子。</summary>
        public ulong NextSeed { get; set; }

        bool _confirmNew;
        TextMeshProUGUI _hint;

        void Start()
        {
            SceneSetup.EnsureEventSystem();
            SceneSetup.LoadData(DataFiles, nameof(RunBootstrap));
            NextSeed = RandomSeed();
            ShowTitle();
        }

        static ulong RandomSeed() => (ulong)UnityEngine.Random.Range(1, 1000000);

        // ------------------------------------------------------------------ 开局 / 继续

        public RunScreen NewRun(ulong seed, RunConfig config = null)
        {
            var run = RunEngine.NewRun(seed, config);
            Save(run);
            return Play(run);
        }

        /// <summary>读档继续；没有存档或读不了时返回 null（标题画面上提示原因）。</summary>
        public RunScreen Continue()
        {
            if (!File.Exists(SavePath)) return null;
            RunEngine run;
            try
            {
                run = RunSaves.Read(SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (_hint != null) _hint.text = $"存档读不了：{ex.Message}";
                return null;
            }
            return Play(run);
        }

        /// <summary>用一个现成的 RunEngine 开打（读档、测试里改过状态的局）。</summary>
        public RunScreen Play(RunEngine run)
        {
            CloseTitle();
            if (Screen != null) Destroy(Screen.gameObject);
            Screen = RunScreen.Create(Camera.main, run, Save, Exit);
            return Screen;
        }

        static void Save(RunEngine run)
        {
            if (run.IsOver) RunSaves.Delete(SavePath);
            else RunSaves.Write(SavePath, run);
        }

        void Exit(bool abandon)
        {
            if (abandon) RunSaves.Delete(SavePath);
            else if (Screen != null) Save(Screen.Run);
            ShowTitle();
        }

        void OnApplicationQuit()
        {
            if (Screen != null) Save(Screen.Run);
        }

        // ------------------------------------------------------------------ 标题画面

        public void ShowTitle()
        {
            if (Screen != null) Destroy(Screen.gameObject);
            Screen = null;
            CloseTitle();
            _confirmNew = false;

            var root = RunUi.Canvas("Title", Camera.main);
            Title = root;
            Ui.Box("Paper", root, Palette.Paper).rectTransform.Fill();
            // 背景：主视觉（10 §8，栈桥尽头的调查员和雾里的巨物）；没有时用海图最上面的漩涡
            bool keyart = Backdrop(root);
            float col = keyart ? -520 : 0;                                      // 有主视觉时标题和按钮放在左边的雾里
            var logo = Ui.Box("Logo", root, Palette.Ink);
            if (ArtSprites.Apply(logo, "logo", Palette.Ink))
            {
                logo.raycastTarget = false;
                logo.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(col, 280), new Vector2(760, 295));
            }
            else
            {
                Destroy(logo.gameObject);
                var name = RunUi.Title(root, "深潮", new Vector2(col, 250), new Vector2(800, 190), 170);
                name.outlineWidth = 0.15f;
                name.outlineColor = Palette.Paper;
            }
            Legible(RunUi.Label(root, "理智与双面牌 · Demo", new Vector2(col, 112), new Vector2(800, 44), 30, Palette.Ink));

            var save = RunSaves.Peek(SavePath);
            float y = -10;
            if (save != null)
            {
                string where = save.Phase == RunPhase.Battle ? "战斗中，读档后这场重新开始" : save.Row < 0 ? "还没出发" : $"第 {save.Floor} 层";
                RunUi.Button(root, "Continue", $"继续\n<size=55%>{where} · 生命 {save.Hp}/{save.MaxHp} · ◎ {save.Gold}</size>",
                    new Vector2(col, y), new Vector2(460, 100), () => Continue(), true, null, 38);
                y -= 120;
            }
            TextMeshProUGUI newLabel = null;
            var newRun = RunUi.Button(root, "NewRun", "新的一局", new Vector2(col, y), new Vector2(460, 84), () =>
            {
                if (save != null && !_confirmNew)
                {
                    _confirmNew = true;
                    newLabel.text = "再点一次：放弃存档，开新局";
                    newLabel.color = Palette.Blood;
                    return;
                }
                NewRun(NextSeed);
            }, true, null, 36);
            newLabel = newRun.GetComponentInChildren<TextMeshProUGUI>();
            y -= 80;
            TextMeshProUGUI seedLabel = null;
            seedLabel = Legible(RunUi.Label(root, $"种子 {NextSeed}", new Vector2(col - 70, y), new Vector2(300, 40), 24, Palette.Ink, TextAlignmentOptions.Right));
            RunUi.Button(root, "Reseed", "换一个", new Vector2(col + 150, y), new Vector2(140, 48), () =>
            {
                NextSeed = RandomSeed();
                seedLabel.text = $"种子 {NextSeed}";
            }, true, null, 22);
            y -= 90;
            RunUi.Button(root, "Greybox", "单场战斗（灰盒测试）", new Vector2(col, y), new Vector2(460, 70),
                () => SceneManager.LoadScene("Battle_Greybox"), true, null, 26);
            if (!Application.isEditor)
                RunUi.Button(root, "Quit", "退出", new Vector2(col, y - 90), new Vector2(460, 70), Application.Quit, true, null, 26);
            _hint = RunUi.Label(root, "", new Vector2(col, -470), new Vector2(1000, 40), 24, Palette.Blood);
        }

        /// <summary>压在主视觉排线上的小字：加一圈纸色描边。</summary>
        static TextMeshProUGUI Legible(TextMeshProUGUI t)
        {
            t.outlineWidth = 0.3f;
            t.outlineColor = Palette.Paper;
            return t;
        }

        /// <summary>标题画面的背景，返回是不是主视觉。主视觉用 Etching 着清醒面；退而求其次是海图顶上的漩涡。</summary>
        static bool Backdrop(RectTransform root)
        {
            var key = ArtDatabase.Illustration("keyart");
            var etching = Shader.Find("Deeptide/UI/Etching");
            if (key != null && etching != null)
            {
                Cover(root, key, CardArt.For(false), new Rect(0, 0, 1, 1), (float)key.width / key.height, false);
                return true;
            }
            var map = ArtDatabase.Background("map");
            var shader = Shader.Find("Deeptide/UI/Backdrop");
            if (map == null || shader == null) return false;
            var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            mat.SetColor("_InkColor", Palette.InkMid);
            mat.SetColor("_MadColor", Palette.Teal);
            // 海图最上面的 1920 × 1088 正好 16:9
            Cover(root, map, mat, new Rect(0, 1 - 1088f / 3400f, 1, 1088f / 3400f), 1920f / 1088f, true);
            return false;
        }

        /// <summary>铺满屏幕（宽屏时上下裁一点）的一张图。</summary>
        static void Cover(RectTransform root, Texture tex, Material mat, Rect uv, float aspect, bool ownMaterial)
        {
            var art = Ui.Rect("Art", root).Fill().gameObject.AddComponent<RawImage>();
            art.texture = tex;
            art.material = mat;
            art.color = Palette.Paper;
            art.uvRect = uv;
            art.raycastTarget = false;
            if (ownMaterial) art.gameObject.AddComponent<MaterialOwner>().Material = mat;
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = aspect;
        }

        void CloseTitle()
        {
            if (Title != null) Destroy(Title.gameObject);
            Title = null;
            _hint = null;
        }

        /// <summary>点标题画面上名为 name 的按钮（测试用）。</summary>
        public bool ClickTitle(string name)
        {
            if (Title == null) return false;
            foreach (var btn in Title.GetComponentsInChildren<Button>(true))
                if (btn.name == name)
                {
                    btn.onClick.Invoke();
                    return true;
                }
            return false;
        }
    }
}
