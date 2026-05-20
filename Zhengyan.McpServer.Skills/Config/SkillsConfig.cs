using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpServer.Skills.Config;

public class SkillsGroupConfig
{
    public string SkillsGroupName { get; set; } = "default";

    public string SkillsRootPath { get; set; } = "./resources/skills";

    public string WorkspaceRootPath { get; set; } = "./";

    public string EntryFileName { get; set; } = "SKILL.md";
}

public class SkillsConfig
{
    public List<SkillsGroupConfig> SkillsGroup { get; set; } = new();

    public string SkillsRootPath { get; set; } = "./resources/skills";

    public string WorkspaceRootPath { get; set; } = "./";

    public string EntryFileName { get; set; } = "SKILL.md";

    public int PreviewLength { get; set; } = 240;

    public int MaxContentLength { get; set; } = 20000;

    public int MaxFileReadLength { get; set; } = 200000;

    public int MaxCommandTimeoutSeconds { get; set; } = 120;

    public int MaxCommandOutputLength { get; set; } = 60000;

    public int MaxListEntries { get; set; } = 5000;

    public int MaxReadLines { get; set; } = 500;

    public int MaxSearchResults { get; set; } = 200;

    public int MaxSearchFiles { get; set; } = 2000;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
    }
}
