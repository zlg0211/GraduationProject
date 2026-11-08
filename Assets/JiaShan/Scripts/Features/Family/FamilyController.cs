using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>管理家庭档案的填写草稿、逐位填写与真实数据库保存。</summary>
    public sealed class FamilyController
    {
        private readonly UserSession _session;
        private readonly MySqlApiClient _api;
        private readonly CancellationToken _token;
        private readonly Action<int> _navigate;
        private readonly List<FamilyMemberData> _draft = new List<FamilyMemberData>();
        private int _memberCount = 1;
        private int _currentIndex;
        private int _editingIndex;
        private bool _isAdding;
        private bool _editingDraft;
        private string _goal = "日常均衡";

        public FamilyController(UserSession session, MySqlApiClient api, CancellationToken token, Action<int> navigate)
        { _session = session; _api = api; _token = token; _navigate = navigate; }

        /// <summary>从数据库重新获取本账户家庭成员。</summary>
        public async UniTask ReloadAsync() => _session.Members = await _api.RequestAsync<List<FamilyMemberData>>("/family", "GET", null, _session.Token, _token);

        public void SelectMember(string action) { int.TryParse(action.Substring(action.LastIndexOf('_') + 1), out _editingIndex); _isAdding = false; _editingDraft = false; }
        public void StartAdding() { _isAdding = true; _draft.Clear(); _currentIndex = 0; _memberCount = 1; }

        public bool CanHandle(int page, string action)
            => page == 18 || page == 20 || page == 21 || page == 19 && (action == "下一步，填写饮食信息" || action.StartsWith("目标_")) || page == 12 && action == "保存档案";

        /// <summary>字段校验完成后才推进到下一位成员。</summary>
        public async UniTask<int> HandleAsync(UiPageView view, string action, Func<string, string, UniTask<string>> askText)
        {
            if (view.PageId == 18)
            {
                if (action.StartsWith("人数_"))
                {
                    int.TryParse(action.Substring(3), out _memberCount);
                    if (_memberCount == 6)
                    {
                        string count = await askText("请输入家庭人数（1–12）", "6");
                        if (!int.TryParse(count, out _memberCount) || _memberCount < 1 || _memberCount > 12) throw new InvalidOperationException("家庭人数需为 1–12。");
                    }
                    ChangeText(view, "家庭人数值", _memberCount.ToString());
                    RenderChoices(view, "人数选项", new[] { "1 人", "2 人", "3 人", "4 人", "5 人", "6 人及以上" }, _memberCount > 5 ? 5 : _memberCount - 1, 22, 407, 100, 51, 108, 65, 3);
                    return 0;
                }
                if (!action.StartsWith("下一步")) return 0;
                _draft.Clear(); _currentIndex = 0; _isAdding = false;
                return 19;
            }
            if (view.PageId == 19)
            {
                if (action.StartsWith("目标_"))
                {
                    _goal = action.Substring(3);
                    RenderGoal(view);
                    return 0;
                }
                if (view.ReadInput(0).Length == 0 || !int.TryParse(view.ReadInput(1), out int age) || age < 0 || age > 120) throw new InvalidOperationException("请填写称呼，以及 0–120 岁之间的年龄。");
                FamilyMemberData member = new FamilyMemberData { name = view.ReadInput(0), age = age, goal = _goal };
                if (_draft.Count > _currentIndex) _draft[_currentIndex] = member; else _draft.Add(member);
                return 20;
            }
            if (view.PageId == 20)
            {
                FamilyMemberData member = _draft[_currentIndex];
                if (action == "过敏原") member.allergies = await askText("食物过敏原", member.allergies);
                else if (action == "饮食限制") member.restrictions = await askText("健康与饮食限制", member.restrictions);
                else if (action == "忌口") member.dislikes = await askText("不吃的食材", member.dislikes);
                else if (action.StartsWith("口味_"))
                {
                    List<string> preferences = new List<string>(member.preferences.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    string value = action.Substring(3);
                    if (preferences.Contains(value)) preferences.Remove(value); else preferences.Add(value);
                    member.preferences = string.Join(",", preferences);
                }
                else if (action.StartsWith("保存"))
                {
                    if (++_currentIndex < _memberCount) return 19;
                    if (_isAdding)
                    {
                        _session.Members.AddRange(_draft);
                        await SaveAsync();
                        return 11;
                    }
                    return 21;
                }
                RenderDiet(view, member);
                return 0;
            }
            if (view.PageId == 21 && action == "确认档案，生成推荐")
            {
                _session.Members = new List<FamilyMemberData>(_draft);
                await SaveAsync();
                return 22;
            }
            if (view.PageId == 12)
            {
                if (!int.TryParse(view.ReadInput(1), out int age) || age < 0 || age > 120 || view.ReadInput(0).Length == 0) throw new InvalidOperationException("请检查称呼与年龄。");
                FamilyMemberData member = _editingDraft ? _draft[_editingIndex] : _session.Members[_editingIndex];
                member.name = view.ReadInput(0); member.age = age; member.allergies = view.ReadInput(2); member.restrictions = view.ReadInput(3); member.dislikes = view.ReadInput(4); member.preferences = view.ReadInput(5);
                if (_editingDraft) { _editingDraft = false; return 21; }
                await SaveAsync();
                return 11;
            }
            return 0;
        }

        private UniTask<object> SaveAsync() => _api.RequestAsync<object>("/family", "PUT", new { members = _session.Members }, _session.Token, _token);

        /// <summary>显示实际账户和填写草稿，覆盖设计稿中的示例人物。</summary>
        public void Render(UiPageView view)
        {
            if (view.PageId == 18)
            {
                foreach (Text text in view.Content.GetComponentsInChildren<Text>())
                    if (text.text == "3") { text.name = "家庭人数值"; text.text = _memberCount.ToString(); }
                RenderChoices(view, "人数选项", new[] { "1 人", "2 人", "3 人", "4 人", "5 人", "6 人及以上" }, _memberCount > 5 ? 5 : _memberCount - 1, 22, 407, 100, 51, 108, 65, 3);
            }
            if (view.PageId == 19)
            {
                _goal = _draft.Count > _currentIndex ? _draft[_currentIndex].goal : "日常均衡";
                foreach (Text text in view.Content.GetComponentsInChildren<Text>())
                {
                    if (text.text.StartsWith("成员 1 /")) text.text = "成员 " + (_currentIndex + 1) + " / " + _memberCount;
                    if (text.text == "妈妈 · 待填写" || text.text == "爸爸 · 待填写") text.text = "待填写";
                    if (text.text == "先认识第一位家人") text.text = "认识第 " + (_currentIndex + 1) + " 位家人";
                }
                if (_currentIndex == 0 && !_isAdding) view.Inputs[0].text = "我";
                if (_draft.Count > _currentIndex)
                {
                    view.Inputs[0].text = _draft[_currentIndex].name;
                    view.Inputs[1].text = _draft[_currentIndex].age.ToString();
                }
                UiFactory.Panel(view.transform, "成员填写进度", 22, 253, 316, 43, UiFactory.Background, 0);
                UiFactory.Label(view.transform, "正在填写第 " + (_currentIndex + 1) + " 位，共 " + _memberCount + " 位家人", 24, 266, 312, 28, 13, view.Font, UiFactory.Parse("70804E"));
                RenderGoal(view);
            }
            if (view.PageId == 20 && _draft.Count > _currentIndex) RenderDiet(view, _draft[_currentIndex]);
            if (view.PageId == 11 || view.PageId == 21) RenderMembers(view, view.PageId == 21 ? _draft : _session.Members);
            if (view.PageId == 12 && (_editingDraft ? _draft.Count : _session.Members.Count) > _editingIndex) RenderEditor(view, _editingDraft ? _draft[_editingIndex] : _session.Members[_editingIndex]);
            if (view.PageId == 22)
                foreach (Text text in view.Content.GetComponentsInChildren<Text>()) text.text = text.text.Replace("3 位", _session.Members.Count + " 位");
        }

        private void RenderDiet(UiPageView view, FamilyMemberData member)
        {
            foreach (Text text in view.Content.GetComponentsInChildren<Text>())
            {
                if (text.text == "无已知过敏" || text.name == "实际过敏") { text.name = "实际过敏"; text.text = string.IsNullOrEmpty(member.allergies) ? "未填写" : member.allergies; }
                if (text.text == "暂无特殊限制" || text.name == "实际限制") { text.name = "实际限制"; text.text = string.IsNullOrEmpty(member.restrictions) ? "未填写" : member.restrictions; }
                if (text.text == "香菜  ×" || text.name == "实际忌口") { text.name = "实际忌口"; text.text = string.IsNullOrEmpty(member.dislikes) ? "未填写" : member.dislikes; }
                if (text.text.StartsWith("成员 1 /")) text.text = "成员 " + (_currentIndex + 1) + " / " + _memberCount + " · " + member.name;
            }
            string[] options = { "清淡", "少辣", "家常", "少油" };
            for (int i = 0; i < options.Length; i++)
                RenderChoices(view, "偏好选项" + i, new[] { options[i] }, member.preferences.Contains(options[i]) ? 0 : -1, 24 + i * 81, 569, 69, 31, 81, 0, 1);
            if (_currentIndex == _memberCount - 1)
                foreach (Text text in view.Content.GetComponentsInChildren<Text>()) if (text.text.StartsWith("保存，")) text.text = _isAdding ? "保存家庭成员" : "保存，确认家庭档案";
        }

        private void RenderMembers(UiPageView view, List<FamilyMemberData> members)
        {
            foreach (Text text in view.Content.GetComponentsInChildren<Text>())
                if (text.text.StartsWith("共 3 位")) text.text = "共 " + members.Count + " 位成员 · 点击卡片可返回修改";
            float top = view.PageId == 21 ? 260 : 190;
            DesignShape panel = UiFactory.Panel(view.transform, "真实成员列表", 0, top, 360, view.PageId == 21 ? 420 : 398, UiFactory.Background, 0);
            panel.raycastTarget = true;
            if (members.Count == 0)
            { UiFactory.Label(panel.transform, "还没有家庭成员，点击下方添加", 24, 40, 312, 70, 15, view.Font, UiFactory.Ink); return; }
            RectTransform viewport = UiFactory.Rect("列表窗口", panel.transform, 22, 0, 316, view.PageId == 21 ? 390 : 390);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image input = viewport.gameObject.AddComponent<Image>(); input.color = Color.clear;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            RectTransform content = UiFactory.Rect("成员内容", viewport, 0, 0, 316, Mathf.Max(390, members.Count * 112));
            scroll.content = content; scroll.viewport = viewport;
            for (int i = 0; i < members.Count; i++)
            {
                FamilyMemberData member = members[i]; int memberIndex = i;
                UiFactory.Panel(content, "成员卡片", 0, i * 112, 316, 100, Color.white, 22);
                UiFactory.Label(content, member.name + " · " + member.age + " 岁", 18, i * 112 + 17, 280, 35, 18, view.Font, UiFactory.Ink, true);
                string description = string.IsNullOrEmpty(member.allergies) ? "尚未填写过敏信息" : "过敏：" + member.allergies;
                UiFactory.Label(content, description, 18, i * 112 + 55, 280, 28, 11, view.Font, UiFactory.Parse("858B7E"));
                UiFactory.HitButton(content, "编辑成员", 0, i * 112, 316, 100).onClick.AddListener(() => { _editingIndex = memberIndex; _editingDraft = view.PageId == 21; _navigate(12); });
            }
        }

        private void RenderEditor(UiPageView view, FamilyMemberData member)
        {
            string[] values = { member.name, member.age.ToString(), member.allergies, member.restrictions, member.dislikes, member.preferences };
            for (int i = 0; i < values.Length; i++) view.Inputs[i].text = values[i];
        }

        private static void ChangeText(UiPageView view, string name, string value)
        {
            foreach (Text text in view.GetComponentsInChildren<Text>()) if (text.name == name) text.text = value;
        }

        /// <summary>在原设计位置刷新选项外观，按钮仍由预制体负责命中。</summary>
        private static void RenderChoices(UiPageView view, string name, string[] values, int selected, float x, float y, float width, float height, float stepX, float stepY, int columns)
        {
            Transform old = view.transform.Find(name);
            if (old != null) { old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
            RectTransform root = UiFactory.Rect(name, view.transform, 0, 0, 360, 760);
            for (int i = 0; i < values.Length; i++)
            {
                float left = x + i % columns * stepX, top = y + i / columns * stepY;
                UiFactory.Panel(root, values[i], left, top, width, height, selected == i ? UiFactory.Parse("DFEFB6") : Color.white, 14);
                UiFactory.Label(root, values[i], left, top + height / 2 - 9, width, 26, 13, view.Font, UiFactory.Ink, selected == i).alignment = TextAnchor.UpperCenter;
            }
        }

        private void RenderGoal(UiPageView view)
        {
            string[] goals = { "日常均衡", "控制体重", "增加营养" };
            RenderChoices(view, "目标选项", goals, Array.IndexOf(goals, _goal), 22, 531, 100, 31, 108, 0, 3);
        }
    }
}
