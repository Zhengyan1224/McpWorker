# 城市问题查询总路由 Skill

用于 `cityagent_mcp_server` 和 `dataquery_mcp_streamablehttp` 的联动调用。目标是先做实体归一化，再调用业务工具，避免把用户原话直接塞进参数导致查错对象、查错指标或传错编码。

## Use When

- 用户问某个小区、社区、街道、城区的情况
- 用户问某个指标对应哪些对象
- 用户问问题总量、问题列表、指标统计、单条问题详情
- 用户输入里出现口语化简称、别名、模糊指标名
- 用户要求"体检总结""AI 总结""核心结论""总体分析"
- 用户要求"分析一下某个小区/社区/片区的情况"

## 新增查询类型（重要补充）

### 类型1：住房情况查询

当用户问题是"XX的住房情况是怎么样的"类型时：
1. 先提取分析维度：住房情况（包含住房、社区、街区三个分析维度）
2. 提取区划名称（如"鼓楼区"），并解析为 area_code
3. 提取具体名称（达明新村），按 区划-街道-社区-小区 拆解：
   - 若提取名称为小区 → 分析维度为住房
   - 若提取名称为社区 → 分析维度为住房或社区
   - 若提取名称为街道 → 分析维度为街区、社区或住房
4. 调用 `get_indicator_statistics(statistics_type="1", name="<具体名称>")`
   - type=1 表示按住房维度分析
   - name 为归一化后的小区名称（如"达明新村"）

### 类型2：指标统计查询

当用户问题是"XX区去住房结构安全隐患结果分布情况"类型时：
1. 提取区划信息（如"鼓楼区"），转为 area_code
2. 提取指标信息（如"结构安全隐患结果"），通过 `query_data` 归一化为 indicator_code
3. 调用 `get_safe_total(indicatorCode="<指标编码>", areaCode="<区划编码>")`

## Mandatory Order

1. 先判断查询对象是全市、区级、小区级、指标级，还是单条问题。
2. 只要用户提到小区名，就先调用 `query_data(query="<用户原始小区名>", sourceFiles="小区.csv", topN=5)`。
3. 只要后续工具需要 `indicator_code`，就先调用 `query_data(query="<用户原始指标名或问题描述>", sourceFiles="问题指标表.csv", topN=5)`。
4. 后续工具只使用归一化后的 `estate_name`、`district`、`indicator_code`，不要继续使用用户原始叫法。
5. 回答时同时保留“用户叫法 -> 库内真实名”的解释，避免用户误以为查错对象。

## Routing

- 全市或某区总览：`get_region_overview(area_code?)`。如果用户明确问全市，可不传 `area_code`。
- 各分析维度汇总：`get_dimension_statistics()`。
- 体检总体总结：先拿 `get_dimension_statistics()` + `get_region_overview(area_code?)`，再交给 `cityagent-health-summary` 组织最终表述。
- 某对象深入分析：先按对象类型拿到稳定统计结果，再交给 `cityagent-target-analysis` 组织最终表述。
- 某指标涉及哪些对象：先解析 `indicator_code`，再调用 `list_indicator_targets(indicator_code, area_code?)`。
- 某小区有哪些问题或问题分布：先解析小区名，再调用 `get_target_indicator_statistics(...)`。
- 某小区问题总量：先解析小区名，再调用 `get_target_issue_totals(target_name=...)`。
- 某小区涉及多少指标：先解析小区名，再调用 `get_target_indicator_totals(target_name=...)`。
- 某小区分页问题列表：先解析小区名，再调用 `list_target_issues(target_name=..., page_size=..., page_number=...)`。
- 某条问题详情：先通过列表拿到 `issue_id`，再调用 `get_issue_detail(issue_id=...)`。
- 某小区基础详情：先解析小区名，再调用 `get_target_detail(target_name=...)`。如果该工具返回空，再按 `cityagent-call-guardrails` 的回退流程处理。

