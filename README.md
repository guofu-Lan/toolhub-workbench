# ToolHub Workbench

一个 Windows 本地软件工作台：单个 exe 在本机起一个**只监听 127.0.0.1** 的网页控制台，页面按软件分页，点按钮就真的调用本机工具。

> 本仓库只包含**可开源的部分**：页面、服务骨架、架构文档。
> 授权校验、加密载荷、密钥派生与收费逻辑**不在本仓库**，也不在此说明。

## 它解决什么

Windows 上常用的工具是散的：找文件用 Everything，下载用下载器，开网页用浏览器，代理用 Clash，大文件在网盘。每次都要记路径、找快捷方式。

这个工作台把它们收进一个页面，按用途分页，一个 exe 带起来：

| 分页 | 调用对象 | 页面动作 |
| --- | --- | --- |
| 文件搜索 | Everything | 唤起程序，关键词在程序内输入 |
| 下载 | FluxDown 之类下载器 | 把 http / magnet 链接交给下载器 |
| 浏览器 | Chromium | 打开网址或本地文件 |
| 网络 | Clash Verge | 唤起程序 + 真实外网连通测试 |
| 云盘 | 百度网盘 | 唤起客户端 |
| 飞书资料 | 浏览器 | 粘贴文档、知识库、妙记链接直接打开 |
| 重复比对 | 内置只读脚本 | 出空目录与 MD5 重复组，不删文件 |
| 授权 / 收款 | — | 展示机器信息与收款入口 |
| 本机 | — | 主机名、系统、CPU、内存、网卡、内网 IP |
| 须知 | — | 三步上手、边界、FAQ |

## 技术选型

- **C# / .NET Framework 4.x**，用系统自带的 `csc.exe` 编译，不依赖 SDK、不需要安装运行时。
- UI 是**单文件自包含 HTML**，作为资源编进 exe，不落地明文页面。
- 服务用 `HttpListener`，只绑 `127.0.0.1`，启动生成一次性令牌，请求必须带 `X-Token`。

不选 C/C++ 的原因很实际：授权、HTTP 服务、硬件信息、进程唤起都在同一套 .NET 里已经跑通；C/C++ 要多一套编译链和 COM 封装，收益不明显而风险更高。

## 编译

```
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%CSC% /target:exe /r:System.Management.dll /resource:ui\toolhub.html,ui.html /out:ToolHub.exe src\Workbench.cs
```

## 运行

```
ToolHub.exe                启动服务并打开页面
ToolHub.exe -Port 18800    固定端口
ToolHub.exe -Token abc     固定令牌, 便于排查
```

页面地址形如 `http://127.0.0.1:18800/?t=<令牌>`，去掉令牌参数会返回 403。

## 目录

```
src/Workbench.cs      服务骨架 + 工具表 + 路径解析（脱敏示例）
ui/demo.html          示例页面：文件搜索 / 下载 / 本机 / 须知 四页
docs/ARCHITECTURE.md  设计说明与接口契约
CHANGELOG.md          更新记录
```

> `ui/demo.html` 只是同款视觉的精简示例；正式版的完整分页不在本仓库。<br>
> 编译示例：`csc /target:exe /resource:ui\demo.html,ui.html /out:Demo.exe src\Workbench.cs`

## 设计上踩过的坑（详细见 docs）

1. 子进程中文输出按 GBK，服务端按 UTF-8 解会乱码 → 强制子进程 UTF-8 输出，读回字节严格 UTF-8 解码，失败回退 GBK。
2. 绝对路径写死会让绿色版换机失效 → 统一 `Resolve(rel, abs)`，exe 同目录优先，绝对路径回退。
3. `csc.exe` 只到 C# 5 → `switch` 每支必须 `break`，`Thread` 必须显式 `ThreadStart`。

## 许可

MIT，见 LICENSE。
