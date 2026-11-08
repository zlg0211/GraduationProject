using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using JiaShan.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.EditorTools
{
    /// <summary>编辑器联调检查：通过真实按钮走注册、家庭档案与饮食记录流程，不参与打包。</summary>
    public static class UiFlowProbe
    {
        public static string Status = "未运行";
        public static string Username = "";

        public static void Run() => RunAsync().Forget();

        private static UiPageView Current => UnityEngine.Object.FindObjectOfType<UiPageView>();

        private static async UniTask WaitPage(int id)
        {
            for (int i = 0; i < 150; i++)
            {
                EditorApplication.isPaused = false;
                if (Current != null && Current.PageId == id) { await UniTask.Delay(400); Status = "页面 " + id; return; }
                await UniTask.Delay(100);
            }
            throw new InvalidOperationException("等待页面超时：" + id + "，当前 " + Current?.PageId);
        }

        private static void Click(string action) => Current.Actions.First(binding => binding.Action == action).Button.onClick.Invoke();

        private static async UniTask RunAsync()
        {
            try
            {
                Username = "ui_check_" + Guid.NewGuid().ToString("N").Substring(0, 10);
                await WaitPage(15);
                Click("创建账户，开启家庭厨房");
                await WaitPage(17);
                Current.Inputs[0].text = Username;
                Current.Inputs[1].text = "TemporaryCheck_1108";
                Current.Inputs[2].text = "TemporaryCheck_1108";
                Current.GetComponentInChildren<Toggle>().isOn = true;
                Click("注册并填写家庭信息");
                await WaitPage(18);
                Click("人数_2");
                await UniTask.Delay(100);
                Click("下一步，填写成员信息");
                for (int i = 0; i < 2; i++)
                {
                    await WaitPage(19);
                    Current.Inputs[0].text = "联调成员" + (i + 1);
                    Current.Inputs[1].text = (24 + i).ToString();
                    Click("目标_控制体重");
                    await UniTask.Delay(100);
                    Click("下一步，填写饮食信息");
                    await WaitPage(20);
                    Click("口味_清淡");
                    await UniTask.Delay(100);
                    Click("保存，填写下一位家人");
                }
                await WaitPage(21);
                Click("确认档案，生成推荐");
                await WaitPage(22);
                Click("看看今天吃什么");
                await WaitPage(4);
                Click("我的");
                await WaitPage(10);
                Click("家庭成员");
                await WaitPage(11);
                Current.GetComponentsInChildren<Button>().First(button => button.name == "编辑成员").onClick.Invoke();
                await WaitPage(12);
                if (Current.Inputs[0].text != "联调成员1") throw new Exception("档案读取失败");
                Current.Inputs[2].text = "花生";
                Click("保存档案");
                await WaitPage(11);
                Click("返回");
                await WaitPage(10);
                Click("记录");
                await WaitPage(3);
                Click("添加记录");
                await WaitPage(14);
                string[] values = { "界面联调记录", DateTime.Today.ToString("yyyy-MM-dd"), "午餐", "320", "30", "25", "10" };
                for (int i = 0; i < values.Length; i++) Current.Inputs[i].text = values[i];
                Click("保存这餐记录");
                await WaitPage(3);
                if (!Current.GetComponentsInChildren<Text>().Any(text => text.text.Contains("界面联调记录"))) throw new Exception("保存后记录未显示");
                Click("我的");
                await WaitPage(10);
                Click("设置");
                await WaitPage(25);
                Toggle setting = Current.GetComponentsInChildren<Toggle>().First();
                bool original = setting.isOn;
                setting.isOn = !original;
                setting.isOn = original;
                Click("退出当前账户");
                await WaitPage(16);
                Current.Inputs[0].text = Username;
                Current.Inputs[1].text = "TemporaryCheck_1108";
                Click("登录");
                await WaitPage(4);
                Click("记录");
                await WaitPage(3);
                if (!Current.GetComponentsInChildren<Text>().Any(text => text.text.Contains("界面联调记录"))) throw new Exception("重新登录后记录未恢复");
                Status = "PASS：注册、2 位成员、目标口味、编辑过敏、返回导航、饮食记录、设置开关与重新登录读取";
            }
            catch (Exception exception) { Status = "FAIL：" + exception.Message; Debug.LogError(Status); }
        }
    }
}
