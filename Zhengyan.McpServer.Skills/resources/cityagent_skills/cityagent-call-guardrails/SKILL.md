# ⚠️ Cityagent 调用护栏 Skill（输出规范：不要出现"类型1"、"住房（类型1）"等字眼）

用于在真正调用 `cityagent_mcp_server` 之前做参数检查，并在空结果、参数报错、兼容别名场景下给出稳定回退路径。

## 问题类型编码（重要基础）

在调用需要问题类型参数的接口时，必须使用以下编码：

| 编码 | 问题类型 | 说明 |
|------|----------|------|
| 1 | 住房 | 住宅楼栋、住房单元等 |
| 2 | 小区（社区） | 小区、新村、花园、宿舍等 |
| 3 | 街区 | 街道、片区 |
| 4 | 城区（城市） | 城区、城市级 |

**重要：内部编码仅用于接口调用，在回答用户时必须转换为对应的中文名称！**

## 输出格式规范（重要！必须遵守）

当接口返回数据中包含 `target_type`、`issuesType` 等类型字段时，**必须**将编码转换为用户可读的中文名称：

| 接口返回 | 回答用户时必须说 |
|----------|-----------------|
| `"target_type": "1"` 或 `issuesType: "1"` | 住房 |
| `"target_type": "2"` 或 `issuesType: "2"` | 小区（社区） |
| `"target_type": "3"` 或 `issuesType: "3"` | 街区 |
| `"target_type": "4"` 或 `issuesType: "4"` | 城区（城市） |

**禁止在回答中出现以下内容：**
- ❌ "类型1"、"类型2"、"类型3"、"类型4"
- ❌ "问题类型1"、"问题类型2"
- ❌ "住房（类型1）"、"小区（类型2）"、"街区（类型3）"
- ❌ 任何数字编码形式

**🚨 特别注意：不要在括号内显示编码！**
- ✅ "住房问题184条" 或 "住房：184条"
- ❌ "住房（类型1）问题184条" ← 这是错误的！
- ❌ "住房类（类型1）共184条" ← 这是错误的！
- 在表格或列表中，只写中文名称，不要加括号说明类型

**正确示例：**
- ✅ "住房问题共184条"
- ✅ "小区（社区）问题数量为73条"
- ✅ "街区问题有28条"

**错误示例（绝对禁止）：**
- ❌ "住房（类型1）问题184条"
- ❌ "住房类（类型1）共184条"
- ❌ "小区（类型2）73条"

## Use When

- 你已经拿到归一化后的小区名、指标编码、区划编码，准备调用 cityagent 工具
- cityagent 工具返回空数据、查询为空，或类型参数错误
- 你不确定该用标准工具名还是兼容别名

## Parameter Guardrails

- `area_code`：只传真实区划编码字符串，例如 `350102`。优先来自 `query_data(..., sourceFiles="小区.db")` 的 `district` 字段。
- `target_name`：只传 `query_data(..., sourceFiles="小区.db")` 选出的 `estate_name`。
- `indicator_code`：只传 `query_data(..., sourceFiles="问题指标表.db")` 选出的 `indicator_code`。
- `page_size` / `page_number`：当用户只说“列一下问题”但没给分页参数时，先用 `page_size=20`, `page_number=1` 起步，避免一次拉太大。
- `_t`：可省略；只有兼容链路明确需要时，再传当前毫秒时间戳字符串。

## Verified Notes

- `get_target_indicator_statistics` 和 `get_indicator_statistics` 的类型参数，后端实测只接受字符串数字 `1` 或 `2`，不要传“小区”“区划”这类中文值。
- 当前工具说明里只写了“按小区查询还是按区划查询”，但没有给出 `1/2` 的稳定枚举解释。若当前系统里已有成功样例，优先沿用成功样例；如果没有，不要臆造中文值或新枚举。
- 兼容别名工具与标准工具混用时，要保证参数名跟所选工具完全一致，例如：
  - 标准：`indicator_code`, `area_code`, `target_name`, `page_size`, `page_number`, `issue_id`
  - 别名：`indicatorCode`, `areaCode`, `issueOwnerName`, `size`, `current`, `id`

