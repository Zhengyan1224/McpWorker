using Zhengyan.ChatUI.CLI;

try
{
    using var app = new CliChatApp();
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
