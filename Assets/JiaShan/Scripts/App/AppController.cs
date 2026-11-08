using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>协调模块入口与公共提示，具体业务交给功能控制器。</summary>
    public sealed class AppController : IDisposable
    {
        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly PageNavigator _navigator;
        private readonly UserSession _session = new UserSession();
        private readonly AuthController _auth;
        private readonly FamilyController _family;
        private readonly DiaryController _diary;
        private GameObject _dialog;
        private bool _isHandling;

        public AppController(RectTransform root, Font font, MySqlApiClient api, CancellationToken token)
        {
            _root = root;
            _font = font;
            _navigator = new PageNavigator(root, BindPage, RefreshRestoredPage);
            _auth = new AuthController(_session, api, token);
            _family = new FamilyController(_session, api, token, page => _navigator.OpenAsync(page).Forget());
            _diary = new DiaryController(_session, api, token);
        }

        /// <summary>从欢迎页开始，避免自动使用设计稿中的示例账户。</summary>
        public void Start() => _navigator.OpenAsync(15, false).Forget();

        private void BindPage(UiPageView view)
        {
            foreach (UiActionBinding binding in view.Actions)
            {
                UiActionBinding action = binding;
                action.Button.onClick.AddListener(() => HandleAsync(view, action).Forget());
            }
            _family.Render(view);
            _diary.Render(view);
            if (view.PageId == 4 || view.PageId == 23)
                Empty(view, "今天的菜谱还在等你", "AI 菜谱推荐尚未接入，家庭档案可以正常填写。", 130, 550);
            if (view.PageId == 8) Empty(view, "烹饪时，随时开口提问", "语音助手尚未接入。", 135, 550);
            if (view.PageId == 13) Empty(view, "还没有收藏菜谱", "收藏的菜谱会出现在这里。", 230, 455);
            if (view.PageId == 26) Empty(view, "暂无屏蔽菜谱", "以后可以在菜谱详情中调整推荐。", 200, 460);
            if (view.PageId == 10) RenderProfile(view);
            if (view.PageId == 25) BindSettings(view);
        }

        /// <summary>统一捕获用户操作的错误，防止异常终止演示流程。</summary>
        private async UniTask HandleAsync(UiPageView view, UiActionBinding binding)
        {
            if (view != _navigator.Current || _isHandling) return;
            _isHandling = true;
            try
            {
                string action = binding.Action;
                if (action == "返回") { await _navigator.BackAsync(); return; }
                if (action == "登录" || action == "注册并填写家庭信息")
                {
                    binding.Button.interactable = false;
                    int next = await _auth.SubmitAsync(view);
                    if (view.PageId == 16) await _navigator.DismissDownAsync(next);
                    else await _navigator.OpenAsync(next);
                    return;
                }
                if (_family.CanHandle(view.PageId, action))
                {
                    int next = await _family.HandleAsync(view, action, AskText);
                    if (next == 11 || view.PageId == 12 && next == 21) await _navigator.ReturnToAsync(next);
                    else if (next > 0) await _navigator.OpenAsync(next);
                    return;
                }
                if (view.PageId == 14 && action == "保存这餐记录")
                {
                    await _diary.SaveAsync(view);
                    await _navigator.ReturnToAsync(3);
                    return;
                }
                if (binding.TargetPage > 0)
                {
                    if (binding.TargetPage == 3 || binding.TargetPage == 10) await _diary.ReloadAsync();
                    if (binding.TargetPage == 11) await _family.ReloadAsync();
                    if (view.PageId == 11 && action.StartsWith("编辑成员_")) _family.SelectMember(action);
                    if (action == "新增成员") _family.StartAdding();
                    if (action == "推荐" || action == "记录" || action == "拍照" || action == "语音" || action == "我的")
                        await _navigator.SwitchTabAsync(binding.TargetPage);
                    else await _navigator.OpenAsync(binding.TargetPage);
                    return;
                }
                switch (action)
                {
                    case "创建账户，开启家庭厨房": await _navigator.OpenAsync(17); break;
                    case "已有账户，登录": await _navigator.OpenAsync(16); break;
                    case "看看今天吃什么": await _navigator.DismissDownAsync(4); break;
                    case "退出当前账户": _session.Token = null; _session.Username = null; _session.Members.Clear(); _session.Records.Clear(); await _navigator.SwitchTabAsync(16); break;
                    case "拍摄": case "相册": Notice("拍照选菜暂未接入识别服务，当前不会生成识别结果。"); break;
                    case "开始说话": Notice("语音助手暂未接入。"); break;
                    case "就做这道菜": case "先看菜谱": case "换一组推荐": Notice("AI 菜谱功能尚未接入，当前没有可制作的菜谱。"); break;
                    default: Notice("该入口的界面已准备，业务功能将在后续接入。"); break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Notice(exception.Message); }
            finally { _isHandling = false; if (binding.Button != null) binding.Button.interactable = true; }
        }

        /// <summary>在独立动态层展示空状态，不修改设计预制体。</summary>
        public static void Empty(UiPageView view, string title, string message, float top, float height)
        {
            DesignShape panel = UiFactory.Panel(view.transform, "真实数据空状态", 0, top, 360, height, UiFactory.Background, 0);
            panel.raycastTarget = true;
            UiFactory.Label(panel.transform, title, 24, height * .36f, 312, 65, 23, view.Font, UiFactory.Ink, true).alignment = TextAnchor.UpperCenter;
            Text description = UiFactory.Label(panel.transform, message, 32, height * .36f + 72, 296, 80, 12, view.Font, UiFactory.Parse("858B7E"));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.alignment = TextAnchor.UpperCenter;
        }

        /// <summary>保留父页面状态的同时，刷新子页面可能修改的统计数据，不重复绑定按钮。</summary>
        private void RefreshRestoredPage(UiPageView view)
        {
            if (view.PageId == 10) RenderProfile(view);
        }

        private void RenderProfile(UiPageView view)
        {
            Transform previous = view.transform.Find("账户动态信息");
            if (previous != null) { previous.gameObject.SetActive(false); UnityEngine.Object.Destroy(previous.gameObject); }
            RectTransform dataRoot = UiFactory.Rect("账户动态信息", view.transform, 0, 0, 360, 760);
            foreach (Text text in view.Content.GetComponentsInChildren<Text>())
            {
                if (text.text.StartsWith("家庭档案已完善") || text.text.StartsWith("家庭饮食档案 ·")) text.text = "家庭饮食档案 · " + _session.Members.Count + " 位成员";
                if (text.text == "已保存") text.text = "MySQL";
                if (text.text == "数据备份与恢复") text.text = "当前版本使用本机 MySQL 数据服务";
            }
            UiFactory.Panel(dataRoot, "实际账户信息", 100, 132, 220, 64, UiFactory.Parse("DFEFB6"), 0);
            UiFactory.Label(dataRoot, _session.Username ?? "尚未登录", 110, 139, 210, 38, 21, _font, UiFactory.Ink, true);
            UiFactory.Label(dataRoot, "家膳智选，照顾每一餐", 110, 171, 205, 25, 11, _font, UiFactory.Parse("70804E"));
            UiFactory.Panel(dataRoot, "真实统计", 22, 239, 316, 76, UiFactory.Background, 0);
            string[] values = { _session.Records.Count.ToString(), "0", _session.Members.Count.ToString() };
            string[] names = { "饮食记录", "收藏菜谱", "家庭成员" };
            for (int i = 0; i < 3; i++)
            {
                UiFactory.Label(dataRoot, values[i], 31 + i * 106, 249, 86, 36, 25, _font, UiFactory.Ink, true).alignment = TextAnchor.UpperCenter;
                UiFactory.Label(dataRoot, names[i], 31 + i * 106, 288, 86, 23, 11, _font, UiFactory.Parse("858B7E")).alignment = TextAnchor.UpperCenter;
            }
        }

        private void BindSettings(UiPageView view)
        {
            foreach (Toggle toggle in view.GetComponentsInChildren<Toggle>())
            {
                string key = "JiaShan." + toggle.name;
                toggle.isOn = PlayerPrefs.GetInt(key, toggle.isOn ? 1 : 0) == 1;
                toggle.onValueChanged.AddListener(value => { PlayerPrefs.SetInt(key, value ? 1 : 0); PlayerPrefs.Save(); });
            }
        }

        /// <summary>公共文字输入弹窗，用于过敏原与饮食限制等补充信息。</summary>
        private async UniTask<string> AskText(string title, string initial)
        {
            if (_dialog != null) UnityEngine.Object.Destroy(_dialog);
            RectTransform overlay = UiFactory.Rect("文字输入弹窗", _root, 0, 0, 360, 760);
            _dialog = overlay.gameObject;
            Image shade = overlay.gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .3f);
            UiFactory.Panel(overlay, "内容", 22, 238, 316, 231, Color.white, 25);
            UiFactory.Label(overlay, title, 38, 263, 284, 40, 19, _font, UiFactory.Ink, true);
            UiFactory.Panel(overlay, "输入背景", 38, 318, 284, 49, UiFactory.Background, 15);
            InputField input = UiFactory.Input(overlay, "输入内容", 38, 318, 284, _font, "多个项目用逗号分隔");
            input.text = initial;
            UniTaskCompletionSource<string> completion = new UniTaskCompletionSource<string>();
            MakeDialogButton(overlay, "取消", 38, 396, 126).onClick.AddListener(() => completion.TrySetResult(initial));
            MakeDialogButton(overlay, "确定", 181, 396, 141).onClick.AddListener(() => completion.TrySetResult(input.text.Trim()));
            string result = await completion.Task;
            if (_dialog != null) UnityEngine.Object.Destroy(_dialog);
            return result;
        }

        private Button MakeDialogButton(Transform root, string text, float x, float y, float width)
        {
            UiFactory.Panel(root, text, x, y, width, 44, UiFactory.Ink, 22);
            UiFactory.Label(root, text, x, y + 11, width, 25, 14, _font, Color.white, true).alignment = TextAnchor.UpperCenter;
            return UiFactory.HitButton(root, text, x, y, width, 44);
        }

        /// <summary>所有失败都以可关闭提示展示，不伪造成功结果。</summary>
        private void Notice(string message)
        {
            if (_dialog != null) UnityEngine.Object.Destroy(_dialog);
            RectTransform overlay = UiFactory.Rect("提示", _root, 0, 0, 360, 760);
            _dialog = overlay.gameObject;
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .3f);
            UiFactory.Panel(overlay, "内容", 22, 270, 316, 210, Color.white, 25);
            Text label = UiFactory.Label(overlay, message, 40, 299, 280, 100, 15, _font, UiFactory.Ink);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            MakeDialogButton(overlay, "知道了", 90, 414, 180).onClick.AddListener(() => UnityEngine.Object.Destroy(overlay.gameObject));
        }

        public void Dispose() => _navigator.Dispose();
    }
}
