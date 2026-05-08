using Renci.SshNet;

public static class SSHTest
{
    public static async Task Run(string[] args)
    {
        /*
        using (var client = new SshClient("127.0.0.1", "zhengyan", "974400763"))
        {
            client.Connect();
            using SshCommand cmd = client.RunCommand("echo 'Hello World!' && ls -l");
            Console.WriteLine(cmd.Result); // "Hello World!\n"
        }
        */


        using (var client = new SshClient("110.80.146.176", "zhengyan", new PrivateKeyFile("/root/.ssh/id_rsa_176","12345")))
        {
            client.Connect();
            using SshCommand cmd = client.RunCommand("echo 'Hello World!' && cd /home/zhengyan && ls -l");
            Console.WriteLine(cmd.Result); // "Hello World!\n"
        }

    }
}