// Workbench.cs — 脱敏示例：只演示“本机服务 + 分页页面 + 工具唤起 + 路径解析”。
// 不包含授权校验、加密载荷、密钥派生与收费逻辑，这些属于私有实现。
// 目标编译器：C# 5（系统自带 csc.exe），因此不使用字符串插值、不使用现代语法。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ToolHubWorkbench
{
    internal sealed class ToolItem
    {
        public string Key;
        public string Name;
        public string Rel;   // 绿色包内相对路径，如 tools\Everything\Everything.exe
        public string Abs;   // 本机已装路径，作为回退
        public ToolItem(string key, string name, string rel, string abs)
        {
            Key = key; Name = name; Rel = rel; Abs = abs;
        }
    }

    internal static class Program
    {
        private const int PortFrom = 18790;
        private const int PortTo = 18840;
        private static string _token = "";
        private static int _port = 0;
        private static readonly List<ToolItem> Tools = new List<ToolItem>();

        // ---- 路径解析：exe 同目录优先，回退本机已装路径 ----
        private static string Home()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static string Resolve(string rel, string abs)
        {
            string local = Path.Combine(Home(), rel);
            if (rel != null && rel.Length > 0 && File.Exists(local)) return local;
            if (abs != null && abs.Length > 0 && File.Exists(abs)) return abs;
            return "";
        }

        private static void RegisterTools()
        {
            Tools.Add(new ToolItem("everything", "Everything",
                @"tools\Everything\Everything.exe", @"D:\guofu\Everything\Everything.exe"));
            Tools.Add(new ToolItem("fluxdown", "FluxDown",
                @"tools\FluxDown\fluxdown-desktop.exe", @"D:\Programs\FluxDown\fluxdown-desktop.exe"));
            Tools.Add(new ToolItem("browser", "Chromium",
                @"tools\Chromium\chrome.exe", ""));
            Tools.Add(new ToolItem("clash", "Clash Verge",
                @"tools\ClashVerge\clash-verge.exe", @"D:\Program Files\Clash Verge\clash-verge.exe"));
            Tools.Add(new ToolItem("baidu", "BaiduNetdisk",
                @"tools\BaiduNetdisk\BaiduNetdisk.exe", @"E:\Program Files (x86)\BaiduNetdisk\BaiduNetdisk.exe"));
        }

        // ---- JSON 转义（C# 5 没有 System.Text.Json）----
        private static string J(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string StateJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"ok\":true");
            sb.Append(",\"host\":\"").Append(J(Environment.MachineName)).Append("\"");
            sb.Append(",\"os\":\"").Append(J(Environment.OSVersion.ToString())).Append("\"");
            sb.Append(",\"port\":").Append(_port);
            sb.Append(",\"tools\":[");
            for (int i = 0; i < Tools.Count; i++)
            {
                ToolItem t = Tools[i];
                string path = Resolve(t.Rel, t.Abs);
                if (i > 0) sb.Append(',');
                sb.Append("{\"key\":\"").Append(J(t.Key)).Append("\"");
                sb.Append(",\"name\":\"").Append(J(t.Name)).Append("\"");
                sb.Append(",\"exists\":").Append(path.Length > 0 ? "true" : "false");
                sb.Append(",\"path\":\"").Append(J(path)).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string ReadEmbedded(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream st = asm.GetManifestResourceStream(name))
            {
                if (st == null) return "";
                using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static bool TokenOk(HttpListenerRequest req)
        {
            string h = req.Headers["X-Token"];
            if (h != null && h == _token) return true;
            string q = req.QueryString["t"];
            return q != null && q == _token;
        }

        private static void Write(HttpListenerResponse res, int code, string body, string mime)
        {
            byte[] buf = Encoding.UTF8.GetBytes(body ?? "");
            res.StatusCode = code;
            res.ContentType = mime;
            res.ContentLength64 = buf.Length;
            res.OutputStream.Write(buf, 0, buf.Length);
            res.OutputStream.Close();
        }

        private static void Handle(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            try
            {
                string path = req.Url.AbsolutePath;
                if (!TokenOk(req))
                {
                    Write(res, 403, "403 forbidden", "text/plain; charset=utf-8");
                    return;
                }
                if (path == "/" || path == "/index.html")
                {
                    string html = ReadEmbedded("ui.html");
                    if (html.Length == 0) Write(res, 404, "ui not embedded", "text/plain; charset=utf-8");
                    else Write(res, 200, html, "text/html; charset=utf-8");
                    return;
                }
                if (path == "/api/state")
                {
                    Write(res, 200, StateJson(), "application/json; charset=utf-8");
                    return;
                }
                Write(res, 404, "not found", "text/plain; charset=utf-8");
            }
            catch (Exception ex)
            {
                try { Write(res, 500, ex.Message, "text/plain; charset=utf-8"); }
                catch { }
            }
        }

        private static void Serve()
        {
            int start = PortFrom;
            HttpListener listener = null;
            for (int p = start; p <= PortTo; p++)
            {
                try
                {
                    HttpListener l = new HttpListener();
                    l.Prefixes.Add("http://127.0.0.1:" + p + "/");
                    l.Start();
                    listener = l;
                    _port = p;
                    break;
                }
                catch { }
            }
            if (listener == null) throw new Exception("no free port");

            while (true)
            {
                HttpListenerContext ctx = listener.GetContext();
                Thread t = new Thread(new ThreadStart(delegate { Handle(ctx); }));
                t.IsBackground = true;
                t.Start();
            }
        }

        private static string Arg(string[] args, string name, string def)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return def;
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            RegisterTools();

            _token = Arg(args, "-Token", Guid.NewGuid().ToString("N").Substring(0, 16));
            string portArg = Arg(args, "-Port", "");
            if (portArg.Length > 0) _port = int.Parse(portArg);

            Thread server = new Thread(new ThreadStart(Serve));
            server.IsBackground = false;
            server.Start();
            Thread.Sleep(600);

            string url = "http://127.0.0.1:" + _port + "/?t=" + _token;
            Console.WriteLine("ToolHub workbench: " + url);
            try { Process.Start(url); } catch { }

            Console.WriteLine("按回车退出 / press Enter to quit");
            Console.ReadLine();
        }
    }
}