## 新增路由规则（对应需求1和需求2）

### 路由1：住房情况查询（对应 indicator-statistics 接口）

当用户问题格式为"<区划>中<名称>的住房情况是怎么样的"时：
1. 提取区划名称（如"鼓楼区"），调用 `query_data(query="鼓楼区", sourceFiles="小区.csv")` 获取 area_code
2. 提取具体名称（如"达明新村"），调用 `query_data(query="达明新村", sourceFiles="小区.csv")` 归一化
3. 判断名称类型：
   - 若名称为小区名（如达明新村、XX花园、XX公寓）→ type="1"（住房维度）
   - 若名称为社区名 → type="2" 或 type="1"（可尝试两种）
   - 若名称为街道名 → type="1"/"2"/"3"（住房/社区/街区）
4. 调用 `get_indicator_statistics(statistics_type=type, name=归一化后名称)`

### 路由2：指标统计查询（对应 safeTotal 接口）

当用户问题格式为"<区划>去<指标名称>结果分布情况"时：
1. 提取区划名称（如"鼓楼区"），调用 `query_data(query="鼓楼区", sourceFiles="小区.csv")` 获取 area_code
2. 提取指标名称（如"结构安全隐患结果"），调用 `query_data(query="结构安全隐患", sourceFiles="问题指标表.csv")` 获取 indicator_code
3. 调用 `get_safe_total(indicatorCode=指标编码, areaCode=区划编码)`

### 路由判断示例

- "鼓楼区中达明新村的住房情况是怎么样的"
  → 路由1：住房情况查询
  → 提取：区划=鼓楼区，名称=达明新村（小区）
  → 调用：`get_indicator_statistics(statistics_type="1", name="达明新村")`

- "鼓楼区去住房结构安全隐患结果分布情况"
  → 路由2：指标统计查询
  → 提取：区划=鼓楼区，指标=结构安全隐患结果 → indicatorCode（如N-SSHU）
  → 调用：`get_safe_total(indicatorCode="N-SSHU", areaCode="350102")`

## Preferred Tool Names

- 优先使用标准工具名：`get_region_overview`, `get_dimension_statistics`, `list_indicator_targets`, `get_target_indicator_statistics`, `get_target_detail`, `get_target_issue_totals`, `list_target_issues`, `get_issue_detail`, `get_target_indicator_totals`, `get_indicator_statistics`, `get_safe_total`。
- 只有当上游明确要求兼容旧名字时，才使用别名工具：`get_area_summary`, `get_city_total`, `get_summary_indicator`, `get_safe_total`, `get_indicator_statistics`, `get_estate_detail`, `get_detail_total`, `get_issues_page`, `get_question_details`, `get_issue_statistics`。

## Area Code Hints

- 如果 `query_data(..., sourceFiles="小区.csv")` 已经返回 `district`，优先直接把该值当 `area_code`。
- 当前 `小区.csv` 中已出现的区划编码包括：`鼓楼区=350102`, `台江区=350103`, `仓山区=350104`, `晋安区=350111`。
- 如果用户只给出区名且不在已知映射里，不要编造编码；无法确定时可回退为全市总览，或先请求用户确认。

## Example

1. 用户问：“达明小区有哪些问题？”
2. 先调 `query_data(query="达明小区", sourceFiles="小区.csv")`。
3. 从结果中抽取 `estate_name="达明新村"`, `community_name="达明社区"`, `street_name="鼓西街道"`, `district_name="鼓楼区"`, `district="350102"`。
4. 之后所有 cityagent 工具都用 `达明新村` 和 `350102`，不要再传 `达明小区`。

## Hard Rules

- 不要把用户原始小区名直接传给需要 `target_name` 的工具。
- 不要凭感觉编 `indicator_code` 或 `area_code`。
- 当 `query_data` 前几条结果明显跨区、跨社区或跨维度时，先消歧，再继续调用。
