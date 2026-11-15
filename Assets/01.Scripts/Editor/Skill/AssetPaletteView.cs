using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MySecondWorld.Combat.EditorTools
{
    // 부품 보관함의 VFX / SFX 탭: 에셋 팩(회사)별로 접이식 그룹을 만들고, 끌어서 클립 칸에 넣는다.
    // 팩을 추가·제거하려면 아래 Packs 배열만 고치면 된다. 존재하지 않는 폴더는 건너뛰고, 결과가 0개인 팩은 숨긴다.
    internal class AssetPaletteView : VisualElement
    {
        public enum Kind { Vfx, Sfx }

        // Label: 그룹 제목 / Vfx: 프리팹을 찾을 폴더 / Sfx: 사운드를 찾을 폴더
        private readonly struct Pack
        {
            public readonly string Label, Vfx, Sfx;
            public Pack(string label, string vfx, string sfx) { Label = label; Vfx = vfx; Sfx = sfx; }
        }

        // Hovl Studio의 Epic toon VFX 1~3은 URP 지원이 유료 별도 에셋이라 프로젝트에서 뺐다(2026-11-15).
        private static readonly Pack[] Packs =
        {
            new("Epic Toon FX",         "Assets/Models/Epic Toon FX",                          "Assets/Models/Epic Toon FX/Sound"),
            new("Hovl · Fantastic Cartoon", "Assets/Models/Hovl Studio/Fantastic cartoon VFX", "Assets/Models/Hovl Studio/Fantastic cartoon VFX"),
            new("Inguz Media Studio",   "Assets/Models/Inguz Media Studio",                    "Assets/Models/Inguz Media Studio"),
            new("JMO Assets",           "Assets/Models/JMO Assets",                            "Assets/Models/JMO Assets"),
            new("Polygon Particle FX",  "Assets/Models/PolygonParticleFX/Prefabs",             "Assets/Models/PolygonParticleFX"),
        };

        private const int MaxShownPerPack = 30; // 한 그룹에서 한 번에 그리는 카드 수. 썸네일 생성 비용을 묶어 둔다
        private const int ThumbSize = 56;

        private readonly Kind _kind;
        private readonly List<(string label, List<string> paths)> _groups = new();
        private readonly HashSet<string> _opened = new(); // 사용자가 펼쳐 둔 그룹
        private readonly VisualElement _results = new();
        private string _filter = "";

        public AssetPaletteView(Kind kind)
        {
            _kind = kind;

            foreach (var pack in Packs)
            {
                string folder = kind == Kind.Vfx ? pack.Vfx : pack.Sfx;
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                var paths = AssetDatabase.FindAssets(kind == Kind.Vfx ? "t:Prefab" : "t:AudioClip", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(Path.GetFileNameWithoutExtension)
                    .ToList();
                if (paths.Count > 0) _groups.Add((pack.Label, paths));
            }

            var search = new TextField { value = "", label = "검색" };
            search.RegisterValueChangedCallback(e => { _filter = e.newValue; Rebuild(); });
            Add(search);

            if (kind == Kind.Sfx)
            {
                var stop = new Button(StopPreview) { text = "■ 정지" };
                stop.style.alignSelf = Align.FlexStart;
                Add(stop);
            }

            Add(_results);
            Rebuild();

            RegisterCallback<DetachFromPanelEvent>(_ => StopPreview());
        }

        private void Rebuild()
        {
            _results.Clear();
            bool searching = !string.IsNullOrWhiteSpace(_filter);

            if (_groups.Count == 0)
            {
                _results.Add(new Label("보여 줄 에셋이 없습니다. AssetPaletteView의 Packs 폴더를 확인하세요."));
                return;
            }

            foreach (var (label, paths) in _groups)
            {
                var matches = searching
                    ? paths.Where(p => Path.GetFileNameWithoutExtension(p).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : paths;
                if (matches.Count == 0) continue;

                var foldout = new Foldout
                {
                    text = $"{label} ({matches.Count})",
                    // 검색 중에는 결과가 보이도록 자동으로 펼친다
                    value = searching || _opened.Contains(label),
                };
                foldout.style.marginTop = 6;
                foldout.Q<Toggle>().style.unityFontStyleAndWeight = FontStyle.Bold;

                var body = new VisualElement();
                foldout.Add(body);
                _results.Add(foldout);

                // 카드(썸네일)는 펼쳐진 그룹만 만든다 — 접힌 그룹은 비용이 들지 않는다.
                void Fill()
                {
                    if (body.childCount > 0) return;
                    foreach (var path in matches.Take(MaxShownPerPack))
                        body.Add(_kind == Kind.Vfx ? BuildVfxCard(path) : BuildSfxCard(path));
                    if (matches.Count > MaxShownPerPack)
                        body.Add(new Label($"…{matches.Count - MaxShownPerPack}개 더 있음 — 검색어로 좁히세요")
                            { style = { fontSize = 11, opacity = 0.6f, marginTop = 4 } });
                }

                if (foldout.value) Fill();
                foldout.RegisterValueChangedCallback(e =>
                {
                    // 검색 중 자동으로 펼쳐진 상태는 사용자의 선택으로 기억하지 않는다
                    if (!searching) { if (e.newValue) _opened.Add(label); else _opened.Remove(label); }
                    if (e.newValue) Fill();
                });
            }
        }

        // ── VFX 카드: 썸네일 + 이름 ───────────────────────────

        private VisualElement BuildVfxCard(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var card = NewCard();

            var thumb = new Image { scaleMode = ScaleMode.ScaleToFit };
            thumb.style.width = ThumbSize;
            thumb.style.height = ThumbSize;
            thumb.style.marginRight = 8;
            thumb.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            card.Add(thumb);
            LoadThumbnailLater(thumb, prefab);

            card.Add(NameBlock(prefab.name, Path.GetDirectoryName(path)?.Replace('\\', '/')));
            SkillEditorWindow.MakeDraggable(card, prefab);
            return card;
        }

        // AssetPreview는 처음엔 null을 돌려주고 뒤에서 만든다 — 준비될 때까지 조금씩 다시 물어본다.
        private static void LoadThumbnailLater(Image thumb, GameObject prefab)
        {
            int tries = 0;
            thumb.schedule.Execute(() =>
            {
                var tex = AssetPreview.GetAssetPreview(prefab);
                if (tex != null) thumb.image = tex;
                tries++;
            }).Every(200).Until(() => thumb.image != null || tries > 25);
        }

        // ── SFX 카드: ▶ + 이름 + 길이 ─────────────────────────

        private VisualElement BuildSfxCard(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var card = NewCard();

            var play = new Button(() => PlayPreview(clip)) { text = "▶" };
            play.style.width = 28;
            play.style.marginRight = 8;
            card.Add(play);

            card.Add(NameBlock(clip.name, $"{clip.length:0.0}초"));
            SkillEditorWindow.MakeDraggable(card, clip);
            return card;
        }

        // ── 공통 ───────────────────────────────────────────────

        private static VisualElement NewCard()
        {
            var card = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            card.style.marginTop = 4;
            card.style.paddingLeft = 6;
            card.style.paddingRight = 6;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 4;
            card.style.backgroundColor = SkillEditorWindow.PanelColor;
            return card;
        }

        private static VisualElement NameBlock(string name, string sub)
        {
            var block = new VisualElement { style = { flexShrink = 1 } };
            block.Add(new Label(name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            var hint = new Label(sub) { style = { fontSize = 10, opacity = 0.6f, whiteSpace = WhiteSpace.Normal } };
            block.Add(hint);
            return block;
        }

        // 에디터 내부(UnityEditor.AudioUtil)의 미리듣기 기능. 공개 API가 아니라 리플렉션으로 호출하고, 못 찾으면 조용히 포기한다.
        private static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");

        internal static void PlayPreview(AudioClip clip)
        {
            StopPreview();
            var method = AudioUtil?.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "PlayPreviewClip");
            if (method == null) { Debug.LogWarning("[SkillTool] 미리듣기 함수를 찾지 못했습니다(Unity 버전 변경 가능성)."); return; }

            var args = method.GetParameters().Length switch
            {
                3 => new object[] { clip, 0, false },
                1 => new object[] { clip },
                _ => null,
            };
            if (args != null) method.Invoke(null, args);
        }

        internal static void StopPreview()
        {
            AudioUtil?.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "StopAllPreviewClips" && m.GetParameters().Length == 0)
                ?.Invoke(null, null);
        }
    }
}
