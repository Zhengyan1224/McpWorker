# 城市体检 AI 总结 Skill

用于把城市体检数据整理成“核心结论”式总结，适用于体检总结、AI 总结、总体分析、核心结论、四维度问题概览等场景。

## Use When

- 用户要求“体检总结”“AI 总结”“核心结论”“总体分析”“先整体看一下”
- 需要同时概括住房、小区（社区）、街区、城区（城市）四个维度
- 你已经拿到或准备获取 `get_dimension_statistics()` 与 `get_region_overview(area_code?)` 的结果

## Required Data

- 必须先通过 `cityagent-query-router` 完成数据查询，不要跳过实体归一化与参数校验
- 至少准备两类结果：
  - `get_dimension_statistics()`：每个维度的问题总量、问题指标数
  - `get_region_overview(area_code?)`：每个维度的重点对象和高频指标编码
- 当需要把 `checkIndicatorCode` 翻译成中文指标名时，再调用 `query_data(query="<指标编码>", sourceFiles="问题指标表.csv", topN=5)`

## Workflow

1. 先通过 `ReadSkillFile(skillId="cityagent-health-summary", relativePath="references/summary-template.md")` 读取模板。
2. 将 `issuesType` 映射为：
   - `1 = 住房`
   - `2 = 小区（社区）`
   - `3 = 街区`
   - `4 = 城区（城市）`
3. 对每个维度分别提取：
   - `statisticsList[0].checkIndicators` -> “发现多少个指标存在问题”
   - `statisticsList[0].questionCount` -> “总计多少个问题”
   - `indicators` 中 `checkIndicatorCount` 最大的编码 -> “问题数量较多的指标”
   - `ownerDetailList` 前 1 到 2 个对象 -> “问题指标项居多的代表对象”
4. 输出时优先使用正式指标全名；如输入中已经明确提供了类似“指标 1*”这类展示写法，可以沿用；否则直接写正式 `indicator_code`。
5. 代表对象按“区 + 街道 + 对象名”拼接，例如“鼓楼区鼓西街道的达明新村”。
6. 四个维度保持一致语气和长度，不要某一段异常冗长。
7. 结尾固定保留“可以继续追问细节 + 地图同步呈现”的引导语。

## Severity Rule

- 只有当该维度 top1 指标的 `checkIndicatorCount` 明显高于同维度其他指标时，才写“程度严重”。
- 如果只是相对靠前但没有明显断层，写“程度较突出”更稳妥。
- 若该维度只返回 1 个指标，也可以写“程度较突出”，不要默认一律写“严重”。

## Hard Rules

- 不要混淆“问题总量”和“问题指标数”。
- 不要把 `ownerDetailList[].checkIndicatorCount` 写成问题总量，它表示该对象涉及的问题指标项数量。
- 不要臆造不存在的指标名称、区街名称、对象名称或排行。
- 某一维度缺少有效数据时，明确写“暂未返回该维度有效数据”，不要补造。
- 最终输出直接给自然语言总结，不要返回 JSON、表格或代码块。
