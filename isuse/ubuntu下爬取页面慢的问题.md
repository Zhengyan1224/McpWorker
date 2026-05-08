在 C# 中，`HttpClient` 默认会优先使用系统配置的 DNS 解析结果（可能同时包含 IPv4 和 IPv6）。如果要强制使用 IPv4，可以通过自定义 `HttpClientHandler` 并修改底层的 `Socket` 连接逻辑。以下是具体实现方法：

---

### **方法 1：通过自定义 `SocketsHttpHandler`（推荐，支持 .NET Core 及以上）**
```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class ForceIPv4Handler : HttpClientHandler
{
    public ForceIPv4Handler()
    {
        // 使用 SocketsHttpHandler 的底层配置（.NET Core 3.0+）
        var innerHandler = new SocketsHttpHandler
        {
            // 自定义连接逻辑
            ConnectCallback = async (context, cancellationToken) =>
            {
                // 解析域名时强制返回 IPv4 地址
                var hostEntry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host);
                var ipv4Address = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4Address == null)
                    throw new Exception("No IPv4 address found for the host.");

                // 创建 Socket 并连接到 IPv4 地址
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(ipv4Address, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        InnerHandler = innerHandler;
    }
}

// 使用示例
var client = new HttpClient(new ForceIPv4Handler());
var response = await client.GetAsync("https://www.nxzfgjj.com");
```

---

### **方法 2：全局修改 DNS 解析行为（系统级）**
如果不想修改代码，可以通过修改系统配置强制优先使用 IPv4：
1. **编辑 `/etc/gai.conf` 文件**（Linux）：
   ```bash
   sudo nano /etc/gai.conf
   ```
2. **取消注释以下行**，使 IPv4 优先级高于 IPv6：
   ```ini
   precedence ::ffff:0:0/96  100
   ```

---

### **方法 3：通过 `ServicePointManager`（仅限 .NET Framework）**
如果使用旧的 .NET Framework（非跨平台），可以通过以下方式强制 IPv4：
```csharp
ServicePointManager.Expect100Continue = true;
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
// 强制所有请求使用 IPv4
ServicePointManager.DefaultConnectionLimit = 10;
ServicePointManager.UseNagleAlgorithm = true;
ServicePointManager.EnableDnsRoundRobin = true;
// 设置 DNS 解析行为（仅对某些旧版本有效）
Dns.GetHostAddressesAsync("www.nxzfgjj.com").ContinueWith(task =>
{
    var ipv4Address = task.Result.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
    if (ipv4Address != null)
    {
        ServicePointManager.FindServicePoint(new Uri("https://www.nxzfgjj.com")).BindIPEndPointDelegate = (servicePoint, remoteEndPoint, retryCount) =>
        {
            return new IPEndPoint(ipv4Address, 0);
        };
    }
});
```

---

### **验证是否生效**
在代码中打印实际连接的 IP 地址：
```csharp
var response = await client.GetAsync("https://www.nxzfgjj.com");
var remoteIp = response.RequestMessage.RequestUri.Host;
Console.WriteLine($"Connected to: {remoteIp}"); // 应显示 IPv4 地址
```

---

### **可能遇到的问题**
1. **兼容性**：方法 1 仅适用于 .NET Core 3.0+，旧版本需使用其他方法。
2. **异常处理**：如果目标域名没有 IPv4 地址，需捕获异常并回退。

通过以上方法，可以强制 `HttpClient` 使用 IPv4 地址访问目标网站，从而绕过潜在的 IPv6 路径延迟问题。