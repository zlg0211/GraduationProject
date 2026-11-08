using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>把原生 Toggle 的状态映射成设计稿中的圆形滑块。</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class SettingToggleView : MonoBehaviour
    {
        [Header("开关外观")]
        [SerializeField] private Toggle _toggle;
        [SerializeField] private DesignShape _track;
        [SerializeField] private RectTransform _thumb;

        /// <summary>由界面生成器配置组件引用。</summary>
        public void Configure(Toggle toggle, DesignShape track, RectTransform thumb)
        { _toggle = toggle; _track = track; _thumb = thumb; Refresh(toggle.isOn); }

        private void OnEnable()
        {
            if (_toggle == null) return;
            _toggle.onValueChanged.AddListener(Refresh);
            Refresh(_toggle.isOn);
        }

        private void OnDisable()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(Refresh);
        }

        private void Refresh(bool selected)
        {
            _track.Fill = selected ? UiFactory.Green : UiFactory.Parse("DEE4D4");
            _track.SetVerticesDirty();
            _thumb.anchoredPosition = new Vector2((selected ? 17 : 3) * 3, -2.5f * 3);
        }
    }
}
