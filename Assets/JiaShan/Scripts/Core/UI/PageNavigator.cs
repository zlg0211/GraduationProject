using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>管理页面返回栈和联动滑动动画，保留父页面的输入与滚动状态。</summary>
    public sealed class PageNavigator : IDisposable
    {
        private const float Duration = .35f;
        private const float ParallaxRatio = .25f;
        private const float ShadeAlpha = .25f;
        private readonly RectTransform _container;
        private readonly Stack<UiPageView> _history = new Stack<UiPageView>();
        private readonly Action<UiPageView> _onOpened;
        private readonly Action<UiPageView> _onRestored;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private UiPageView _current;
        private Sequence _sequence;
        private bool _isTransitioning;

        /// <summary>当前显示的页面。</summary>
        public UiPageView Current => _current;
        /// <summary>动画期间拒绝重复导航。</summary>
        public bool IsTransitioning => _isTransitioning;

        public PageNavigator(RectTransform container, Action<UiPageView> onOpened, Action<UiPageView> onRestored = null)
        { _container = container; _onOpened = onOpened; _onRestored = onRestored; }

        /// <summary>进入子页面；同级页面和首次信息收集的内部步骤直接切换。</summary>
        public async UniTask OpenAsync(int pageId, bool remember = true)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            UiPageView outgoing = _current;
            try
            {
                UiPageView incoming = CreatePage(pageId);
                if (outgoing == null) return;
                if (remember) _history.Push(outgoing);
                if (remember && ShouldSlide(outgoing.PageId, pageId)) await SlideAsync(outgoing, incoming, false);
                if (remember) outgoing.gameObject.SetActive(false); else Release(outgoing);
            }
            finally { UnlockCurrent(); }
        }

        /// <summary>底部主导航无动画切换，清空子页面返回记录。</summary>
        public async UniTask SwitchTabAsync(int pageId)
        {
            if (_isTransitioning) return;
            ClearHistory();
            await OpenAsync(pageId, false);
        }

        /// <summary>返回父页，播放子页右移、父页复位与遮罩消失的联动动画。</summary>
        public async UniTask BackAsync()
        {
            if (_isTransitioning) return;
            if (_history.Count == 0) { await SwitchTabAsync(4); return; }
            _isTransitioning = true;
            UiPageView outgoing = _current;
            try
            {
                UiPageView incoming = _history.Pop();
                _current = incoming;
                incoming.gameObject.SetActive(true);
                _onRestored?.Invoke(incoming);
                incoming.transform.SetAsLastSibling();
                outgoing.transform.SetAsLastSibling();
                if (ShouldSlide(outgoing.PageId, incoming.PageId)) await SlideAsync(outgoing, incoming, true);
                Release(outgoing);
            }
            finally { UnlockCurrent(); }
        }

        /// <summary>保存后刷新列表再返回，避免返回到过期列表或刚保存的表单。</summary>
        public async UniTask ReturnToAsync(int pageId)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            UiPageView outgoing = _current;
            try
            {
                bool found = false;
                foreach (UiPageView page in _history) if (page.PageId == pageId) { found = true; break; }
                if (found)
                    while (_history.Count > 0)
                    {
                        UiPageView page = _history.Pop();
                        bool target = page.PageId == pageId;
                        Release(page);
                        if (target) break;
                    }
                UiPageView incoming = CreatePage(pageId);
                outgoing.transform.SetAsLastSibling();
                if (ShouldSlide(outgoing.PageId, pageId)) await SlideAsync(outgoing, incoming, true);
                Release(outgoing);
            }
            finally { UnlockCurrent(); }
        }

        /// <summary>登录或首次建档结束后，把当前页向下移出，目标页在其下方显示。</summary>
        public async UniTask DismissDownAsync(int pageId)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            UiPageView outgoing = _current;
            try
            {
                ClearHistory();
                CreatePage(pageId);
                if (outgoing == null) return;
                outgoing.transform.SetAsLastSibling();
                SetInteractive(outgoing, false);
                SetInteractive(_current, false);
                _sequence = DOTween.Sequence().SetUpdate(true);
                _sequence.Join(((RectTransform)outgoing.transform).DOAnchorPosY(-_container.rect.height, Duration).SetEase(Ease.OutCubic));
                await WaitForSequenceAsync();
                Release(outgoing);
            }
            finally { UnlockCurrent(); }
        }

        /// <summary>三个动画共享时间线，所有位移和遮罩曲线都为 OutCubic。</summary>
        private async UniTask SlideAsync(UiPageView outgoing, UiPageView incoming, bool returning)
        {
            float width = _container.rect.width;
            RectTransform outgoingRect = (RectTransform)outgoing.transform;
            RectTransform incomingRect = (RectTransform)incoming.transform;
            Image shade = CreateShade((returning ? incoming : outgoing).transform, returning ? ShadeAlpha : 0);
            SetInteractive(outgoing, false);
            SetInteractive(incoming, false);
            incomingRect.anchoredPosition = new Vector2(returning ? -width * ParallaxRatio : width, 0);
            _sequence = DOTween.Sequence().SetUpdate(true);
            _sequence.Join(outgoingRect.DOAnchorPosX(returning ? width : -width * ParallaxRatio, Duration).SetEase(Ease.OutCubic));
            _sequence.Join(incomingRect.DOAnchorPosX(0, Duration).SetEase(Ease.OutCubic));
            _sequence.Join(shade.DOFade(returning ? 0 : ShadeAlpha, Duration).SetEase(Ease.OutCubic));
            try { await WaitForSequenceAsync(); }
            finally
            {
                if (shade != null)
                {
                    shade.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(shade.gameObject);
                }
            }
            if (outgoingRect != null) outgoingRect.anchoredPosition = Vector2.zero;
        }

        /// <summary>页面透明度始终为 1，黑色遮罩只作用于父页。</summary>
        private UiPageView CreatePage(int pageId)
        {
            UiPageView prefab = Resources.Load<UiPageView>("UI/Page" + pageId.ToString("00"));
            if (prefab == null) throw new InvalidOperationException("找不到页面预制体：" + pageId);
            _current = UnityEngine.Object.Instantiate(prefab, _container, false);
            RectTransform rect = (RectTransform)_current.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _current.GetComponent<CanvasGroup>().alpha = 1;
            _onOpened(_current);
            return _current;
        }

        private static bool ShouldSlide(int from, int to)
            => !(IsMainPage(from) && IsMainPage(to)) && !(IsOnboarding(from) && IsOnboarding(to))
               && !(IsAuthPage(from) && IsAuthPage(to)) && !(from == 17 && to == 18) && !(from == 18 && to == 17);
        private static bool IsMainPage(int page) => page == 2 || page == 3 || page == 4 || page == 8 || page == 10 || page == 23;
        private static bool IsOnboarding(int page) => page >= 18 && page <= 22 || page == 12;
        private static bool IsAuthPage(int page) => page == 16 || page == 17;

        /// <summary>遮罩跟随父页移动，覆盖父页全部内容，不能影响前景子页。</summary>
        private static Image CreateShade(Transform parent, float alpha)
        {
            RectTransform rect = new GameObject("页面亮度遮罩", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.GetComponent<Image>();
            image.color = new Color(0, 0, 0, alpha);
            image.raycastTarget = true;
            return image;
        }

        private async UniTask WaitForSequenceAsync()
            => await UniTask.WaitUntil(() => !_sequence.IsActive() || _sequence.IsComplete(), cancellationToken: _lifetime.Token);

        private static void SetInteractive(UiPageView page, bool enabled)
        {
            if (page != null) page.GetComponent<CanvasGroup>().interactable = enabled;
        }

        private void UnlockCurrent()
        {
            _isTransitioning = false;
            SetInteractive(_current, true);
        }

        private void ClearHistory()
        {
            while (_history.Count > 0) Release(_history.Pop());
        }

        private static void Release(UiPageView page)
        {
            if (page == null) return;
            page.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(page.gameObject);
        }

        /// <summary>停止动画和异步等待，避免退出后收到旧导航回调。</summary>
        public void Dispose()
        {
            _sequence?.Kill();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _history.Clear();
        }
    }
}
