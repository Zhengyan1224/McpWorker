# Cityagent 调用护栏 Skill

用于在真正调用 `cityagent_mcp_server` 之前做参数检查，并在空结果、参数报错、兼容别名场景下给出稳定回退路径。

## Use When

- 你已经拿到归一化后的小区名、指标编码、区划编码，准备调用 cityagent 工具
- cityagent 工具返回空数据、查询为空，或类型参数错误
- 你不确定该用标准工具名还是兼容别名

## Parameter Guardrails

- `area_code`：只传真实区划编码字符串，例如 `350102`。优先来自 `query_data(..., sourceFiles="小区.csv")` 的 `district` 字段。
- `target_name`：只传 `query_data(..., sourceFiles="小区.csv")` 选出的 `estate_name`。
- `indicator_code`：只传 `query_data(..., sourceFiles="问题指标表.csv")` 选出的 `indicator_code`。
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

### 新增安全调用模式（需求1 + 需求2）

- 住房情况查询（需求1）：先归一化名称，再调用 `get_indicator_statistics(statistics_type="1", name="<归一化名称>")`
  - type 必须是字符串 "1"、"2" 或 "3"，不要传中文
  - name 为归一化后的小区/社区/街道名称
- 指标统计查询（需求2）：先归一化区划和指标编码，再调用 `get_safe_total(indicatorCode=..., areaCode=...)`
  - indicatorCode 只接受 `query_data(..., sourceFiles="问题指标表.csv")` 返回的编码
  - areaCode 只接受 `query_data(..., sourceFiles="小区.csv")` 返回的 district 编码

## Hard Rules

- 不要把标准参数名和别名参数名混在同一次调用里。
- 不要在类型参数报错后把中文枚举再试一次；实测这条路不通。
- 不要因为一个工具返回空，就立即更换真实小区名；先检查参数是否落到了错误字段上。
