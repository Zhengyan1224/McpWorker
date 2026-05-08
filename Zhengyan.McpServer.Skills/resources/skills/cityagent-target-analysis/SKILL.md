# 城市更新对象智能分析 Skill

用于分析某个小区、社区、街区、片区或住房对象的具体问题，适用于“分析一下某小区/社区的情况”“详细分析住房情况”“给出风险和改造优先级”等场景。

## Use When

- 用户点名某个对象，要求“分析一下……的情况”“详细说明……住房情况”“展开说说……社区情况”
- 需要输出固定的四段式分析：问题类型、分布特征、潜在风险、改造优先级与统筹建议
- 需要把 cityagent 工具返回的结构化数据转成可读分析，而不是仅列问题清单

## Required Reads

- 先读 `cityagent-query-router`
- 再通过 `ReadSkillFile(skillId="cityagent-target-analysis", relativePath="references/analysis-template.md")` 读取模板
- 需要做指标释义和风险/建议映射时，再读取 `references/indicator-playbook.md`

## Workflow

1. 先判断对象类型：
   - 小区、新村、花园、宿舍、公寓、住宅楼等：按小区对象处理
   - 社区：先从 `小区.csv` 找到该社区下命中的问题小区，再做聚合分析
   - 街区、片区、城区：优先走区划或片区总览；如果拿不到稳定的对象级细项，明确按“区域概览”口径回答
2. 小区对象流程：
   - 用 `query_data(..., sourceFiles="小区.csv")` 归一化对象名
   - 用 `get_target_indicator_statistics(target_type="1", target_name=estate_name)` 获取指标统计
   - 需要补充问题量时，再用 `get_target_issue_totals(target_name=estate_name)`
3. 社区对象流程：
   - 用 `query_data(query="<社区名>", sourceFiles="小区.csv", topN=20)` 找候选
   - 只保留 `community_name` 与用户目标一致或高度一致的记录
   - 对命中的 1 到 5 个问题小区分别调用 `get_target_indicator_statistics(target_type="1", target_name=estate_name)`
   - 先按单个小区去重，再做社区聚合，不要把同一小区同一指标重复累加
4. 指标去重规则：
   - `get_target_indicator_statistics` 常会返回“同一指标 + 不同 issueDesc + 相同 total”的多行
   - 写分析时按 `indicatorCode + indicatorName + secondDimension` 分组
   - 每组使用 `max(total)` 作为该指标数量，不要把多条 `issueDesc` 的 `total` 相加
5. 选择重点问题：
   - 优先写数量最高的 2 到 3 个指标
   - 若同一指标下有多条 `issueDesc`，提炼 1 到 3 个代表性症状，不逐条照抄
6. “分布特征分析”只能基于以下线索：
   - 命中的多个问题小区或楼栋位置
   - `issueDesc` 暗示的集中表现
   - 问题类型之间的共现关系
   - 与指标类型一致的稳妥推断，必须使用“可能”“大概率”“通常与……有关”等保守表述
7. “潜在风险分析”与“改造建议”优先根据 `indicator-playbook` 的对应模式展开；若没有精确命中，就按“安全风险 / 设施短板 / 服务缺口 / 管理缺位”四类做保守归纳。
8. 输出时先给一句已完成提示，再严格按模板四个部分组织。

## Community Handling

- 当前能力更适合“基于社区内命中的问题小区做综合归纳”，不是直接查询独立的社区接口。
- 如果某社区只命中 1 个小区，可以按该小区为主进行分析，并在开头说明“基于当前命中的问题小区数据”。
- 如果无法稳定筛出同一社区的小区，不要假装拿到了社区级精确统计；应明确说明当前只能先分析已命中的具体小区对象。

## Hard Rules

- 不要把同一指标的重复 `total` 相加。
- 不要把推断写成确定事实，尤其是建筑年代、治理机制、人口结构等信息；没有直接依据时必须加保守措辞。
- 不要输出与对象无关的通用空话；每一段都要回扣命中的指标和问题数量。
- 没有足够数据支持时，不要硬写“问题叠加”“空间分布”“扩散效应”等结论。
- 最终输出是结构化自然语言分析，不要返回 JSON 或原始接口字段清单。
