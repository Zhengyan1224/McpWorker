using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpServer.Ssh.Config;

public class SshConfig
{
    /// <summary>
    /// SSH连接的主机地址
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// SSH连接的端口号
    /// 默认值为22
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// SSH连接的用户名
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// SSH连接的密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// SSH连接的私钥文件路径
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// SSH连接的私钥密码
    /// </summary>
    public string Passphrase { get; set; }

    /// <summary>
    /// 白名单，多个模式用逗号分隔。每个模式都是一个用于匹配命令的正则表达式。(注意：如果同时指定了白名单和黑名单，系统将首先检查命令是否在白名单中，然后检查它是否在黑名单中。命令必须通过两个检查才能执行。)
    /// </summary>
    public string WhiteList { get; set; }

    /// <summary>
    /// 黑名单，多个模式用逗号分隔。每个模式都是一个用于匹配命令的正则表达式。(注意：如果同时指定了白名单和黑名单，系统将首先检查命令是否在白名单中，然后检查它是否在黑名单中。命令必须通过两个检查才能执行。)
    /// </summary>
    public string BlackList { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

    }
}