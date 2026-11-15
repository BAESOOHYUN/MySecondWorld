using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MySecondWorld.Combat.EditorTools
{
    // 스킬 제작 툴 — [스킬 목록] | [부품 보관함] | [조합 중인 스킬]
    // 보관함의 부품 카드를 오른쪽 슬롯으로 드래그해서 스킬을 조합한다.
    // 슬롯은 SkillDefinition의 private 필드 이름(_castTiming 등)으로 SerializedProperty를 찾으므로,
    // 필드 이름을 바꾸면 여기도 같이 고쳐야 한다.
    public class SkillEditorWindow : EditorWindow
    {
        // 타임라인 보기(SkillTimelineView)도 같은 색과 슬롯 모양을 쓰므로 internal로 둔다.
        internal static readonly Color CastColor = new(0.35f, 0.62f, 0.95f);
        internal static readonly Color TargetColor = new(0.95f, 0.62f, 0.25f);
        internal static readonly Color EffectColor = new(0.85f, 0.38f, 0.38f);
        internal static readonly Color PanelColor = new(1f, 1f, 1f, 0.05f);
        private const string DefaultSkillFolder = "Assets/06.ScriptableObject/Skill"; // '새 스킬' 저장 창의 기본 위치

        internal static readonly Color ComboColor = new(0.65f, 0.45f, 0.9f);

        private readonly List<SlotSkill> _skills = new(); // 단일 스킬과 콤보를 같이 보여 준다
        private ListView _list;
        private ScrollView _palette;
        private ScrollView _detail;
        private SlotSkill _current;
        private bool _paletteIsForCombo; // 보관함이 지금 어느 모드로 그려져 있나(콤보면 스킬 카드, 스킬이면 축1~3 부품)
        private int _selectedClip = -1; // 타임라인에서 선택한 클립 번호

        [MenuItem("Tools/Combat/Skill Editor")]
        private static void Open() => GetWindow<SkillEditorWindow>("Skill Editor");

        private void CreateGUI()
        {
            var outer = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
            outer.Add(BuildListPane());

            var inner = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            _palette = new ScrollView();
            Pad(_palette.contentContainer, 8);
            _detail = new ScrollView();
            Pad(_detail.contentContainer, 12);
            inner.Add(_palette);
            inner.Add(_detail);
            outer.Add(inner);

            rootVisualElement.Add(outer);
            RefreshAll();
        }

        // ── 왼쪽: 스킬 목록 ──────────────────────────────────────

        private VisualElement BuildListPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;

            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.Add(new UnityEditor.UIElements.ToolbarButton(CreateSkill) { text = "새 스킬" });
            toolbar.Add(new UnityEditor.UIElements.ToolbarButton(CreateCombo) { text = "새 콤보" });
            toolbar.Add(new UnityEditor.UIElements.ToolbarButton(RefreshAll) { text = "새로고침" });
            pane.Add(toolbar);

            _list = new ListView
            {
                itemsSource = _skills,
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 6 } },
                bindItem = (element, index) =>
                {
                    var label = (Label)element;
                    bool isCombo = _skills[index] is ComboDefinition;
                    label.text = isCombo ? $"[콤보] {_skills[index].name}" : _skills[index].name;
                    label.style.color = isCombo ? ComboColor : StyleKeyword.Null;
                },
            };
            _list.style.flexGrow = 1;
            _list.selectionChanged += OnSelectionChanged;
            pane.Add(_list);
            return pane;
        }

        private void RefreshAll()
        {
            var previous = _list.selectedItem as SlotSkill;

            _skills.Clear();
            _skills.AddRange(FindAssets<SlotSkill>());
            _list.RefreshItems();

            RefreshPalette(previous is ComboDefinition);

            if (previous != null && _skills.Contains(previous)) SelectSkill(previous);
            else ShowDetail(null);
        }

        private void SelectSkill(SlotSkill skill)
        {
            int index = _skills.IndexOf(skill);
            if (index >= 0) _list.SetSelection(index);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            var skill = selection.FirstOrDefault() as SlotSkill;
            _selectedClip = -1; // 다른 스킬로 넘어가면 클립 선택은 초기화
            ShowDetail(skill);

            // 목록에서 고르면 Project 창/Inspector에서도 해당 SO가 선택된다.
            if (skill != null) Selection.activeObject = skill;
        }

        private void CreateSkill()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 스킬", "Skill_New", "asset", "스킬 에셋을 저장할 위치를 고르세요.", DefaultSkillFolder);
            if (string.IsNullOrEmpty(path)) return;

            var skill = CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.SaveAssets();

            RefreshAll();
            SelectSkill(skill);
        }

        private void CreateCombo()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 콤보", "Combo_New", "asset", "콤보 에셋을 저장할 위치를 고르세요.", DefaultSkillFolder);
            if (string.IsNullOrEmpty(path)) return;

            var combo = CreateInstance<ComboDefinition>();
            AssetDatabase.CreateAsset(combo, path);
            AssetDatabase.SaveAssets();

            RefreshAll();
            SelectSkill(combo);
        }

        // ── 가운데: 부품 보관함 ──────────────────────────────────

        // 스킬을 편집 중이면 축1~3 부품, 콤보를 편집 중이면 단계로 넣을 스킬 카드를 보여 준다.
        private void RefreshPalette(bool forCombo)
        {
            _paletteIsForCombo = forCombo;
            _palette.Clear();

            if (forCombo)
            {
                _palette.Add(Title("스킬 보관함", 14));
                _palette.Add(Hint("스킬 카드를 오른쪽 '단계' 칸으로 끌어다 놓으세요."));
                foreach (var skill in FindAssets<SkillDefinition>())
                    _palette.Add(BuildSkillCard(skill));
                return;
            }

            _palette.Add(Title("부품 보관함", 14));

            // 탭: 축1~3 부품 / VFX / SFX
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            tabs.Add(MakeTab("부품", PaletteTab.Parts));
            tabs.Add(MakeTab("VFX", PaletteTab.Vfx));
            tabs.Add(MakeTab("SFX", PaletteTab.Sfx));
            _palette.Add(tabs);

            switch (_paletteTab)
            {
                case PaletteTab.Vfx:
                    _palette.Add(Hint("프리팹을 타임라인의 VFX 클립 '프리팹' 칸으로 끌어다 놓으세요."));
                    _palette.Add(new AssetPaletteView(AssetPaletteView.Kind.Vfx));
                    break;
                case PaletteTab.Sfx:
                    _palette.Add(Hint("▶로 미리 듣고, SFX 클립 '클립' 칸으로 끌어다 놓으세요."));
                    _palette.Add(new AssetPaletteView(AssetPaletteView.Kind.Sfx));
                    break;
                default:
                    _palette.Add(Hint("카드를 오른쪽 슬롯으로 끌어다 놓으세요."));
                    AddPaletteSection<CastTimingSO>("축1 · 발동 타이밍", "언제 효과가 터지나?", CastColor);
                    AddPaletteSection<TargetSelectorSO>("축2 · 타겟팅", "누구를 맞히나?", TargetColor);
                    AddPaletteSection<SkillEffectSO>("축3 · 효과", "맞으면 무슨 일이 생기나?", EffectColor);
                    break;
            }
        }

        private enum PaletteTab { Parts, Vfx, Sfx }
        private PaletteTab _paletteTab = PaletteTab.Parts;

        private Button MakeTab(string text, PaletteTab tab)
        {
            var button = new Button(() => { _paletteTab = tab; RefreshPalette(false); }) { text = text };
            button.SetEnabled(_paletteTab != tab); // 지금 보는 탭은 눌러도 소용없으니 비활성
            return button;
        }

        // 스킬 카드: 설명 줄 대신 길이를 보여 준다.
        private static VisualElement BuildSkillCard(SkillDefinition skill, Action onRemove = null)
        {
            var card = BuildCard(skill, ComboColor, onRemove);
            if (card.childCount > 1 && card[1] is Label desc)
                desc.text = $"길이 {skill.TotalDuration:0.##}초 · 쿨다운 {skill.Cooldown:0.##}초";
            return card;
        }

        private void AddPaletteSection<T>(string title, string hint, Color accent) where T : ScriptableObject
        {
            var header = Title(title, 13);
            header.style.color = accent;
            header.style.marginTop = 12;
            _palette.Add(header);
            _palette.Add(Hint(hint));

            foreach (var asset in FindAssets<T>())
                _palette.Add(BuildCard(asset, accent, null));
        }

        // 보관함 카드와 슬롯 안 카드를 같은 모양으로 쓴다. onRemove가 있으면 ✕ 버튼이 붙는다.
        internal static VisualElement BuildCard(ScriptableObject asset, Color accent, Action onRemove)
        {
            var card = new VisualElement();
            card.style.marginTop = 4;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 6;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 5;
            card.style.borderLeftWidth = 4;
            card.style.borderLeftColor = accent;
            card.style.backgroundColor = PanelColor;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomRightRadius = 4;

            var top = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            top.Add(new Label(asset.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (onRemove != null)
                top.Add(new Button(onRemove) { text = "✕" });
            card.Add(top);

            var desc = new Label(Describe(asset));
            desc.style.fontSize = 11;
            desc.style.opacity = 0.75f;
            desc.style.whiteSpace = WhiteSpace.Normal;
            card.Add(desc);

            MakeDraggable(card, asset);
            return card;
        }

        private static string Describe(ScriptableObject asset)
        {
            var attr = (SkillPartAttribute)Attribute.GetCustomAttribute(asset.GetType(), typeof(SkillPartAttribute));
            return attr != null ? attr.Description : "(설명 없음 — 클래스에 [SkillPart(\"...\")]를 붙이세요)";
        }

        // 클릭 = 해당 SO 선택, 끌기 = 드래그 시작.
        internal static void MakeDraggable(VisualElement card, Object asset)
        {
            bool pressed = false;
            Vector3 start = default;

            card.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                pressed = true;
                start = e.position;
                card.CapturePointer(e.pointerId);
            });
            card.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pressed || (e.position - start).sqrMagnitude < 16f) return;
                pressed = false;
                card.ReleasePointer(e.pointerId);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { asset };
                DragAndDrop.StartDrag(asset.name);
            });
            card.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!pressed) return;
                pressed = false;
                card.ReleasePointer(e.pointerId);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });
        }

        // ── 오른쪽: 조합 중인 스킬 ───────────────────────────────

        private void ShowDetail(SlotSkill entry)
        {
            _current = entry;
            _detail.Clear();

            // 스킬 ↔ 콤보를 오갈 때만 보관함을 다시 그린다.
            bool forCombo = entry is ComboDefinition;
            if (forCombo != _paletteIsForCombo) RefreshPalette(forCombo);

            if (entry == null)
            {
                _detail.Add(new Label("왼쪽에서 스킬·콤보를 고르거나 '새 스킬' / '새 콤보'를 만드세요."));
                return;
            }

            var so = new SerializedObject(entry);
            _detail.Add(Title(entry.name, 16));

            // 슬롯 UI에 쓰이는 정보(이름·아이콘·쿨다운)
            AddFields(so, "_displayName", "_icon", "_cooldown");

            if (entry is ComboDefinition combo)
            {
                ShowComboDetail(so, combo);
                return;
            }

            // 축2·축3은 타임라인의 효과 클립 안에서 고른다.
            AddSingleSlot(so, "_castTiming", "축1 · 발동 타이밍", "언제 효과가 터지나?", CastColor, typeof(CastTimingSO));

            _detail.Add(new SkillTimelineView(so, (SkillDefinition)entry, _selectedClip, SelectClip));
            _detail.Add(new SkillPreviewView((SkillDefinition)entry));
        }

        private void AddFields(SerializedObject so, params string[] names)
        {
            foreach (var name in names)
            {
                var pf = new PropertyField(so.FindProperty(name));
                pf.Bind(so);
                _detail.Add(pf);
            }
        }

        // ── 콤보 편집 ───────────────────────────────────────────

        private void ShowComboDetail(SerializedObject so, ComboDefinition combo)
        {
            AddFields(so, "_comboWindow", "_minStepInterval");

            var previewNote = new HelpBox(
                "콤보는 미리보기가 없습니다. 각 단계 스킬을 왼쪽 목록에서 따로 고르면 그 스킬의 미리보기를 볼 수 있습니다.",
                HelpBoxMessageType.Info);
            previewNote.style.marginTop = 6;
            _detail.Add(previewNote);

            var steps = so.FindProperty("_steps");
            var slot = BuildSlot("단계 (위에서 아래 순서로 나감)", "스킬 카드를 끌어다 놓으면 맨 뒤에 추가됩니다.",
                ComboColor, typeof(SkillDefinition), out var content, dropped =>
                {
                    so.Update();
                    steps.arraySize++;
                    // 배열을 늘리면 마지막 칸이 복제되므로, 새 칸에 반드시 직접 대입한다.
                    steps.GetArrayElementAtIndex(steps.arraySize - 1).objectReferenceValue = dropped;
                    so.ApplyModifiedProperties();
                    Redraw();
                });

            if (steps.arraySize == 0) content.Add(EmptyHint());

            float total = 0f;
            for (int i = 0; i < steps.arraySize; i++)
            {
                int index = i; // 클로저가 반복 변수가 아닌 이 값을 잡도록
                var skill = steps.GetArrayElementAtIndex(i).objectReferenceValue as SkillDefinition;
                if (skill != null) total += Mathf.Max(skill.TotalDuration, combo.MinStepInterval);

                var row = new VisualElement();
                row.style.marginTop = 4;
                row.Add(new Label($"{i + 1}단계") { style = { fontSize = 11, opacity = 0.7f } });
                row.Add(skill != null
                    ? BuildSkillCard(skill, () => RemoveStep(so, steps, index))
                    : new Label("(비어 있음 — 인스펙터에서 지우거나 스킬을 끌어다 놓으세요)"));

                var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                buttons.Add(new Button(() => MoveStep(so, steps, index, -1)) { text = "▲" });
                buttons.Add(new Button(() => MoveStep(so, steps, index, +1)) { text = "▼" });
                row.Add(buttons);
                content.Add(row);
            }
            _detail.Add(slot);

            if (steps.arraySize > 0)
            {
                var summary = Hint($"단계 {steps.arraySize}개 · 끝까지 나가는 데 약 {total:0.##}초 (이어 누를 시간 {combo.ComboWindow:0.##}초 포함 안 함) " +
                                   $"· 끝난 뒤 콤보 쿨다운 {combo.Cooldown:0.##}초");
                summary.style.marginTop = 8;
                summary.style.whiteSpace = WhiteSpace.Normal;
                _detail.Add(summary);
            }
        }

        private void RemoveStep(SerializedObject so, SerializedProperty steps, int index)
        {
            so.Update();
            // 오브젝트 참조 배열은 먼저 null로 비워야 칸이 실제로 지워진다.
            steps.GetArrayElementAtIndex(index).objectReferenceValue = null;
            steps.DeleteArrayElementAtIndex(index);
            so.ApplyModifiedProperties();
            Redraw();
        }

        private void MoveStep(SerializedObject so, SerializedProperty steps, int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= steps.arraySize) return;
            so.Update();
            steps.MoveArrayElement(index, to);
            so.ApplyModifiedProperties();
            Redraw();
        }

        private void AddSingleSlot(SerializedObject so, string propertyName, string title, string hint, Color accent, Type accept)
        {
            var prop = so.FindProperty(propertyName);

            var slot = BuildSlot(title, hint, accent, accept, out var content, dropped =>
            {
                so.Update();
                prop.objectReferenceValue = dropped;
                so.ApplyModifiedProperties();
                Redraw();
            });

            if (prop.objectReferenceValue is ScriptableObject current)
            {
                content.Add(BuildCard(current, accent, () =>
                {
                    so.Update();
                    prop.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                    Redraw();
                }));
            }
            else
            {
                content.Add(EmptyHint());
            }
            _detail.Add(slot);
        }

        // 슬롯 틀 — 제목/힌트 + 드롭 영역. 맞는 타입만 받는다.
        internal static VisualElement BuildSlot(string title, string hint, Color accent, Type accept,
            out VisualElement content, Action<Object> onDrop)
        {
            var slot = new VisualElement();
            slot.style.marginTop = 12;
            slot.style.paddingLeft = 8;
            slot.style.paddingRight = 8;
            slot.style.paddingTop = 6;
            slot.style.paddingBottom = 8;
            slot.style.backgroundColor = PanelColor;
            SetBorder(slot, 1, accent);
            slot.style.borderTopLeftRadius = 6;
            slot.style.borderTopRightRadius = 6;
            slot.style.borderBottomLeftRadius = 6;
            slot.style.borderBottomRightRadius = 6;

            var header = Title(title, 13);
            header.style.color = accent;
            slot.Add(header);
            slot.Add(Hint(hint));

            content = new VisualElement();
            slot.Add(content);

            slot.RegisterCallback<DragUpdatedEvent>(e =>
            {
                bool ok = DragAndDrop.objectReferences.Any(accept.IsInstanceOfType);
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                slot.style.backgroundColor = ok ? new Color(accent.r, accent.g, accent.b, 0.22f) : PanelColor;
                e.StopPropagation();
            });
            slot.RegisterCallback<DragLeaveEvent>(_ => slot.style.backgroundColor = PanelColor);
            slot.RegisterCallback<DragPerformEvent>(e =>
            {
                slot.style.backgroundColor = PanelColor;
                var dropped = DragAndDrop.objectReferences.FirstOrDefault(accept.IsInstanceOfType);
                if (dropped == null) return;
                DragAndDrop.AcceptDrag();
                onDrop(dropped);
                e.StopPropagation();
            });
            return slot;
        }

        private void SelectClip(int index)
        {
            _selectedClip = index;
            Redraw();
        }

        // 드롭 콜백 안에서 곧바로 화면을 지우면 이벤트 도중에 요소가 사라지므로 한 틱 미룬다.
        // UI Toolkit schedule은 드래그가 끝난 직후 마우스를 움직이기 전까지 돌지 않아 화면이 안 바뀌어 보였다 —
        // 에디터 틱(delayCall)에 올리고 창을 강제로 다시 그린다.
        private void Redraw()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return; // 그 사이 창이 닫혔을 수 있다
                ShowDetail(_current);
                Repaint();
            };
        }

        // ── 작은 도우미 ─────────────────────────────────────────

        private static List<T> FindAssets<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(asset => asset.name)
                .ToList();
        }

        private static Label Title(string text, int size) =>
            new(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = size } };

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.opacity = 0.6f;
            return label;
        }

        internal static Label EmptyHint()
        {
            var label = new Label("여기로 끌어다 놓기");
            label.style.marginTop = 6;
            label.style.opacity = 0.5f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 8;
            label.style.paddingBottom = 8;
            return label;
        }

        private static void Pad(VisualElement element, float amount)
        {
            element.style.paddingLeft = amount;
            element.style.paddingRight = amount;
            element.style.paddingTop = amount;
            element.style.paddingBottom = amount;
        }

        private static void SetBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }
    }
}
