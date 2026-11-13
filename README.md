# ToolHub Workbench

![License](https://img.shields.io/badge/license-MIT-3DA639) ![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6) ![Runtime](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4) ![Release](https://img.shields.io/badge/release-v2-C8A356)

把散落在 Windows 上的常用工具收进一个页面。单个 exe 在本机起一个 **只监听 127.0.0.1** 的网页控制台，按软件分页，点按钮就真的调用本机程序。

> 本仓库只放**可开源的部分**：页面、服务骨架、架构文档。
> 授权校验、加密载荷、密钥派生与收费逻辑**不在本仓库**，本文也不涉及。

## 为什么做这个

Windows 上的工具是散的：找文件靠 Everything，下载靠下载器，开网页靠浏览器，代理靠 Clash，大文件在网盘。每换台机器，就要重新记一遍路径。

这个工作台的思路很简单：**一个 exe，一份页面，按用途分页，按钮点下去真的执行**。没有按钮是装饰品，路径缺失就明写“路径缺失”，不假装成功。

## 下载即用

发布页：[Releases · v2](https://github.com/guofu-Lan/toolhub-workbench/releases/tag/v2)

1. 下载 `ToolHub-Green-20261113.zip`（绿色免安装，不写注册表）
2. 校验哈希：

```
certutil -hashfile ToolHub-Green-20261113.zip SHA256
```

期望值：`4803292C6688E04BB1B9C3A7A22F9F19546C29AD08FA1DD6A0002DE804EE1385`

3. 解压到英文路径（如 `D:\ToolHub`），双击 `ToolHub.exe`
4. 程序自动起本地服务，并用 Chromium 打开带令牌的页面

## 功能分区

| 分页 | 调用对象 | 页面动作 |
| --- | --- | --- |
| 文件搜索 | Everything | 唤起程序，关键词在程序内输入 |
| 下载 | FluxDown | 把 http / magnet 链接交给下载器 |
| 浏览器 | Chromium | 打开网址或本地文件 |
| 网络 | Clash Verge | 唤起程序 + 真实外网连通测试 |
| 云盘 | 百度网盘 | 唤起客户端 |
| 飞书资料 | 浏览器 | 文档 / 知识库 / 妙记链接直接打开 |
| 重复比对 | 内置只读脚本 | 出空目录与 MD5 重复组，不删文件 |
| 授权 / 收款 | — | 展示机器信息与收款入口 |
| 本机 | — | 主机名、系统、CPU、内存、网卡、内网 IP |
| 须知 | — | 三步上手、边界、FAQ |

工具未安装时页面显示“路径缺失”；把绿色副本放进 `tools\` 即优先使用。

## 命令行

```
ToolHub.exe -MachineCode          只看机器码
ToolHub.exe -License              看授权与到期
ToolHub.exe -Check                工具自检
ToolHub.exe -Port 18800 -Token t  固定端口与令牌
```

页面地址形如 `http://127.0.0.1:18800/?t=<令牌>`，去掉令牌参数返回 403。

## 技术选型

- **C# / .NET Framework 4.x**，系统自带 `csc.exe` 编译，不依赖 SDK，不装运行时。
- UI 是**单文件自包含 HTML**，作为资源编进 exe，不落地明文页面。
- 服务用 `HttpListener`，只绑 `127.0.0.1`，启动生成一次性令牌，请求必须带 `X-Token`。

不选 C/C++ 的理由很实际：授权、HTTP 服务、硬件信息、进程唤起已在同一套 .NET 里跑通；C/C++ 要多一套编译链和 COM 封装，收益不明显、风险更高。

## 从源码编译

仓库里的示例页是精简版（文件搜索 / 下载 / 本机 / 须知 四页），可直接编译验证骨架：

```
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%CSC% /target:exe /resource:ui\demo.html,ui.html /out:Demo.exe src\Workbench.cs
```

注意：`csc` 的资源参数**不要加引号**，否则报 CS1566。

## 目录结构

```
src/Workbench.cs      服务骨架 + 工具表 + 路径解析（脱敏示例）
ui/demo.html          示例页面：四页
docs/ARCHITECTURE.md  设计说明与接口契约
CHANGELOG.md          更新记录
```

## 安全与隐私

- 服务只绑 `127.0.0.1`，不监听 `0.0.0.0`，同网段其他机器访问不到。
- 每次启动生成一次性令牌，缺失或不匹配一律 403。
- 不代持任何账号密码；飞书、网盘均在官方客户端内登录。
- 重复比对只读，不删除任何文件。
- 本机信息只在本地页面展示，不上传。

## 已知限制

- 自签名证书，首次运行可能被 SmartScreen 拦，选“更多信息 → 仍要运行”。
- 飞书页面内下载视频依赖你自己的登录态，本程序不代持。
- 本机未装 Everything 命令行组件时，关键词需在 Everything 窗口内输入。

## 设计上踩过的坑

1. 子进程中文按 GBK 输出、服务端按 UTF-8 解 → 一片 `Ŀ��Ŀ¼`。改成子进程前置 UTF-8，读回字节严格解码，失败回退 936。
2. 绝对路径写死，绿色版换机就失效 → 统一 `Resolve(rel, abs)`：exe 同目录优先，绝对路径回退。
3. `csc.exe` 只到 C# 5 → `switch` 每支必须 `break`（CS0163），`Thread` 要显式 `new ThreadStart(...)`（CS0121）。

更多细节见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 参与贡献

Issue 与 PR 都欢迎。提 PR 前先开 Issue 对齐方向；代码保持 C# 5 可编译，别引入需要 SDK 的依赖。

## 许可

MIT，见 [LICENSE](LICENSE)。

## 联系作者

- 手机 / Phone：13751335601
- 邮箱 / Email：zhangguofu129@gmail.com
- 购买与授权咨询请注明机器码，便于核对