## Empty Result Recovery

1. 先确认是不是把用户原始小区名直接传进去了；如果是，回到小区归一化步骤。
2. 再确认是不是把社区名、区名误当成了 `target_name`。
3. 再确认 `indicator_code` 是否来自正式召回结果，而不是自然语言原词。
4. 如果参数都正确但结果仍为空，才考虑该对象当前无数据这一种解释。
5. 遇到“查询为空！”时，不要立刻改用更模糊的名字；先回头核对字段名和参数名是否传对。

## Alias Policy

- 默认优先标准工具名。
- 只有在标准工具已有已知兼容问题，或上游明确要求旧接口时，才切到别名工具。
- 一旦切到别名工具，当次调用所有参数名都必须同步切换到别名版本，不能混搭。

## Safe Call Patterns

- 小区问题总量：先归一化小区名，再调用 `get_target_issue_totals`；如果旧链路要求别名，再用 `get_detail_total(issueOwnerName=...)`。
- 小区问题列表：先归一化小区名，再调用 `list_target_issues(target_name=..., page_size=20, page_number=1)`。
- 问题详情：先从列表拿 `issue_id`，再调用 `get_issue_detail(issue_id=...)`；旧链路才用 `get_question_details(id=...)`。
- 某指标涉及对象：先归一化指标编码，再调用 `list_indicator_targets(indicator_code=..., area_code?)`；旧链路才用 `get_safe_total(indicatorCode=..., areaCode?)`。

### 新增安全调用模式（需求1 + 需求2 + 需求3）

- 住房情况查询（需求1）：先归一化名称，再调用 `get_indicator_statistics(statistics_type="1", name="<归一化名称>")`
  - type 必须是字符串 "1"、"2" 或 "3"，不要传中文
  - name 为归一化后的小区/社区/街道名称
- 指标统计查询（需求2）：先归一化区划和指标编码，再调用 `get_safe_total(indicatorCode=..., areaCode=...)`
  - indicatorCode 只接受 `query_data(..., sourceFiles="问题指标表.db")` 返回的编码
  - areaCode 只接受 `query_data(..., sourceFiles="小区.db")` 返回的 district 编码
- **问题详情内容检索（需求3）**：先提取关键词，再调用 `query_data(query="<关键词>", sourceFiles="问题列表.db", topN=50)`
  - 从 `问题列表.db` 的 `issue_desc` 字段匹配用户关心的关键词
  - 返回结果中的 `issue_owner_name` 是问题所属小区名
  - `issue_desc` 是问题详情描述
  - 通过统计 `issue_owner_name` 的分布来回答"分布在哪些小区"

## Hard Rules

- 不要把标准参数名和别名参数名混在同一次调用里。
- 不要在类型参数报错后把中文枚举再试一次；实测这条路不通。
- 不要因为一个工具返回空，就立即更换真实小区名；先检查参数是否落到了错误字段上。

## 指标名称规范（重要）

- **必须使用完整指标名称**进行回答和展示，如"存在结构安全隐患的住宅数量（栋）"、"中学服务半径覆盖率（%）"等
- **禁止使用指标代码**如"N-SSHU"、"N-GSHU"等，也**禁止使用"指标1xxx"、"维度1xxx"这类代号式称呼**
- 从 `query_data(..., sourceFiles="问题指标表.db")` 召回结果中获取 `indicator_name` 字段作为标准名称
- 在调用工具时记录返回数据中的指标名称，用于最终回答
- 如果返回的数据只有代码没有名称，必须先调用 `query_data(query="<指标代码>", sourceFiles="问题指标表.db")` 查询补全
