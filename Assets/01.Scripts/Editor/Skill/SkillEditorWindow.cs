using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
        private static readonly Color CastColor = new(0.35f, 0.62f, 0.95f);
        private static readonly Color TargetColor = new(0.95f, 0.62f, 0.25f);
        private static readonly Color EffectColor = new(0.85f, 0.38f, 0.38f);
        private static readonly Color PanelColor = new(1f, 1f, 1f, 0.05f);
        private const string DefaultSkillFolder = "Assets/06.ScriptableObject/Skill"; // '새 스킬' 저장 창의 기본 위치

        private readonly List<SkillDefinition> _skills = new();
        private ListView _list;
        private ScrollView _palette;
        private ScrollView _detail;
        private SkillDefinition _current;
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
            toolbar.Add(new UnityEditor.UIElements.ToolbarButton(RefreshAll) { text = "새로고침" });
            pane.Add(toolbar);

            _list = new ListView
            {
                itemsSource = _skills,
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 6 } },
                bindItem = (element, index) => ((Label)element).text = _skills[index].name,
            };
            _list.style.flexGrow = 1;
            _list.selectionChanged += OnSelectionChanged;
            pane.Add(_list);
            return pane;
        }

        private void RefreshAll()
        {
            var previous = _list.selectedItem as SkillDefinition;

            _skills.Clear();
            _skills.AddRange(FindAssets<SkillDefinition>());
            _list.RefreshItems();

            RefreshPalette();

            if (previous != null && _skills.Contains(previous)) SelectSkill(previous);
            else ShowDetail(null);
        }

        private void SelectSkill(SkillDefinition skill)
        {
            int index = _skills.IndexOf(skill);
            if (index >= 0) _list.SetSelection(index);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            var skill = selection.FirstOrDefault() as SkillDefinition;
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

        // ── 가운데: 부품 보관함 ──────────────────────────────────

        private void RefreshPalette()
        {
            _palette.Clear();
            _palette.Add(Title("부품 보관함", 14));
            _palette.Add(Hint("카드를 오른쪽 슬롯으로 끌어다 놓으세요."));

            AddPaletteSection<CastTimingSO>("축1 · 발동 타이밍", "언제 효과가 터지나?", CastColor);
            AddPaletteSection<TargetSelectorSO>("축2 · 타겟팅", "누구를 맞히나?", TargetColor);
            AddPaletteSection<SkillEffectSO>("축3 · 효과", "맞으면 무슨 일이 생기나?", EffectColor);
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
        private static VisualElement BuildCard(ScriptableObject asset, Color accent, Action onRemove)
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
        private static void MakeDraggable(VisualElement card, Object asset)
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

        private void ShowDetail(SkillDefinition skill)
        {
            _current = skill;
            _detail.Clear();

            if (skill == null)
            {
                _detail.Add(new Label("왼쪽에서 스킬을 고르거나 '새 스킬'을 만드세요."));
                return;
            }

            var so = new SerializedObject(skill);
            _detail.Add(Title(skill.name, 16));

            AddSingleSlot(so, "_castTiming", "축1 · 발동 타이밍", "언제 효과가 터지나?", CastColor, typeof(CastTimingSO));
            AddSingleSlot(so, "_targetSelector", "축2 · 타겟팅", "누구를 맞히나?", TargetColor, typeof(TargetSelectorSO));
            AddEffectSlot(so);

            _detail.Add(new SkillTimelineView(so, skill, _selectedClip, SelectClip));
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

        private void AddEffectSlot(SerializedObject so)
        {
            var prop = so.FindProperty("_effects");

            var slot = BuildSlot("축3 · 효과", "맞으면 무슨 일이 생기나? (여러 개 가능)", EffectColor, typeof(SkillEffectSO), out var content, dropped =>
            {
                so.Update();
                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = dropped;
                so.ApplyModifiedProperties();
                Redraw();
            });

            bool any = false;
            for (int i = 0; i < prop.arraySize; i++)
            {
                int index = i;
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableObject effect) continue;
                any = true;
                content.Add(BuildCard(effect, EffectColor, () =>
                {
                    so.Update();
                    var element = prop.GetArrayElementAtIndex(index);
                    element.objectReferenceValue = null; // 오브젝트 참조 배열은 null로 비운 뒤 삭제해야 칸이 실제로 사라진다
                    prop.DeleteArrayElementAtIndex(index);
                    so.ApplyModifiedProperties();
                    Redraw();
                }));
            }
            if (!any) content.Add(EmptyHint());
            _detail.Add(slot);
        }

        // 슬롯 틀 — 제목/힌트 + 드롭 영역. 맞는 타입만 받는다.
        private static VisualElement BuildSlot(string title, string hint, Color accent, Type accept,
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

        private static Label EmptyHint()
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
