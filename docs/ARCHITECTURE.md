# 架构说明 / Architecture

## 三层结构

```
exe (C# / .NET Framework 4.x)
 ├─ 静态资源: ui.html 作为嵌入资源编入, 不落地明文
 ├─ 服务层: HttpListener, 只绑 127.0.0.1, 一次性令牌
 └─ 进程层: 按需唤起本机工具, 不常驻

浏览器 (Chromium) ── HTTP + X-Token ──> exe
```

## 为什么是“本机网页”而不是桌面 GUI

1. 桌面 GUI 要控件布局、DPI、主题，改一次样式改一堆；
2. 网页一次写完，手机看、宽屏看都自适应；
3. 页面可以随时换成任意前端，不影响服务端契约；
4. 页面不落地成文件，直接进 exe 资源，减少被替换的机会。

## 端口与令牌

- 端口从 18790 起到 18840 递增，取第一个可绑定的；也可 `-Port` 固定。
- 每次启动生成 16 位随机令牌，页面从 `location.search` 的 `t` 取，放进请求头 `X-Token`。
- 服务端先校验令牌，再分发路由；缺失或不匹配一律 403。
- 只绑 `127.0.0.1`，不监听 `0.0.0.0`，外部机器无法访问。

## 接口契约

| 方法 | 路径 | 入参 | 返回 |
| --- | --- | --- | --- |
| GET | `/` | `t` 或 `X-Token` | 内嵌页面 HTML |
| GET | `/api/state` | — | 主机信息 + 工具清单（exists / path） |
| POST | `/api/launch` | `{ tool, url }` | `{ ok, msg }` |
| POST | `/api/compare` | `{ path }` | `{ ok, text }` 只读比对结果 |

令牌错误的统一响应：`403 forbidden`。

## 工具表与路径解析

每个工具有两条路径：

- `Rel`：绿色包内相对路径，如 `tools\Everything\Everything.exe`
- `Abs`：本机已装路径，作回退

解析顺序固定为 **exe 同目录优先 → 绝对路径回退 → 都没有则判缺失**。
页面遇到缺失不改按钮行为，只在 meta 上写“路径缺失”，避免“点了没反应”的假成功。

## 中文编码（重点）

Windows 上最容易翻车的地方。子进程（Windows PowerShell）默认按系统代码页（中文为 GBK/936）输出，父进程若直接按 UTF-8 解，就是一片 `Ŀ��Ŀ¼`。

约定：

1. 拉起子进程时先在命令前置 `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`；
2. 父进程读**字节流**，用严格 UTF-8（`new UTF8Encoding(false, true)`）解码；
3. 严格解码抛异常时回退 `Encoding.GetEncoding(936)`；
4. 自己的输出统一 UTF-8，不做二次转码。

## C# 5 的边界

系统自带 `csc.exe` 只到 C# 5，所以：

- 不用字符串插值 `$"..."`；
- `switch` 每个分支必须 `break`，否则 `CS0163`；
- `new Thread(匿名委托)` 有二义性，必须 `new Thread(new ThreadStart(...))`，否则 `CS0121`；
- 没有 `System.Text.Json`，JSON 自己拼并统一转义。

## 失败即换方法

编译错误按层清：先语法（`CS0xxx`），再语义（类型 / 重载）。同一条命令原样重试没有意义，报错类型不变，结果也不会变。
