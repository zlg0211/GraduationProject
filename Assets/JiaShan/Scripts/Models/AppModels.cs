using System;
using System.Collections.Generic;

namespace JiaShan.UI
{
    /// <summary>当前账户状态。服务端会话令牌只保存在内存。</summary>
    public sealed class UserSession
    {
        public string Token;
        public string Username;
        public List<FamilyMemberData> Members = new List<FamilyMemberData>();
        public List<MealRecordData> Records = new List<MealRecordData>();
    }

    [Serializable]
    public sealed class FamilyMemberData
    {
        public string name = "";
        public int age;
        public string goal = "日常均衡";
        public string allergies = "";
        public string restrictions = "";
        public string dislikes = "";
        public string preferences = "";
    }

    [Serializable]
    public sealed class MealRecordData
    {
        public string food = "";
        public string date = "";
        public string meal = "晚餐";
        public float calories;
        public float carbs;
        public float protein;
        public float fat;
    }
}
