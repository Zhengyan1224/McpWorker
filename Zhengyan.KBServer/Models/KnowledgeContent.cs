namespace Zhengyan.KBServer.Models;


/// <summary>
/// 文本知识内容
/// </summary>
public class KnowledgeContent
{
    /// <summary>
    /// 知识ID
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string>? MetaData { get; set; }

    /// <summary>
    /// 与查询数据的距离（距离越小相似度越高）
    /// </summary>
    public float? Distance { get; set; }
}