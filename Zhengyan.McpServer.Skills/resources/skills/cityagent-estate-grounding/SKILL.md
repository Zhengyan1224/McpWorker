# 小区名称归一化 Skill

用于把用户口语化的小区名、宿舍名、简称，映射成库内可直接调用的 `estate_name`，并一并拿到 `community_name`, `street_name`, `district_name`, `district`。

## Use When

- 用户提到小区、宿舍、花园、新村、公寓、大厦、楼盘
- 任一后续工具需要 `target_name`
- 任一后续工具需要小区所属 `area_code`

## Mandatory Query

始终先调用：

`query_data(query="<用户原始小区名>", sourceFiles="小区.csv", topN=5)`

除非你已经在当前会话里拿到同一对象的可靠归一化结果，否则不要跳过这一步。

## Fields to Trust

- `estate_name`：后续 `target_name` 的唯一首选值
- `community_name`：用于和用户描述的社区信息交叉验证
- `street_name`：用于和用户描述的街道信息交叉验证
- `district_name`：用于自然语言回答
- `district`：用于 `area_code`

## Selection Rules

1. 先看语义是否对得上，再看 `_similarity` 排名。
2. 用户经常省略后缀，例如把“达明新村”说成“达明小区”；遇到这种情况，优先保留同一词干、且社区街道也合理的候选。
3. 如果用户额外给了社区、街道、区名，优先选择这些上下文同时匹配的候选，即使它不是纯相似度第一。
4. 如果前两名候选落在不同 `district_name` 或不同 `community_name`，且差距不明显，不要盲选。
5. 可把 `_similarity >= 0.60` 视为较强信号；`< 0.50` 时要特别谨慎。相似度只是辅助，不是唯一依据。

## Output Contract

一旦选定候选，后续调用统一携带：

- `target_name = estate_name`
- `area_code = district`，当工具需要区划编码时使用
- 回答中可补充 `community_name`, `street_name`, `district_name`

## Example

用户说：“达明小区”

召回结果可命中：

- `estate_name = 达明新村`
- `community_name = 达明社区`
- `street_name = 鼓西街道`
- `district_name = 鼓楼区`
- `district = 350102`

后续应传：

- `target_name = "达明新村"`
- `area_code = "350102"`

## Ambiguity Handling

- 如果多个候选都像真答案，先把 2 到 3 个候选名称和所属区、街道列给用户确认。
- 如果用户只是问区域总览，而小区无法唯一归一化，不要把不确定小区硬塞给小区级工具；可以先退回区级回答。
- 如果工具返回“查询为空”，先检查是不是选错小区名，再考虑该小区确实没有问题数据。

## Hard Rules

- 不要直接把 `query_data` 的第 1 条结果机械当答案；先核对社区、街道、区。
- 不要把 `community_name` 当成 `target_name` 传给小区工具，除非用户本来问的就是社区级对象且服务端已验证支持。
- 不要自造不存在于 `小区.csv` 结果里的区划编码。
