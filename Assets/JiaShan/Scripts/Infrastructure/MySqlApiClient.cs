using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace JiaShan.UI
{
    /// <summary>通过本机接口访问 MySQL，数据库密码不写入 Unity 客户端。</summary>
    public sealed class MySqlApiClient
    {
        private readonly string _baseUrl;
        public MySqlApiClient(string baseUrl) => _baseUrl = baseUrl.TrimEnd('/');

        /// <summary>请求业务接口，网络取消随应用生命周期传入。</summary>
        public async UniTask<T> RequestAsync<T>(string path, string method, object body, string token, CancellationToken cancellationToken)
        {
            using (UnityWebRequest request = new UnityWebRequest(_baseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 12;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                try { await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken); }
                catch (UnityWebRequestException)
                {
                    string message = request.downloadHandler.text;
                    if (request.responseCode == 0) throw new InvalidOperationException("暂时无法连接数据服务，请先启动 MySQL 接口服务。");
                    try { message = JsonConvert.DeserializeObject<ApiError>(message).detail; } catch { }
                    throw new InvalidOperationException(string.IsNullOrEmpty(message) ? "数据请求失败，请稍后重试。" : message);
                }
                return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
            }
        }

        private sealed class ApiError { public string detail; }
    }
}
