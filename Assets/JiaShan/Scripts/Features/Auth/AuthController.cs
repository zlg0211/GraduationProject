using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>注册登录只接受 MySQL 服务验证成功的结果。</summary>
    public sealed class AuthController
    {
        private readonly UserSession _session;
        private readonly MySqlApiClient _api;
        private readonly CancellationToken _token;
        public AuthController(UserSession session, MySqlApiClient api, CancellationToken token) { _session = session; _api = api; _token = token; }

        /// <summary>提交实际账户信息，密码不写入 PlayerPrefs。</summary>
        public async UniTask<int> SubmitAsync(UiPageView view)
        {
            bool registering = view.PageId == 17;
            string username = view.ReadInput(0), password = view.ReadInput(1);
            if (username.Length < 2 || password.Length < 8) throw new InvalidOperationException("用户名至少 2 位，密码至少 8 位。");
            if (registering)
            {
                if (password != view.ReadInput(2)) throw new InvalidOperationException("两次输入的密码不一致。");
                Toggle agreement = view.GetComponentInChildren<Toggle>();
                if (agreement != null && !agreement.isOn) throw new InvalidOperationException("请先勾选用户协议与隐私说明。");
            }
            LoginResponse response = await _api.RequestAsync<LoginResponse>(registering ? "/auth/register" : "/auth/login", "POST", new { username, password }, null, _token);
            _session.Token = response.token;
            _session.Username = response.username;
            _session.Members = response.members ?? new System.Collections.Generic.List<FamilyMemberData>();
            return _session.Members.Count == 0 ? 18 : 4;
        }

        private sealed class LoginResponse
        {
            public string token;
            public string username;
            public System.Collections.Generic.List<FamilyMemberData> members;
        }
    }
}
