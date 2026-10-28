using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.View.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>
    /// Run 场景的入口（策划/08 §8）：载入 CSV，先显示标题画面（继续 / 新的一局 / 单场战斗），选了之后建 RunScreen。
    /// 存档写在 <c>Application.persistentDataPath/run.json</c>（06 §8）：存档点变化时自动写；退出到标题、关掉游戏时再存一次当前状态；
    /// 一局结束（通关或倒下）删档。战斗中退出，读档后这场战斗从头开始。每次存档顺带写这一局的遥测（TelemetryFiles）和局外档（ProfileFiles）。
    /// 每开一局新的就是新的一夜（策划/12 §3.5）：夜数 +1，地图上先盖一封导师的信。读档继续不算新的一夜。
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
        /// <summary>局外档（第几夜、附言），第一次用到时读盘。</summary>
        public Profile Profile => _profile ??= ProfileFiles.Read();

        Profile _profile;
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
            // 有存档时开新局 = 放弃旧的那局
            var old = RunSaves.Peek(SavePath);
            if (old != null)
            {
                TelemetryFiles.Write(old, abandoned: true);
                Story.Record(Profile, old);
            }
            var run = RunEngine.NewRun(seed, config);
            var postscript = Story.BeginNight(Profile, run.State);
            ProfileFiles.Write(Profile);
            TelemetryFiles.Begin(run.State);
            Save(run);
            var screen = Play(run);
            screen.ShowLetter(Profile, postscript);
            return screen;
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
            _lastInput = Time.unscaledTime;
            return Screen;
        }

        void Save(RunEngine run)
        {
            if (run.IsOver) RunSaves.Delete(SavePath);
            else RunSaves.Write(SavePath, run);
            TelemetryFiles.Write(run.State);
            if (Story.Record(Profile, run.State)) ProfileFiles.Write(Profile);
        }

        void Exit(bool abandon)
        {
            if (abandon)
            {
                // 结算界面的「回到标题」也走这里，局已经结束的不算放弃（RunTelemetry.From 里判断）
                if (Screen != null)
                {
                    TelemetryFiles.Write(Screen.Run.State, abandoned: true);
                    if (Story.Record(Profile, Screen.Run.State)) ProfileFiles.Write(Profile);
                }
                RunSaves.Delete(SavePath);
            }
            else if (Screen != null) Save(Screen.Run);
            ShowTitle();
        }

        void OnApplicationQuit()
        {
            if (Screen != null) Save(Screen.Run);
        }

        // ------------------------------------------------------------------ 计时（遥测的单局用时）

        /// <summary>超过这么久没有鼠标 / 键盘操作就停表：挂机不算进单局用时。</summary>
        const float IdleSeconds = 180;
        float _lastInput;

        void Update()
        {
            if (Screen == null || Screen.Run.IsOver) return;
            var mouse = Mouse.current;
            if ((mouse != null && (mouse.delta.ReadValue() != Vector2.zero || mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.scroll.ReadValue() != Vector2.zero))
                || (Keyboard.current != null && Keyboard.current.anyKey.isPressed))
                _lastInput = Time.unscaledTime;
            // 卡顿、断点、切出窗口回来的一大帧不算
            if (Time.unscaledTime - _lastInput < IdleSeconds) Screen.Run.State.Stats.PlaySeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);
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
            // 第几夜（策划/12 §8.4）：第 2 夜起才显示，深渊青和信上的附言同色
            if (Profile.Nights >= 2)
                Legible(RunUi.Label(root, Story.NightLabel(Profile.Nights), new Vector2(col, 66), new Vector2(800, 40), 28, Palette.Teal)).name = "Night";

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
                ShowCharacterSelect(root);
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

            // 右下角：试玩记录和版本号（写在按钮底色上，主视觉的排线上小字看不清）。试玩的人把记录文件夹发回来，用 Tools 的 telemetry 汇总（策划/07 §6.2）
            RunUi.Button(root, "Telemetry", $"打开试玩记录（{TelemetryFiles.Count} 局）\n<size=75%>版本 {TelemetryFiles.GameVersion}</size>",
                new Vector2(760, -476), new Vector2(360, 80), TelemetryFiles.Reveal, true, null, 22);
        }

        // ------------------------------------------------------------------ 选角（策划/14、09 M6.5）

        /// <summary>选角面板（没在显示时为 null）。盖在标题画面上，按钮名 Pick_{角色 id} / PickBack，测试用 ClickTitle 点。</summary>
        public RectTransform CharacterSelect { get; private set; }

        /// <summary>
        /// 「新的一局」之后先选角色：每个角色一张卷轴，写生命 / 理智上限、特性、起始遗物、起始牌组和一句介绍（策划/14 §0.2）。
        /// 选了就用这个角色开局（种子还是标题画面上那个）；「返回」回到标题。
        /// </summary>
        public void ShowCharacterSelect(RectTransform title)
        {
            if (CharacterSelect != null) Destroy(CharacterSelect.gameObject);
            var layer = Ui.Rect("CharacterSelect", title).Fill();
            CharacterSelect = layer;
            RunUi.Dim(layer, 0.6f);
            RunUi.Title(layer, "选择角色", new Vector2(0, 430), new Vector2(900, 80), 56, Palette.Paper);

            var all = CharacterLibrary.All;
            const float cardW = 620, cardH = 720, gap = 60;
            for (int i = 0; i < all.Count; i++)
            {
                var ch = all[i];
                float x = (i - (all.Count - 1) / 2f) * (cardW + gap);
                CharacterCard(layer, ch, new Vector2(x, -20), new Vector2(cardW, cardH));
            }
            RunUi.Button(layer, "PickBack", "返回", new Vector2(0, -455), new Vector2(240, 64), () =>
            {
                Destroy(layer.gameObject);
                CharacterSelect = null;
            }, true, null, 26);
        }

        void CharacterCard(RectTransform parent, CharacterDef ch, Vector2 pos, Vector2 size)
        {
            var card = RunUi.Panel(parent, $"Character_{ch.Id}", pos, size).rectTransform;
            float top = size.y / 2;

            // 立绘：有角色图用 Stage 着色器的打包模式（和战斗里一样），没有就用剪影；狼人旁边再放一个兽形剪影 / 兽形图
            Portrait(card, PlayerViewArt(ch.Id), SilhouetteKind.Humanoid, new Vector2(ch.Trait == RelicIds.BeastBlood ? -90 : 0, top - 150), 220);
            if (ch.Trait == RelicIds.BeastBlood)
                Portrait(card, ArtDatabase.Actor(Battle.PlayerView.ArtId(ch.Id) + "_beast"), SilhouetteKind.Beast, new Vector2(110, top - 150), 220);

            RunUi.Title(card, ch.Name, new Vector2(0, top - 300), new Vector2(size.x - 60, 60), 46);
            if (!string.IsNullOrEmpty(ch.Title) && ch.Title != ch.Name)
                RunUi.Label(card, ch.Title, new Vector2(0, top - 345), new Vector2(size.x - 60, 34), 26, Palette.InkMid);
            RunUi.Label(card, $"生命 {ch.MaxHp} · 理智上限 {ch.MaxSanity} · ◎ {ch.Gold}", new Vector2(0, top - 385), new Vector2(size.x - 60, 34), 24,
                Palette.Ink).name = "Stats";

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(ch.Trait))
                lines.Add($"<b>特性「{RelicLibrary.Get(ch.Trait).Name}」</b>　{RelicLibrary.Create(ch.Trait).Description}");
            else lines.Add("<b>特性</b>　没有（基准角色）");
            lines.Add($"<b>起始遗物「{RelicLibrary.Get(ch.StartingRelic).Name}」</b>　{RelicLibrary.Create(ch.StartingRelic).Description}");
            lines.Add($"<b>起始牌组</b>　{DeckSummary(ch.Deck)}");
            var body = RunUi.Label(card, string.Join("\n", lines), new Vector2(0, top - 520), new Vector2(size.x - 70, 210), 22, Palette.Ink,
                TextAlignmentOptions.TopLeft);
            body.name = "Body";
            body.lineSpacing = 10;
            body.paragraphSpacing = 12;
            RunUi.Label(card, ch.Text, new Vector2(0, top - 650), new Vector2(size.x - 70, 60), 20, Palette.InkMid).name = "Blurb";

            string id = ch.Id;
            RunUi.Button(card, $"Pick_{id}", $"以{ch.Name}开局", new Vector2(0, -size.y / 2 - 45), new Vector2(340, 70), () =>
            {
                CharacterSelect = null;
                NewRun(NextSeed, new RunConfig { Character = id });
            }, true, null, 30);
        }

        static Texture2D PlayerViewArt(string character) => ArtDatabase.Actor(Battle.PlayerView.ArtId(character));

        /// <summary>选角卡上的立绘：角色图走 Stage 着色器的打包模式；没有图时是墨剪影（10 §10 灰盒）。</summary>
        static void Portrait(RectTransform parent, Texture2D art, SilhouetteKind kind, Vector2 pos, float height)
        {
            var img = Ui.Box(art != null ? "Portrait" : "Silhouette", parent, art != null ? Color.white : Palette.Ink);
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = art != null ? ActorArt.Sprite(art) : Shapes.Silhouette(kind);
            if (art != null)
            {
                var shader = Shader.Find("Deeptide/UI/Stage");
                if (shader != null)
                {
                    var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
                    ActorArt.Configure(mat);
                    img.material = mat;
                    img.gameObject.AddComponent<MaterialOwner>().Material = mat;
                }
            }
            float aspect = img.sprite.rect.width / img.sprite.rect.height;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, new Vector2(Mathf.Min(height * aspect, 260), height));
        }

        /// <summary>「打击 ×4、防御 ×3、獠牙、嚎月、银链」：按出现顺序合并同名牌。</summary>
        static string DeckSummary(IEnumerable<string> deck)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var id in deck)
            {
                if (!counts.ContainsKey(id)) order.Add(id);
                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            return string.Join("、", order.Select(id => CardLibrary.Get(id).Name + (counts[id] > 1 ? $" ×{counts[id]}" : "")));
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
