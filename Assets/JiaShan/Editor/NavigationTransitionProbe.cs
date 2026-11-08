using System;
using System.Reflection;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using JiaShan.UI;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.EditorTools
{
    /// <summary>编辑器验证真实动画中间值、返回状态和无动画路由，不读写数据库。</summary>
    public static class NavigationTransitionProbe
    {
        public static string Status = "未运行";

        /// <summary>进入 Play 后手动运行，验证结束会清理临时页面。</summary>
        public static void Run() => RunAsync().Forget();

        private static async UniTask RunAsync()
        {
            PageNavigator navigator = null;
            RectTransform root = null;
            try
            {
                Status = "检查子页面进入";
                Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                root = UiFactory.Rect("导航动画验证", canvas.transform, 0, 0, 360, 760);
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                navigator = new PageNavigator(root, _ => { });
                await navigator.OpenAsync(10, false);
                UiPageView parent = navigator.Current;
                UniTask push = navigator.OpenAsync(13);
                Sequence sequence = Halfway(navigator);
                CheckX(parent, -root.rect.width * .21875f);
                CheckX(navigator.Current, root.rect.width * .125f);
                CheckShade(parent, .21875f);
                CheckOpaque(parent); CheckOpaque(navigator.Current);
                await navigator.SwitchTabAsync(8);
                Require(navigator.Current.PageId == 13, "动画期间重复导航未被拦截");
                sequence.Complete(); await push;
                Require(!parent.gameObject.activeSelf, "父页面未隐藏");

                Status = "检查子页面返回";
                UiPageView child = navigator.Current;
                UniTask back = navigator.BackAsync();
                sequence = Halfway(navigator);
                Require(navigator.Current == parent, "返回没有复用父页面");
                CheckX(child, root.rect.width * .875f);
                CheckX(parent, -root.rect.width * .03125f);
                CheckShade(parent, .03125f);
                sequence.Complete(); await back;
                CheckX(parent, 0);

                Status = "检查无动画切换";
                await navigator.SwitchTabAsync(4);
                UniTask tab = navigator.SwitchTabAsync(10);
                Require(!navigator.IsTransitioning && navigator.Current.PageId == 10, "同级切换出现动画");
                await tab;
                await navigator.OpenAsync(18, false);
                UniTask form = navigator.OpenAsync(19);
                Require(!navigator.IsTransitioning && navigator.Current.PageId == 19, "信息填写步骤出现动画");
                await form;
                await navigator.BackAsync();
                Require(navigator.Current.PageId == 18 && !navigator.IsTransitioning, "信息填写返回出现动画");

                Status = "检查建档完成和登录下滑";
                await navigator.OpenAsync(22);
                await CheckDismissAsync(navigator, root);
                await navigator.OpenAsync(16, false);
                await CheckDismissAsync(navigator, root);
                Status = "PASS：进入/返回三项 OutCubic 中间值、页面不透明、父页保留、重复导航拦截、同级及填表无动画、登录与建档下滑";
                Debug.Log(Status);
            }
            catch (Exception exception) { Status = "FAIL：" + Status + "：" + exception.Message; Debug.LogError(Status); }
            finally
            {
                navigator?.Dispose();
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        private static async UniTask CheckDismissAsync(PageNavigator navigator, RectTransform root)
        {
            UiPageView outgoing = navigator.Current;
            UniTask dismiss = navigator.DismissDownAsync(4);
            Sequence sequence = Halfway(navigator);
            Require(Mathf.Abs(((RectTransform)outgoing.transform).anchoredPosition.y + root.rect.height * .875f) < 1, "下滑位移或 OutCubic 曲线错误");
            CheckX(navigator.Current, 0); CheckOpaque(outgoing); CheckOpaque(navigator.Current);
            sequence.Complete(); await dismiss;
            Require(navigator.Current.PageId == 4, "下滑后目标页面错误");
            await navigator.BackAsync();
            Require(navigator.Current.PageId == 4, "登录或建档结束后仍保留返回栈");
        }

        private static Sequence Halfway(PageNavigator navigator)
        {
            Sequence sequence = (Sequence)typeof(PageNavigator).GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(navigator);
            Require(sequence != null && sequence.IsActive(), "没有创建过渡动画");
            Require(Mathf.Abs(sequence.Duration() - .35f) < .001f, "动画时长不是 0.35 秒");
            sequence.Pause();
            sequence.Goto(.175f, false);
            return sequence;
        }

        private static void CheckX(UiPageView page, float expected)
            => Require(Mathf.Abs(((RectTransform)page.transform).anchoredPosition.x - expected) < 1, "横向位移或 OutCubic 曲线错误：" + page.PageId);

        private static void CheckOpaque(UiPageView page)
            => Require(Mathf.Approximately(page.GetComponent<CanvasGroup>().alpha, 1), "页面透明度发生变化");

        private static void CheckShade(UiPageView parent, float expected)
        {
            float actual = parent.GetComponentsInChildren<Image>().First(image => image.name == "页面亮度遮罩").color.a;
            Require(Mathf.Abs(actual - expected) < .001f, "遮罩期望 " + expected + "，实际 " + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
