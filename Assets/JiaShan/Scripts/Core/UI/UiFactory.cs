using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>设计坐标采用 360×760，实际画布采用 1080×2280。</summary>
    public static class UiFactory
    {
        public static readonly Color Ink = Parse("25301E");
        public static readonly Color Green = Parse("9BCB3B");
        public static readonly Color Background = Parse("F6F7F3");

        /// <summary>建立左上角锚定的界面元素。</summary>
        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x * 3, -y * 3);
            rect.sizeDelta = new Vector2(width * 3, height * 3);
            return rect;
        }

        /// <summary>创建文字组件，业务文本可以在运行时单独更新。</summary>
        public static Text Label(Transform parent, string value, float x, float y, float width, float height, int size, Font font, Color color, bool bold = false)
        {
            Text text = Rect(value, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size * 3;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>创建按钮命中区域，保留设计稿自身的独立外观。</summary>
        public static Button HitButton(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject item = Rect(name, parent, x, y, width, height).gameObject;
            Image image = item.AddComponent<Image>();
            image.color = Color.clear;
            Button button = item.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        /// <summary>构建真实输入框，不在视图里保存业务规则。</summary>
        public static InputField Input(Transform parent, string name, float x, float y, float width, Font font, string placeholder, bool password = false)
        {
            GameObject item = Rect(name, parent, x, y, width, 49).gameObject;
            item.AddComponent<Image>().color = Color.clear;
            InputField field = item.AddComponent<InputField>();
            Text label = Label(item.transform, string.Empty, 16, 12, width - 32, 32, 14, font, Ink);
            Text hint = Label(item.transform, placeholder, 16, 12, width - 32, 32, 14, font, Parse("859077"));
            field.textComponent = label;
            field.placeholder = hint;
            field.targetGraphic = item.GetComponent<Image>();
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.characterLimit = 100;
            return field;
        }

        /// <summary>解析设计稿颜色，空字符串表示透明。</summary>
        public static Color Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.clear;
            return ColorUtility.TryParseHtmlString("#" + hex, out Color value) ? value : Color.clear;
        }

        /// <summary>创建圆角底板。</summary>
        public static DesignShape Panel(Transform parent, string name, float x, float y, float width, float height, Color fill, float radius = 20)
        {
            DesignShape shape = Rect(name, parent, x, y, width, height).gameObject.AddComponent<DesignShape>();
            shape.Fill = fill;
            shape.Radius = radius * 3;
            shape.raycastTarget = false;
            return shape;
        }
    }
}
