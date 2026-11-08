using UnityEngine;
using Cysharp.Threading.Tasks;

namespace JiaShan.UI
{
    /// <summary>仅负责组装导航、会话与控制器依赖。</summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Header("界面依赖")]
        [SerializeField] private RectTransform _pageContainer;
        [SerializeField] private Font _font;
        [Header("数据服务地址")]
        [SerializeField] private string _apiUrl = "http://127.0.0.1:8766";
        private AppController _controller;

        /// <summary>由编辑器生成器填写场景依赖。</summary>
        public void Configure(RectTransform pageContainer, Font font)
        {
            _pageContainer = pageContainer;
            _font = font;
        }

        private void Start()
        {
            _controller = new AppController(_pageContainer, _font, new MySqlApiClient(_apiUrl), this.GetCancellationTokenOnDestroy());
            _controller.Start();
        }

        private void OnDestroy() => _controller?.Dispose();
    }
}
