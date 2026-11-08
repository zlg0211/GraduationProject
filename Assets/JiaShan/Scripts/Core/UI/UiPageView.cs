using System;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    [Serializable]
    public sealed class UiActionBinding
    {
        public string Action;
        public int TargetPage;
        public Button Button;
    }

    /// <summary>页面只暴露输入和显示引用，由控制器绑定业务事件。</summary>
    public sealed class UiPageView : MonoBehaviour
    {
        [Header("页面标识")]
        public int PageId;
        [Header("交互引用")]
        public UiActionBinding[] Actions;
        public InputField[] Inputs;
        [Header("显示层")]
        public RectTransform Content;
        public Font Font;

        /// <summary>读取表单字段，避免控制器查找场景层级。</summary>
        public string ReadInput(int index) => Inputs != null && index < Inputs.Length ? Inputs[index].text.Trim() : string.Empty;
    }
}
