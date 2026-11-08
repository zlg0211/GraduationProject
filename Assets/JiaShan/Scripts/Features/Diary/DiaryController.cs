using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>管理手动饮食记录，营养值由用户填写，不把设计稿数据当成真实摄入。</summary>
    public sealed class DiaryController
    {
        private readonly UserSession _session;
        private readonly MySqlApiClient _api;
        private readonly CancellationToken _token;
        private DateTime _selectedDate = DateTime.Today;

        public DiaryController(UserSession session, MySqlApiClient api, CancellationToken token)
        { _session = session; _api = api; _token = token; }

        /// <summary>刷新当前账户的真实记录。</summary>
        public async UniTask ReloadAsync() => _session.Records = await _api.RequestAsync<List<MealRecordData>>("/diary", "GET", null, _session.Token, _token);

        /// <summary>按当前页面绑定记录列表或输入表单。</summary>
        public void Render(UiPageView view)
        {
            if (view.PageId == 3) RenderHistory(view);
            if (view.PageId == 14) RenderForm(view);
        }

        /// <summary>校验所有营养字段，成功写入数据库后才更新页面。</summary>
        public async UniTask SaveAsync(UiPageView view)
        {
            if (view.ReadInput(0).Length == 0) throw new InvalidOperationException("请填写食物名称。");
            if (!DateTime.TryParseExact(view.ReadInput(1), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) throw new InvalidOperationException("日期格式应为 yyyy-MM-dd，例如 " + DateTime.Today.ToString("yyyy-MM-dd") + "。");
            string meal = view.ReadInput(2);
            if (meal != "早餐" && meal != "午餐" && meal != "晚餐" && meal != "加餐") throw new InvalidOperationException("餐次请填写早餐、午餐、晚餐或加餐。");
            float[] numbers = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(view.ReadInput(i + 3), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || float.IsNaN(numbers[i]) || float.IsInfinity(numbers[i]) || numbers[i] < 0 || numbers[i] > 100000) throw new InvalidOperationException("热量和营养值请填写 0–100000 的数字，不含单位。");
            MealRecordData record = new MealRecordData { food = view.ReadInput(0), date = date.ToString("yyyy-MM-dd"), meal = meal, calories = numbers[0], carbs = numbers[1], protein = numbers[2], fat = numbers[3] };
            await _api.RequestAsync<object>("/diary", "POST", record, _session.Token, _token);
            _selectedDate = date;
            await ReloadAsync();
        }

        private void RenderForm(UiPageView view)
        {
            view.Inputs[1].text = _selectedDate.ToString("yyyy-MM-dd");
            view.Inputs[2].text = "晚餐";
        }

        private void RenderHistory(UiPageView view)
        {
            Transform previous = view.transform.Find("真实饮食记录");
            if (previous != null) { previous.gameObject.SetActive(false); UnityEngine.Object.Destroy(previous.gameObject); }
            DesignShape panel = UiFactory.Panel(view.transform, "真实饮食记录", 0, 112, 360, 574, UiFactory.Background, 0);
            panel.raycastTarget = true;
            UiFactory.Panel(panel.transform, "日历", 22, 0, 316, 133, UiFactory.Parse("DFEFB6"), 23);
            DateTime start = _selectedDate.AddDays(-3);
            UiFactory.Label(panel.transform, _selectedDate.ToString("yyyy 年 M 月"), 38, 16, 210, 30, 18, view.Font, UiFactory.Ink, true);
            for (int i = 0; i < 7; i++)
            {
                DateTime day = start.AddDays(i);
                float x = 33 + i * 42;
                UiFactory.Label(panel.transform, "日一二三四五六"[(int)day.DayOfWeek].ToString(), x, 56, 35, 22, 11, view.Font, UiFactory.Parse("70804E")).alignment = TextAnchor.UpperCenter;
                UiFactory.Panel(panel.transform, "日期", x, 80, 35, 37, i == 3 ? UiFactory.Green : Color.white, 18);
                UiFactory.Label(panel.transform, day.Day.ToString(), x, 89, 35, 26, 12, view.Font, UiFactory.Ink).alignment = TextAnchor.UpperCenter;
                UiFactory.HitButton(panel.transform, "选择日期", x, 80, 35, 37).onClick.AddListener(() => { _selectedDate = day; RenderHistory(view); });
            }
            List<MealRecordData> records = _session.Records.Where(record => record.date == _selectedDate.ToString("yyyy-MM-dd")).ToList();
            UiFactory.Panel(panel.transform, "营养统计", 22, 148, 316, 156, Color.white, 23);
            UiFactory.Label(panel.transform, "当天已记录摄入", 38, 165, 280, 28, 15, view.Font, UiFactory.Ink, true);
            UiFactory.Label(panel.transform, records.Sum(record => record.calories).ToString("0.#") + " kcal", 38, 204, 280, 45, 30, view.Font, UiFactory.Ink, true);
            UiFactory.Label(panel.transform, "碳水 " + records.Sum(record => record.carbs).ToString("0.#") + "g    蛋白质 " + records.Sum(record => record.protein).ToString("0.#") + "g    脂肪 " + records.Sum(record => record.fat).ToString("0.#") + "g", 38, 265, 284, 30, 11, view.Font, UiFactory.Parse("858B7E"));
            UiFactory.Label(panel.transform, "三餐记录", 24, 324, 230, 30, 20, view.Font, UiFactory.Ink, true);
            UiFactory.Label(panel.transform, "仅统计已录入食物，不代表完整摄入", 24, 360, 312, 26, 11, view.Font, UiFactory.Parse("858B7E"));
            RectTransform viewport = UiFactory.Rect("记录窗口", panel.transform, 22, 394, 316, 127);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            RectTransform content = UiFactory.Rect("记录列表", viewport, 0, 0, 316, Mathf.Max(127, records.Count * 72));
            scroll.viewport = viewport; scroll.content = content;
            if (records.Count == 0) UiFactory.Label(content, "这一天还没有记录，记下认真吃的一餐吧", 12, 30, 292, 60, 12, view.Font, UiFactory.Parse("858B7E"));
            for (int i = 0; i < records.Count; i++)
            {
                MealRecordData record = records[i];
                UiFactory.Panel(content, "食物记录", 0, i * 72, 316, 64, Color.white, 17);
                UiFactory.Label(content, record.meal + " · " + record.food, 14, i * 72 + 10, 288, 28, 14, view.Font, UiFactory.Ink, true);
                UiFactory.Label(content, record.calories.ToString("0.#") + " kcal", 14, i * 72 + 39, 288, 23, 11, view.Font, UiFactory.Parse("858B7E"));
            }
            UiFactory.Panel(panel.transform, "添加记录底板", 22, 527, 316, 43, UiFactory.Green, 22);
            UiFactory.Label(panel.transform, "＋ 添加饮食记录", 22, 538, 316, 28, 14, view.Font, UiFactory.Ink, true).alignment = TextAnchor.UpperCenter;
            // 原页面的添加按钮负责路由，新的按钮只转发已有事件，避免重复实现导航。
            Button source = view.Actions.FirstOrDefault(action => action.TargetPage == 14)?.Button;
            UiFactory.HitButton(panel.transform, "添加饮食记录", 22, 527, 316, 43).onClick.AddListener(() => source?.onClick.Invoke());
        }
    }
}
