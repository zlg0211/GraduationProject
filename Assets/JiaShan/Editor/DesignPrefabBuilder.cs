using System.Collections.Generic;
using System.IO;
using JiaShan.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JiaShan.EditorTools
{
    /// <summary>将 Illustrator 元素数据生成原生 UI 预制体，生成器不参与运行时业务。</summary>
    public static class DesignPrefabBuilder
    {
        private const string Root = "Assets/JiaShan/";
        private static Font _font;

        /// <summary>生成全部页面与启动场景；保留原来的 SampleScene。</summary>
        [MenuItem("家膳智选/生成界面与启动场景")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "Scenes");
            BuildPrefabs();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject canvasObject = new GameObject("家膳智选", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2280);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform viewport = UiFactory.Rect("页面容器", canvasObject.transform, 0, 0, 360, 760);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            canvasObject.AddComponent<AppBootstrap>().Configure(viewport, _font);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, Root + "Scenes/JiaShan.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "Scenes/JiaShan.unity", true) };
            PlayerSettings.productName = "家膳智选";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("家膳智选：26 个可编辑 UI 预制体与启动场景已生成。");
        }

        /// <summary>仅更新页面预制体，不修改当前场景或启动配置。</summary>
        [MenuItem("家膳智选/重新生成页面预制体")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(Root + "Resources/UI");
            _font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/DesignFont.ttf");
            DesignDocument document = JsonUtility.FromJson<DesignDocument>(File.ReadAllText(Root + "Configs/Design.json"));
            foreach (DesignPage design in document.pages) BuildPage(design);
            AssetDatabase.SaveAssets();
        }

        /// <summary>按原稿的绘制顺序重建文字、路径、阴影和照片。</summary>
        private static void BuildPage(DesignPage design)
        {
            RectTransform page = UiFactory.Rect(design.name, null, 0, 0, 360, 760);
            page.gameObject.AddComponent<RectMask2D>();
            page.gameObject.AddComponent<CanvasGroup>();
            UiPageView view = page.gameObject.AddComponent<UiPageView>();
            view.PageId = design.id;
            view.Font = _font;
            RectTransform content = UiFactory.Rect("设计元素", page, 0, 0, 360, 760);
            view.Content = content;
            int index = 0;
            foreach (DesignNode node in design.nodes)
            {
                if (IsInputHint(node, design.controls)) continue;
                if (design.id == 1 && node.kind == "box" && Mathf.Abs(node.y - 669) < 0.1f) continue;
                if (design.id == 3 && node.kind == "box" && Mathf.Abs(node.y - 385) < 0.1f) continue;
                if (design.id == 25 && (node.kind == "box" || node.kind == "circle") && node.x >= 285 && node.y >= 230 && node.y < 371) continue;
                DrawNode(content, node, index++);
            }
            List<UiActionBinding> actions = new List<UiActionBinding>();
            List<InputField> inputs = new List<InputField>();
            foreach (DesignControl control in design.controls)
            {
                if (control.kind == "input")
                    inputs.Add(UiFactory.Input(page, control.text, control.x, control.y, control.w, _font, control.placeholder, control.password));
                else AddAction(actions, page, control.text, control.x, control.y, control.w, control.h);
            }
            if (design.id != 3 && design.id != 4 && design.id != 8 && design.id != 10 && design.id != 15 && design.id != 23)
                AddAction(actions, page, "返回", 22, 54, 44, 44);
            if (design.nav)
            {
                AddAction(actions, page, "推荐", 24, 692, 42, 45, 4);
                AddAction(actions, page, "记录", 95, 692, 42, 45, 3);
                AddAction(actions, page, "拍照", 156, 689, 48, 48, 2);
                AddAction(actions, page, "语音", 223, 692, 42, 45, 8);
                AddAction(actions, page, "我的", 294, 692, 42, 45, 10);
            }
            AddPageControls(design.id, page, actions, inputs);
            if (design.id == 1) AddSlider(page, 24, 669, 312, 9, .86f);
            if (design.id == 3) AddSlider(page, 37, 385, 286, 9, .714f);
            view.Actions = actions.ToArray();
            view.Inputs = inputs.ToArray();
            if (design.id == 12) BuildDataForm(view, new[] { "称呼", "年龄", "食物过敏", "健康与饮食限制", "不吃的食材", "口味偏好" }, 130, 86);
            if (design.id == 14) BuildDataForm(view, new[] { "食物名称", "日期", "餐次", "热量 kcal", "碳水化合物 g", "蛋白质 g", "脂肪 g" }, 125, 76);
            PrefabUtility.SaveAsPrefabAsset(page.gameObject, Root + "Resources/UI/Page" + design.id.ToString("00") + ".prefab");
            Object.DestroyImmediate(page.gameObject);
        }

        /// <summary>把真实业务表单直接保存进预制体，运行时只填数据，不临时搭建字段。</summary>
        private static void BuildDataForm(UiPageView view, string[] labels, float firstY, float spacing)
        {
            UiFactory.Panel(view.transform, "业务输入表单", 0, 116, 360, 557, UiFactory.Background, 0).raycastTarget = true;
            view.Inputs = new InputField[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = firstY + i * spacing;
                float offset = labels.Length == 6 ? 25 : 21;
                UiFactory.Label(view.transform, labels[i], 24, y, 312, 25, 12, _font, UiFactory.Ink, true);
                UiFactory.Panel(view.transform, "输入背景", 22, y + offset, 316, 49, Color.white, 16);
                view.Inputs[i] = UiFactory.Input(view.transform, labels[i], 22, y + offset, 316, _font, "请输入" + labels[i]);
            }
        }

        /// <summary>输入框文字由 InputField 接管，避免与设计占位字叠加。</summary>
        private static bool IsInputHint(DesignNode node, DesignControl[] controls)
        {
            if (node.kind != "text") return false;
            foreach (DesignControl control in controls)
                if (control.kind == "input" && node.text == control.placeholder && node.y > control.y && node.y < control.y + control.h) return true;
            return false;
        }

        private static void DrawNode(Transform parent, DesignNode node, int index)
        {
            if (node.kind == "text")
            {
                float width = node.align == "left" ? 360 - node.x : 350;
                float x = node.align == "center" ? node.x - width / 2 : node.align == "right" ? node.x - width : node.x;
                Text text = UiFactory.Label(parent, node.text, x, node.y - node.size * 1.05f, width, node.size * 1.8f, Mathf.RoundToInt(node.size), _font, UiFactory.Parse(node.fill), node.bold);
                text.alignment = node.align == "center" ? TextAnchor.UpperCenter : node.align == "right" ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                return;
            }
            if (node.kind == "photo")
            {
                RectTransform mask = UiFactory.Rect("菜品图片", parent, node.x, node.y, node.w, node.h);
                DesignShape outline = mask.gameObject.AddComponent<DesignShape>();
                outline.Kind = node.file == "market-fruit.png" ? "box" : "circle";
                outline.raycastTarget = false;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                RawImage image = UiFactory.Rect("照片", mask, 0, 0, node.w, node.h).gameObject.AddComponent<RawImage>();
                image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/" + node.file);
                image.raycastTarget = false;
                if (node.crop) image.uvRect = new Rect(445f / 1024, 1 - 560f / 1536, 260f / 1024, 260f / 1536);
                else if (image.texture != null)
                {
                    float imageAspect = (float)image.texture.width / image.texture.height;
                    float frameAspect = node.w / node.h;
                    image.uvRect = imageAspect > frameAspect ? new Rect((1 - frameAspect / imageAspect) / 2, 0, frameAspect / imageAspect, 1) : new Rect(0, (1 - imageAspect / frameAspect) / 2, 1, imageAspect / frameAspect);
                }
                return;
            }
            RectTransform rect = node.kind == "path" ? UiFactory.Rect("图标路径 " + index, parent, 0, 0, 360, 760) : UiFactory.Rect("图形 " + index, parent, node.x, node.y, node.w, node.h);
            DesignShape shape = rect.gameObject.AddComponent<DesignShape>();
            shape.Kind = node.kind;
            shape.Radius = node.r * 3;
            shape.Fill = UiFactory.Parse(node.fill);
            shape.Stroke = UiFactory.Parse(node.stroke);
            shape.StrokeWidth = node.sw * 3;
            shape.color = new Color(1, 1, 1, node.opacity / 100);
            shape.Closed = node.closed;
            shape.raycastTarget = false;
            if (node.points == null) return;
            shape.Points = new Vector2[node.points.Length];
            for (int i = 0; i < node.points.Length; i++) shape.Points[i] = new Vector2(node.points[i].x * 3, -node.points[i].y * 3);
        }

        private static void AddAction(List<UiActionBinding> actions, Transform page, string name, float x, float y, float w, float h, int target = 0)
            => actions.Add(new UiActionBinding { Action = name, TargetPage = target, Button = UiFactory.HitButton(page, "按钮_" + name, x, y, w, h) });

        /// <summary>为原稿中没有显式按钮框的可点击区域补充组件。</summary>
        private static void AddPageControls(int id, Transform page, List<UiActionBinding> actions, List<InputField> inputs)
        {
            if (id == 1) { AddAction(actions, page, "收藏", 268, 424, 70, 28); AddAction(actions, page, "菜谱选项", 294, 54, 44, 44); }
            if (id == 2) { AddAction(actions, page, "拍摄", 147, 653, 66, 66); AddAction(actions, page, "相册", 57, 666, 42, 42); }
            if (id == 3) AddAction(actions, page, "添加记录", 249, 463, 89, 32, 14);
            if (id == 4) AddAction(actions, page, "换一组推荐", 100, 638, 160, 32);
            if (id == 5) for (int i = 0; i < 6; i++) AddCheck(page, "食材_" + i, 38, 338 + i * 35, i == 1 || i == 2);
            if (id == 6) { AddAction(actions, page, "开始计时", 248, 490, 74, 36); AddAction(actions, page, "语音", 22, 558, 316, 96, 8); AddAction(actions, page, "食材清单", 256, 114, 82, 28, 5); }
            if (id == 8) AddAction(actions, page, "开始说话", 140, 569, 80, 80);
            if (id == 9) AddAction(actions, page, "返回推荐", 90, 692, 180, 36, 4);
            if (id == 10) { AddAction(actions, page, "家庭成员", 22, 325, 316, 47, 11); AddAction(actions, page, "收藏", 22, 372, 316, 47, 13); AddAction(actions, page, "记录", 22, 419, 316, 47, 3); AddAction(actions, page, "屏蔽", 22, 466, 316, 47, 26); AddAction(actions, page, "设置", 292, 54, 46, 44, 25); }
            if (id == 11) { AddAction(actions, page, "新增成员", 22, 594, 316, 50, 19); for (int i = 0; i < 3; i++) AddAction(actions, page, "编辑成员_" + i, 22, 198 + i * 127, 316, 111, 12); }
            if (id == 16) AddAction(actions, page, "立即注册", 210, 578, 80, 35, 17);
            if (id == 17) { AddCheck(page, "同意协议", 24, 518, false); AddAction(actions, page, "返回登录", 95, 640, 170, 35, 16); }
            if (id == 18) for (int i = 0; i < 6; i++) AddAction(actions, page, "人数_" + (i + 1), 22 + i % 3 * 108, 407 + i / 3 * 65, 100, 51);
            if (id == 19) for (int i = 0; i < 3; i++) AddAction(actions, page, "目标_" + new[] { "日常均衡", "控制体重", "增加营养" }[i], 22 + i * 108, 531, 100, 31);
            if (id == 20) { AddAction(actions, page, "过敏原", 141, 293, 134, 28); AddAction(actions, page, "饮食限制", 155, 395, 91, 28); AddAction(actions, page, "忌口", 117, 497, 88, 28); }
            if (id == 20) for (int i = 0; i < 4; i++) AddAction(actions, page, "口味_" + new[] { "清淡", "少辣", "家常", "少油" }[i], 24 + i * 81, 569, 69, 31);
            if (id == 25) for (int i = 0; i < 3; i++) AddSettingSwitch(page, "设置_" + i, 285, 233 + i * 57, i != 1);
        }

        /// <summary>设置开关保持圆形滑块外观，同时使用原生 Toggle 交互。</summary>
        private static void AddSettingSwitch(Transform page, string name, float x, float y, bool selected)
        {
            DesignShape track = UiFactory.Panel(page, name, x, y, 36, 21, selected ? UiFactory.Green : UiFactory.Parse("DEE4D4"), 10.5f);
            track.raycastTarget = true;
            Toggle toggle = track.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = selected;
            DesignShape thumb = UiFactory.Panel(track.transform, "圆形滑块", selected ? 17 : 3, 2.5f, 16, 16, Color.white, 8);
            track.gameObject.AddComponent<SettingToggleView>().Configure(toggle, track, thumb.rectTransform);
        }

        private static void AddCheck(Transform page, string name, float x, float y, bool selected)
        {
            RectTransform root = UiFactory.Rect(name, page, x, y, 18, 18);
            DesignShape background = root.gameObject.AddComponent<DesignShape>();
            background.Radius = 12;
            background.Fill = Color.white;
            background.Stroke = UiFactory.Parse("D8DDCF");
            background.StrokeWidth = 3;
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            DesignShape mark = UiFactory.Panel(root, "选中", 1, 1, 16, 16, UiFactory.Green, 4);
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.isOn = selected;
        }

        private static void AddSlider(Transform page, float x, float y, float width, float height, float value)
        {
            DesignShape track = UiFactory.Panel(page, "营养进度", x, y, width, height, UiFactory.Parse("E7EADF"), height / 2);
            Slider slider = track.gameObject.AddComponent<Slider>();
            DesignShape fill = UiFactory.Panel(track.transform, "填充", 0, 0, width, height, UiFactory.Green, height / 2);
            slider.fillRect = fill.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            slider.interactable = false;
        }
    }
}
