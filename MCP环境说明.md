# Unity MCP 环境

已配置并验证：

- Unity 2022.3.62f3c1，项目 GraduationProject。
- MCP for Unity 插件与 Python 服务均为 10.0.0。
- Python 3.12.16，由 uv 管理。
- uv 0.13.7。
- 工具目录：`C:/Users/zlg02/.local/bin`，已加入当前用户 PATH。
- MCP 地址：`http://127.0.0.1:8080/mcp`。
- Codex 现有配置：`C:/Users/zlg02/.codex/config.toml` 中的 `mcp_servers.unityMCP`。
- 已启用 Unity MCP 的 Auto-Start on Editor Load。

验证结果：Unity 面板显示 Session Active (GraduationProject)，MCP 可读取 `Assets/Scenes/SampleScene.unity`。

## 后续使用

保持 Unity 工程打开。可从 Window > MCP for Unity > Toggle MCP Window 查看连接。

若尚未连接，先确认 Local Server 已启动，再点击 Connect。自动启动首次弹出插件自己的启动确认时，查看提示后按需要操作。

若 Codex 中暂时没有加载 Unity MCP 工具，完全退出并重新打开 Codex 后再检查。无需重启电脑。

## 手动启动备用方式

在 PowerShell 中运行：

```powershell
& 'C:/Users/zlg02/.local/bin/mcp-for-unity.exe' --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools
```

本次只配置开发工具环境与插件连接，未修改项目场景及业务代码。
