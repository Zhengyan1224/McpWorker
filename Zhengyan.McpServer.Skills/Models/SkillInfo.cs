namespace Zhengyan.McpServer.Skills.Models;

public class SkillInfo
{
    public string SkillsGroupName { get; set; } = string.Empty;

    public string ID { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EntryFilePath { get; set; } = string.Empty;
}

public class SkillDetail : SkillInfo
{
    public string SkillRootPath { get; set; } = string.Empty;

    public string RelativePathRule { get; set; } = string.Empty;

    public int ContentLength { get; set; }

    public bool Truncated { get; set; }

    public string Content { get; set; } = string.Empty;
}
