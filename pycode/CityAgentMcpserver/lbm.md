---
title: 默认模块
language_tabs:
  - shell: Shell
  - http: HTTP
  - javascript: JavaScript
  - ruby: Ruby
  - python: Python
  - php: PHP
  - java: Java
  - go: Go
toc_footers: []
includes: []
search: true
code_clipboard: true
highlight_theme: darkula
headingLevel: 2
generator: "@tarslib/widdershins v4.0.30"

---

# 默认模块

Base URLs:http://uam.ffcs.cn:30148

# Authentication

# 总览模块

## GET 区域综合统计接口-AI总结

GET /api/lbm/lbmcityissues/summary

### 请求参数

|名称|位置|类型|必选|说明|
|---|---|---|---|---|
|areaCode|query|string| 否 |默认全福州|
|Authorization|header|string| 是 |none|
|cloudna-service-route-id|header|string| 是 |none|

> 返回示例

> 200 Response

```json
{
  "code": 0,
  "errorMessage": "OK",
  "data": [
    {
      "issuesType": "1",
      "ownerDetailList": [
        {
          "issueOwnerName": "达明新村",
          "districtName": "鼓楼区",
          "streetName": "鼓西街道",
          "communityName": "达明社区",
          "checkIndicatorCount": 3
        },
        {
          "issueOwnerName": "东汤小区",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "汤边社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "元洪花园",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "闽都社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "粮食局宿舍",
          "districtName": "鼓楼区",
          "streetName": "鼓西街道",
          "communityName": "达明社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "龙庭花园",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "乐天泉社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "塔头新村",
          "districtName": "晋安区",
          "streetName": "岳峰镇",
          "communityName": "浦下社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "卫前里新村",
          "districtName": "鼓楼区",
          "streetName": "鼓东街道",
          "communityName": "庆城社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "祥安花园",
          "districtName": "台江区",
          "streetName": "新港街道",
          "communityName": "雁塔社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "永安街新村",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "金汤社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "裕华公寓",
          "districtName": "台江区",
          "streetName": "上海街道",
          "communityName": "河上社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "海潮小区",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "乐天泉社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "金城小区",
          "districtName": "晋安区",
          "streetName": "新店镇",
          "communityName": "金城社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "鼓山苑小区",
          "districtName": "晋安区",
          "streetName": "鼓山镇",
          "communityName": "浦东社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "省管理局宿舍",
          "districtName": "鼓楼区",
          "streetName": "华大街道",
          "communityName": "屏山社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "宏裕新村",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "莲宅社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "遥逸居",
          "districtName": "台江区",
          "streetName": "上海街道",
          "communityName": "交通社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "警官公寓",
          "districtName": "鼓楼区",
          "streetName": "鼓东街道",
          "communityName": "汤边社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "怡丰楼",
          "districtName": "台江区",
          "streetName": "瀛洲街道",
          "communityName": "河上社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "长城花园",
          "districtName": "晋安区",
          "streetName": "鼓山镇",
          "communityName": "前横社区",
          "checkIndicatorCount": 1
        }
      ],
      "statisticsList": [
        {
          "questionCount": 184,
          "checkIndicators": 6
        }
      ],
      "indicators": [
        {
          "checkIndicatorCode": "N-NCH",
          "checkIndicatorCount": 10
        },
        {
          "checkIndicatorCode": "N-HRAA",
          "checkIndicatorCount": 15
        },
        {
          "checkIndicatorCode": "N-B-SSH",
          "checkIndicatorCount": 20
        },
        {
          "checkIndicatorCode": "N-B-ESH",
          "checkIndicatorCount": 36
        },
        {
          "checkIndicatorCode": "N-H-GSH",
          "checkIndicatorCount": 40
        },
        {
          "checkIndicatorCode": "N-H-SSH",
          "checkIndicatorCount": 63
        }
      ]
    },
    {
      "issuesType": "2",
      "ownerDetailList": [
        {
          "issueOwnerName": "东汤小区",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "汤边社区",
          "checkIndicatorCount": 3
        },
        {
          "issueOwnerName": "达明新村",
          "districtName": "鼓楼区",
          "streetName": "鼓西街道",
          "communityName": "达明社区",
          "checkIndicatorCount": 3
        },
        {
          "issueOwnerName": "裕华公寓",
          "districtName": "台江区",
          "streetName": "上海街道",
          "communityName": "河上社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "大营街 5 号小区",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "汤边社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "凯旋商厦",
          "districtName": "台江区",
          "streetName": "新港街道",
          "communityName": "雁塔社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "粮食局宿舍",
          "districtName": "鼓楼区",
          "streetName": "鼓西街道",
          "communityName": "达明社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "祥安花园",
          "districtName": "台江区",
          "streetName": "新港街道",
          "communityName": "雁塔社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "怡丰楼",
          "districtName": "台江区",
          "streetName": "瀛洲街道",
          "communityName": "河上社区",
          "checkIndicatorCount": 2
        },
        {
          "issueOwnerName": "劳改局宿舍金皇公寓",
          "districtName": "鼓楼区",
          "streetName": "鼓西街道",
          "communityName": "达明社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "八角楼巷 11 号",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "东大社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "龙庭花园",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "乐天泉社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "梦茵园 1 座～5 座",
          "districtName": "台江区",
          "streetName": "上海街道",
          "communityName": "交通社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "省立宿舍",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "金汤社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "卫前里新村",
          "districtName": "鼓楼区",
          "streetName": "鼓东街道",
          "communityName": "庆城社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "温泉新村（地震局宿舍）",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "龙峰社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "永安街新村",
          "districtName": "鼓楼区",
          "streetName": "温泉街道",
          "communityName": "金汤社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "仓山区金骏小区",
          "districtName": "仓山区",
          "streetName": "金山街道",
          "communityName": "横江渡社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "福建省商业厅宿舍",
          "districtName": "鼓楼区",
          "streetName": "鼓东街道",
          "communityName": "龙峰社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "海潮小区",
          "districtName": "鼓楼区",
          "streetName": "水部街道",
          "communityName": "乐天泉社区",
          "checkIndicatorCount": 1
        },
        {
          "issueOwnerName": "遥逸居",
          "districtName": "台江区",
          "streetName": "上海街道",
          "communityName": "交通社区",
          "checkIndicatorCount": 1
        }
      ],
      "statisticsList": [
        {
          "questionCount": 73,
          "checkIndicators": 5
        }
      ],
      "indicators": [
        {
          "checkIndicatorCode": "N-C-CCF",
          "checkIndicatorCount": 6
        },
        {
          "checkIndicatorCode": "L-P-SW",
          "checkIndicatorCount": 11
        },
        {
          "checkIndicatorCode": "N-C-PAS",
          "checkIndicatorCount": 12
        },
        {
          "checkIndicatorCode": "N-C-PM",
          "checkIndicatorCount": 12
        },
        {
          "checkIndicatorCode": "N-C-ECF",
          "checkIndicatorCount": 32
        }
      ]
    }
  ]
}
```

### 返回结果

|状态码|状态码含义|说明|数据模型|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|none|Inline|

### 返回数据结构

状态码 **200**

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|» code|integer|true|none||none|
|» errorMessage|string|true|none||none|
|» data|[object]|true|none||none|
|»» issuesType|string|true|none||分析维度|
|»» ownerDetailList|[object]|true|none||从属详情列表|
|»»» issueOwnerName|string|true|none||小区名称|
|»»» districtName|string|true|none||区划名称|
|»»» streetName|string|true|none||街道名称|
|»»» communityName|string|true|none||社区名称|
|»»» checkIndicatorCount|integer|true|none||涉及指标数量|
|»» statisticsList|[object]|true|none||统计详情列表|
|»»» questionCount|string|true|none||问题数量|
|»»» checkIndicators|string|true|none||指标（去重）|
|»» indicators|[object]|true|none||指标列表|
|»»» checkIndicatorCode|string|true|none||涉及指标数量|
|»»» checkIndicatorCount|integer|true|none||涉及指标类型|

### 请求的Shell示例：

```shell
curl --location --request GET 'http://uam.ffcs.cn:30148/api/lbm/lbmcityissues/summary?areaCode' \
--header 'Authorization: Bearer +yEuTitoL20J8F8/.vC/Yu6YLZny+dPk7rLponAvmZD5GKCMfvNhYU3HnexqLQup3Z9vjN+rDs65Kv+1UeaXH/4w5UmKiSRU+za8X4CiZkQ5Euw3ubWB3JoefgD+swq2HF7py8pxJIbBISjTpWhVHsHLGdSolZ9enmvn0XVAMrQuzXbzFvK8sReAp54fnx/RXUTjf4eL3CMGiuJE7Ga/UUxdBjfbm888DqhIVbHiQpIycZrH2dbIjN92AzALyFAnqcebnELtT+tYXCyWxkXXdbK5HqTCSixImKfeuaeHoBsXKf0DxJNbgbIrMkx1d0P2qVIrubFs0QEk5N2PD4tbG2HAfLyZN8bRZxIwbJYautIhJvIr8nrSKySan3HngIdG7N4/6hvEDaVfjxqFZvA8hRm45aX9KRS84GzpAg7VQbOlJXZtHB700WC/PWSZ2+pcC891ZNnyFNgL+8A59ZW+/1bUr5cZoHvhH5cy0HxWDpJLlfU/mmbQEDp8jYMBpe7ZR+rkrJZH19ePMlegv3ZdxD/BhD5wAayUsA3XAHloFA3IPVJF09c+j5oY/GNsLOtqn8q2OYTb9nrvkXi8BHRgRhW4p3AX7F8zDwvMYWycpOcPDZJy+exbP7KamlXDMuZR3ugD5OJ1vK4G4co4Nboww/KMdZWlskNT8UMhB3vLfLhRtRLmcjYA01SEBJ4gLLgOzDHl6JO4wzlCUy2RlLCSYpoxm/VAfFWLqMv78d2VI3yf9e+Ogcjv55zFdyR+AMopU84QsRrD1wrFQ==.t6Y3famRpYTJAQNfYjXAQ5W6WwO9laFyJ3UkTUrQ87I' \
--header 'cloudna-service-route-id: zdh'
```



## GET 分析维度-指标统计接口-AI-Chart

GET /api/lbm/lbmcityissues/summaryIndicator

分析维度-指标统计接口

### 请求参数

|名称|位置|类型|必选|说明|
|---|---|---|---|---|
|Authorization|header|string| 是 |none|
|cloudna-service-route-id|header|string| 是 |none|

> 返回示例

> 200 Response

```json
{
  "code": 0,
  "errorMessage": "OK",
  "data": [
    {
      "issuesType": "1",
      "statisticsList": [
        {
          "questionCount": 184,
          "checkIndicators": 6
        }
      ]
    },
    {
      "issuesType": "2",
      "statisticsList": [
        {
          "questionCount": 73,
          "checkIndicators": 5
        }
      ]
    }
  ]
}
```

### 返回结果

|状态码|状态码含义|说明|数据模型|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|none|Inline|

### 返回数据结构

状态码 **200**

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|» code|integer|true|none||none|
|» errorMessage|string|true|none||none|
|» data|[object]|true|none||none|
|»» issuesType|string|true|none|分析维度类型|none|
|»» statisticsList|[object]|true|none||none|
|»»» questionCount|integer|true|none|问题数量|none|
|»»» checkIndicators|integer|true|none|指标数量|none|

### 请求的Shell示例：

```shell
curl --location --request GET 'http://uam.ffcs.cn:30148/api/lbm/lbmcityissues/summaryIndicator' \
--header 'Authorization: Bearer /9G73cSd9PWf9ZKE.LqTODbecKx3+9vE4xHW+hL7IdSlYVputHSceKbR52fAH2/X1jw770D9gpod26W3gHd4NS35XXTM0af3/m0rw2d2eqUYLkCVplSrhjLIVP4ZHtTl5DsXCJL8vzkFZIMd3T2XL00FDv1D1MPejimzR+El5ULTpe3e3exDToZQYl8df48H9wa7PVJv+lYJdNm+2MnDy7/8WvgJhGoEkZg6VmTfCW9znC9cpjtbv1a6jj3TdIM0iUyY+JfwpBKVRO14jtfBoTA+5rdK4J0Ry113SwJ8LaqwuVqSTrmZ+LD0HSoE85cxfFOGD5/4hoFW08QbWvAQ3sw7dx1UU+QejfBQRjqnnA0oJB0dfHL0PJqzPlu0Mc/ao7aM9bYunzInrrNNXGAs8fO1y3PrLUbjrC4JWIo5gAoUz+Fc0zGe44OXz0a/S81WOJB8ycWKIeI3F7e/2vVZOMxW9sxvekZdveUXF+2ZnyZteIyLilWYQRJHRb0R1c54GoLvxbzhONgaTHM8Y6lMuJL1JmzS/AWazBuL3SYioXOGSITVc1lOFhAFzn+ZhIOBFRVLQVjo32pGibOntYB/Vwrs5GqpkgGCA1F0+Bhd08AvBJ4Nk+d+QolP2eydd8kNoReJXrXTpgtncLicd3RAE0ed50MbCl3RH7PliI9wVCjEDL61xN/8wDMpxHxVRJQypRjSh3Nry/iEURibkRz32TYKSK55nImHiz2759YuayegsfdUbgBLaEHKZT5wwtvrrqm2tX75btHSg==.t6Y3famRpYTJAQNfYjXAQ5W6WwO9laFyJ3UkTUrQ87I' \
--header 'cloudna-service-route-id: zdh'
```



## GET 区域-指标接口-AI

GET /api/lbm/lbmcityissues/safeTotal

### 请求参数

|名称|位置|类型|必选|说明|
|---|---|---|---|---|
|areaCode|query|string| 否 |区划名称|
|indicatorCode|query|string| 是 |指标编码|
|Authorization|header|string| 是 |none|
|cloudna-service-route-id|header|string| 是 |none|

> 返回示例

> 200 Response

```json
{
  "code": 0,
  "errorMessage": "OK",
  "data": [
    {
      "issueOwnerName": "八角楼巷 11 号",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-PAS"
      ],
      "latitude": 26.09415,
      "longitude": 119.299506,
      "xxx": null
    },
    {
      "issueOwnerName": "仓山区金骏小区",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "L-P-SW"
      ],
      "latitude": 26.045935,
      "longitude": 119.272026,
      "xxx": null
    },
    {
      "issueOwnerName": "长城花园",
      "issusePosition": [
        "长城花园4#",
        "长城花园5#",
        "长城花园6#",
        "长城花园7#",
        "长城花园3#",
        "长城花园8#",
        "长城花园11#",
        "长城花园9#",
        "长城花园10#",
        "长城花园2#",
        "长城花园1#",
        "长城花园12#"
      ],
      "checkIndicatorCodes": [
        "N-B-ESH"
      ],
      "latitude": 26.077147,
      "longitude": 119.35312,
      "xxx": null
    },
    {
      "issueOwnerName": "达明新村",
      "issusePosition": [
        "达明新村2#",
        "达明新村3#",
        "达明新村1#",
        "达明新村7#",
        "达明新村4#",
        "达明新村10#",
        "达明新村9#",
        "达明新村5#",
        "达明新村8#",
        "达明新村6#"
      ],
      "checkIndicatorCodes": [
        "N-C-ECF",
        "L-P-SW",
        "N-B-SSH",
        "N-H-SSH",
        "N-H-GSH",
        "N-C-PM"
      ],
      "latitude": 26.088169,
      "longitude": 119.294976,
      "xxx": null
    },
    {
      "issueOwnerName": "大营街 5 号小区",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-CCF",
        "N-C-PM"
      ],
      "latitude": 26.091353,
      "longitude": 119.307854,
      "xxx": null
    },
    {
      "issueOwnerName": "地质大院宿舍",
      "issusePosition": [
        "地质大院宿舍4#",
        "地质大院宿舍1#",
        "地质大院宿舍6#",
        "地质大院宿舍3#",
        "地质大院宿舍5#",
        "地质大院宿舍2#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH"
      ],
      "latitude": 26.089063,
      "longitude": 119.317524,
      "xxx": null
    },
    {
      "issueOwnerName": "东汤小区",
      "issusePosition": [
        "东汤小区4#",
        "东汤小区12#",
        "东汤小区1#",
        "东汤小区2#",
        "东汤小区8#",
        "东汤小区15#",
        "东汤小区3#",
        "东汤小区6#"
      ],
      "checkIndicatorCodes": [
        "L-P-SW",
        "N-HRAA",
        "N-C-PM",
        "N-C-CCF",
        "N-H-SSH"
      ],
      "latitude": 26.096037,
      "longitude": 119.308588,
      "xxx": null
    },
    {
      "issueOwnerName": "福建省商业厅宿舍",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-ECF"
      ],
      "latitude": 26.096178,
      "longitude": 119.305856,
      "xxx": null
    },
    {
      "issueOwnerName": "鼓山苑小区",
      "issusePosition": [
        "鼓山苑小区2#",
        "鼓山苑小区4#",
        "鼓山苑小区1#",
        "鼓山苑小区6#"
      ],
      "checkIndicatorCodes": [
        "N-B-SSH"
      ],
      "latitude": 26.066291,
      "longitude": 119.35652,
      "xxx": null
    },
    {
      "issueOwnerName": "海潮小区",
      "issusePosition": [
        "海潮小区1#",
        "海潮小区11#",
        "海潮小区8#",
        "海潮小区10#",
        "海潮小区9#",
        "海潮小区4#",
        "海潮小区2#",
        "海潮小区6#",
        "海潮小区3#",
        "海潮小区5#",
        "海潮小区12#",
        "海潮小区7#"
      ],
      "checkIndicatorCodes": [
        "N-B-ESH",
        "N-C-CCF",
        "N-NCH"
      ],
      "latitude": 26.071642,
      "longitude": 119.31557,
      "xxx": null
    },
    {
      "issueOwnerName": "宏裕新村",
      "issusePosition": [
        "宏裕新村5#",
        "宏裕新村9#",
        "宏裕新村7#",
        "宏裕新村1#",
        "宏裕新村4#",
        "宏裕新村2#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH"
      ],
      "latitude": 26.074833,
      "longitude": 119.320424,
      "xxx": null
    },
    {
      "issueOwnerName": "金城小区",
      "issusePosition": [
        "金城小区8#",
        "金城小区1#",
        "金城小区6#",
        "金城小区10#",
        "金城小区5#",
        "金城小区2#",
        "金城小区3#",
        "金城小区4#"
      ],
      "checkIndicatorCodes": [
        "N-H-GSH",
        "N-HRAA"
      ],
      "latitude": 26.125098,
      "longitude": 119.293953,
      "xxx": null
    },
    {
      "issueOwnerName": "警官公寓",
      "issusePosition": [
        "警官公寓4#",
        "警官公寓1#"
      ],
      "checkIndicatorCodes": [
        "N-NCH"
      ],
      "latitude": 26.083188,
      "longitude": 119.290255,
      "xxx": null
    },
    {
      "issueOwnerName": "凯旋商厦",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-PAS",
        "N-C-ECF"
      ],
      "latitude": 26.067975,
      "longitude": 119.317415,
      "xxx": null
    },
    {
      "issueOwnerName": "劳改局宿舍金皇公寓",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-ECF"
      ],
      "latitude": 26.090564,
      "longitude": 119.292553,
      "xxx": null
    },
    {
      "issueOwnerName": "粮食局宿舍",
      "issusePosition": [
        "粮食局宿舍5#",
        "粮食局宿舍6#",
        "粮食局宿舍2#",
        "粮食局宿舍3#",
        "粮食局宿舍1#",
        "粮食局宿舍4#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH",
        "N-C-ECF",
        "N-HRAA",
        "N-C-PM"
      ],
      "latitude": 26.077314,
      "longitude": 119.287778,
      "xxx": null
    },
    {
      "issueOwnerName": "龙庭花园",
      "issusePosition": [
        "龙庭花园8#",
        "龙庭花园2#",
        "龙庭花园3#",
        "龙庭花园6#",
        "龙庭花园7#",
        "龙庭花园5#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH",
        "N-C-CCF",
        "N-NCH"
      ],
      "latitude": 26.072889,
      "longitude": 119.315615,
      "xxx": null
    },
    {
      "issueOwnerName": "梦茵园 1 座～5 座",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-ECF"
      ],
      "latitude": 26.06917,
      "longitude": 119.295996,
      "xxx": null
    },
    {
      "issueOwnerName": "省管理局宿舍",
      "issusePosition": [
        "省管理局宿舍4#",
        "省管理局宿舍2#",
        "省管理局宿舍1#",
        "省管理局宿舍3#",
        "省管理局宿舍5#",
        "省管理局宿舍6#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH"
      ],
      "latitude": 26.065985,
      "longitude": 119.30159,
      "xxx": null
    },
    {
      "issueOwnerName": "省立宿舍",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-ECF"
      ],
      "latitude": 26.11967,
      "longitude": 119.297542,
      "xxx": null
    },
    {
      "issueOwnerName": "塔头新村",
      "issusePosition": [
        "塔头新村1#",
        "塔头新村7#",
        "塔头新村8#",
        "塔头新村9#",
        "塔头新村10#",
        "塔头新村12#",
        "塔头新村2#",
        "塔头新村3#",
        "塔头新村5#",
        "塔头新村6#",
        "塔头新村4#",
        "塔头新村11#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH",
        "N-B-ESH"
      ],
      "latitude": 26.087305,
      "longitude": 119.317668,
      "xxx": null
    },
    {
      "issueOwnerName": "卫前里新村",
      "issusePosition": [
        "卫前里新村5#",
        "卫前里新村7#",
        "卫前里新村1#",
        "卫前里新村4#",
        "卫前里新村2#",
        "卫前里新村9#",
        "卫前里新村3#",
        "卫前里新村10#"
      ],
      "checkIndicatorCodes": [
        "N-NCH",
        "N-H-GSH",
        "N-C-PAS"
      ],
      "latitude": 26.088633,
      "longitude": 119.30345,
      "xxx": null
    },
    {
      "issueOwnerName": "温泉新村（地震局宿舍）",
      "issusePosition": [],
      "checkIndicatorCodes": [
        "N-C-ECF"
      ],
      "latitude": 26.095512,
      "longitude": 119.306493,
      "xxx": null
    },
    {
      "issueOwnerName": "祥安花园",
      "issusePosition": [
        "祥安花园2#",
        "祥安花园3#",
        "祥安花园4#",
        "祥安花园1#",
        "祥安花园7#"
      ],
      "checkIndicatorCodes": [
        "N-C-ECF",
        "N-C-PAS",
        "N-B-SSH",
        "N-HRAA"
      ],
      "latitude": 26.067338,
      "longitude": 119.322119,
      "xxx": null
    },
    {
      "issueOwnerName": "遥逸居",
      "issusePosition": [
        "遥逸居2#",
        "遥逸居4#",
        "遥逸居3#",
        "遥逸居1#"
      ],
      "checkIndicatorCodes": [
        "N-B-SSH",
        "N-C-ECF"
      ],
      "latitude": 26.066078,
      "longitude": 119.302912,
      "xxx": null
    },
    {
      "issueOwnerName": "怡丰楼",
      "issusePosition": [
        "怡丰楼1#",
        "怡丰楼6#",
        "怡丰楼7#",
        "怡丰楼2#"
      ],
      "checkIndicatorCodes": [
        "N-C-PM",
        "N-C-PAS",
        "N-B-SSH"
      ],
      "latitude": 26.07104,
      "longitude": 119.301988,
      "xxx": null
    },
    {
      "issueOwnerName": "永安街新村",
      "issusePosition": [
        "永安街新村15#",
        "永安街新村9#",
        "永安街新村6#",
        "永安街新村2#",
        "永安街新村10#",
        "永安街新村14#",
        "永安街新村4#",
        "永安街新村7#"
      ],
      "checkIndicatorCodes": [
        "N-C-ECF",
        "N-H-SSH",
        "N-NCH"
      ],
      "latitude": 26.089539,
      "longitude": 119.307768,
      "xxx": null
    },
    {
      "issueOwnerName": "裕华公寓",
      "issusePosition": [
        "裕华公寓5#",
        "裕华公寓8#",
        "裕华公寓6#",
        "裕华公寓1#",
        "裕华公寓4#",
        "裕华公寓9#",
        "裕华公寓10#",
        "裕华公寓3#",
        "裕华公寓2#"
      ],
      "checkIndicatorCodes": [
        "N-H-GSH",
        "N-C-PM",
        "N-HRAA",
        "N-C-PAS"
      ],
      "latitude": 26.066612,
      "longitude": 119.303317,
      "xxx": null
    },
    {
      "issueOwnerName": "元洪花园",
      "issusePosition": [
        "元洪花园4#",
        "元洪花园3#",
        "元洪花园2#",
        "元洪花园7#",
        "元洪花园1#",
        "元洪花园9#",
        "元洪花园8#",
        "元洪花园6#",
        "元洪花园5#"
      ],
      "checkIndicatorCodes": [
        "N-H-SSH",
        "N-H-GSH"
      ],
      "latitude": 26.073567,
      "longitude": 119.310327,
      "xxx": null
    }
  ]
}
```

### 返回结果

|状态码|状态码含义|说明|数据模型|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|none|Inline|

### 返回数据结构

状态码 **200**

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|» code|integer|true|none||none|
|» errorMessage|string|true|none||none|
|» data|[object]|true|none||none|
|»» issueOwnerName|string|true|none||none|
|»» issusePosition|[string]|true|none||none|
|»» checkIndicatorCodes|[string]|true|none||none|
|»» latitude|number|true|none||none|
|»» longitude|number|true|none||none|
|»» xxx|null|true|none||none|

### 请求的Shell示例：

```shell
curl --location --request GET 'http://uam.ffcs.cn:30148/api/lbm/lbmcityissues/safeTotal?areaCode&indicatorCode=N-ESHU' \
--header 'Authorization: Bearer 9bRwTvLnpV9w4Sik.oOKyjVP+c/Poja7Og1mT3vLuZfWg9Ep7L1uYnJx7GDsKF655BsInYkLoSmycvM+xByPsNhQPEsg1MlCDtuFtdojDxwmm6Vzb5S3CqLJrFtmuOwElKWwFSc3aGgntlkTEHpCDreiwH3HsgyKbKqkIGzIccT9sjdQlbECKG/wgoXlkePhiBZYM5ML4cT44g/giDPq7UI/zyzlB9uRfBTSV53mH8ocAjqoVXjt/UAoURU1DSQxQIDpSJ0vWAL64gZ3Cq54g7DXkCut7GajUPdUtTcnz3afPfZEdnDLeMugkTzI0b0L10ZiMZnrz7iaDzNCfjPQRLufuxL24E1Au8y9fayUWHHiSQptvFj+tRptp4h62cgGNGGWuQCcXtxhRkD6isSILeLZZ0jCr965Pqru7LvBsftheeAMdPpIlPKZg9tM+FTUQ6HL2UYOfNZyVYO33MswZ5ivQh8+rIfr8Qwb/pFqNWt4mZCNcSPBAex1IF6W03LqNtdc4lTIvBrZ9QUROkQ1fRTb/+WoLcNNVdIIi1rTYBFWhEiU2ct2CPNidn2xnAA9goNj6HeE5/xboPu7eBlMK5t7UsoETzjYtImqFK/9IfiAKfir+WaoieJ6mz9HDjHd1sNZn9vy3CR8sQQ/3RqTVKYEMrBjD1j4kxFU05i9vuYZ5tEMaqHFGNwybUwYtx9B9kp8c6KPC77z+O/6ztBgGfluKOygZUOKvEObrUUza9DvYXKz5KLjZu87w0pHgvy2fOqC4AhbFzABw==.t6Y3famRpYTJAQNfYjXAQ5W6WwO9laFyJ3UkTUrQ87I' \
--header 'cloudna-service-route-id: zdh'
```



## GET 小区名称（区划名称）-分析维度-指标统计-AI

GET /api/lbm/lbmcityissues/indicator-statistics

### 请求参数

|名称|位置|类型|必选|说明|
|---|---|---|---|---|
|type|query|string| 是 |none|
|name|query|string| 是 |none|
|_t|query|string| 是 |none|
|Authorization|header|string| 是 |none|
|cloudna-service-route-id|header|string| 是 |none|

> 返回示例

> 200 Response

```json
{
  "code": 0,
  "errorMessage": "OK",
  "data": [
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "砖混结构主体出现砖体缺棱掉角",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "用气环境通风不良，\r\n形成潜在爆炸性环境",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "使用不符合国家标准的劣质或\r\n来源不明的燃气具及配件",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除外窗窗下墙体加建阳台",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "燃气管道穿越密闭空间或违规包，\r\n泄漏气体无法散发",
      "total": 5
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "非成套住宅数量（套）",
      "indicatorCode": "N-NSCHU",
      "issueDesc": "没有独立厨房",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "住宅燃气立管、引入管、水平管运行年限满 20 年，\r\n且存在锈蚀严重、破损等安全隐患",
      "total": 5
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "非成套住宅数量（套）",
      "indicatorCode": "N-NSCHU",
      "issueDesc": "没有独立卫生间",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "燃气器具超期服役，\r\n连接部位老化漏气",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "安全装置缺失、失效或安装不当，\r\n失去预警与保护作用",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "户内燃气橡胶软管存在变硬变脆、龟裂、明显缺损、\r\n油污严重等老化破损现象有燃气泄漏风险",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在燃气安全隐患的住宅数量（栋）",
      "indicatorCode": "N-GSHU",
      "issueDesc": "住户私改私接燃气管道，燃气器具无熄火保护装置，\r\n未安装燃气自闭阀，未安装燃气报警器。",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规加建悬挑飘窗",
      "total": 5
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "消防安全出口指示灯损坏或者缺失。",
      "total": 4
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "砂浆饱和度差，呈粉末状，砖与砂浆之间存在较大缝隙",
      "total": 4
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除承重结构构件，底商或者地库结构柱拆除",
      "total": 4
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "结构梁开裂，梁裂缝多集中于梁的底部，呈现多条裂缝",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "外墙装饰材料（外墙粉刷、涂料、饰面砖等）和保温材料开裂、损坏、脱落；\r\n可进一步细化评价为：无隐患、有隐患（损坏或脱落面积 10 平方米以下）、隐患较大（损坏或脱落面积 10 至 30 平方米）、隐患严重（损坏或脱落面积 30 平方米以上）。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "灭火器缺失、未设置灭火器保护设施；\r\n灭火器老化无法使用",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑散水、勒脚存在开裂、破损或严重沉降，\r\n导致排水倒灌或基础保护失效。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑伸缩缝、沉降缝等结构缝的密封盖板或填充材料老化、\r\n破损、脱落，导致防水失效或杂物侵入。",
      "total": 3
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "多层无电梯住宅楼梯间未沿墙加装扶手。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "外墙悬挂设施不规范（如过大、过高）或损坏松脱的情况。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "屋面排水不畅、漏水。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑出入口上方的雨篷、遮阳棚等构件存在结构变形、\r\n开裂、连接松动或覆面材料破损。",
      "total": 3
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "住宅单元出入口和通道未进行无障碍改造、\r\n地面防滑处理。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑外立面附属管线（如雨水管、燃气管、空调排水管）固定不牢、\r\n破损、锈蚀或堵塞。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑外立面或屋面搭建物（如广告牌、太阳能板、外挂设备支架）存在安装不牢固、\r\n结构锈蚀、超载或违规搭建情况。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "外墙内侧、\r\n地下室渗水漏水。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "消防门缺失、损坏、无法关闭。",
      "total": 3
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "门窗玻璃存在破损、脱落等情况。",
      "total": 2
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "住宅室内空间布局存在高差或门槛，卫生间未安装安全扶手、\r\n淋浴座椅，厨房操作台高度不适应老年人坐姿或轮椅使用需求。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除承重结构构件，户内墙体拆墙打洞",
      "total": 2
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "多层住宅中建成时未安装电梯，具备加装电梯条件但尚未进行加装改造。\r\n是否具备加装条件根据各城市相关要求进行判断确定。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "通风井道、排风烟道等堵塞，\r\n造成通风不畅、异味串味。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "高层住宅消火栓灭火装置缺失或无水、无日常维护、\r\n老化损坏、消防水源不达标。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "楼梯间内楼梯踏步缺损、楼梯扶手松动损坏、\r\n照明损坏缺失、安全护栏松动损坏或缺失",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "屋面附属构件（如瓦片、防水层、天沟、檐口）存在破损、\r\n松动或缺失。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "室外台阶、小径、坡道、护栏存在地砖开裂、\r\n破损、松动或缺失。",
      "total": 2
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "室外台阶、坡道、护栏存在开裂、\r\n破损、松动或缺失。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "公共楼道停放自行车、\r\n电动自行车以及违规充电。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "结构柱损坏：混凝土柱出现竖向受压裂缝、保护层剥落、\r\n钢筋外露锈蚀，或存在明显的倾斜、压溃现象",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除外窗窗下墙体加建阳台，\r\n违规加建悬挑飘窗",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "拆除承重墙：在砖混或剪力墙结构中，\r\n擅自整体或部分拆除承重墙体。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "楼板 贯穿性裂缝 渗水",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "灭火器缺失、未设置灭火器保护设施。",
      "total": 1
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "小区公共区域（如道路、单元门前）未设置连续平整的无障碍通道，存在台阶缺损、\r\n路面破损或坡度过陡等问题，影响轮椅及助行器通行。",
      "total": 1
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "多层住宅公共楼道及出入口照明不足或灯具损坏，\r\n未按要求设置具备声控、光控或延时功能的应急照明设施，影响老年人夜间安全出行。",
      "total": 1
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "小区公共区域（如道路、单元门前）未设置连续平整的无障碍通道，\r\n存在台阶缺损、路面破损或坡度过陡等问题，影响轮椅及助行器通行。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "破坏基础/地梁：违规开挖地下室、地窖，导致基础外露、受损或地基土扰动；\r\n擅自切割地梁",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "建筑外墙或檐口部位的混凝土构件、\r\n装饰线脚等存在剥落、露筋或明显的结构性裂缝。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "外挂物安装缺陷：空调外机架、雨棚、晾衣架等安装不牢，\r\n锚固点选在非承重或已风化墙体上，存在坠落风险。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "楼板开裂，裂缝多为贯通状",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "房屋明显倾斜：通过肉眼或简单工具可察觉房屋整体发生倾斜，\r\n或与相邻建筑明显不平行",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "拆除/破坏结构柱：为扩大空间，擅自拆除或截断底商、\r\n车库、门厅等处的钢筋混凝土承重柱。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在围护安全隐患的住宅数量（栋）",
      "indicatorCode": "N-ESHU",
      "issueDesc": "屋面附属构件（如瓦片、防水层、天沟、檐口）存在破损、松动或缺失。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "混凝土结构构件裂缝，如承重墙体、楼板、结构梁开裂，\r\n裂缝肉眼清晰可见，裂缝较深。墙体、楼板裂缝多为贯通状",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规加建悬挑结构：擅自在外墙外挑出阳台、飘窗、设备平台等，\r\n其承载完全依赖后置锚固，抗倾覆能力不足",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "地下空间违规开挖：在一层或地下室违规向下开挖，\r\n建造“地下室”，影响地基稳定。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "混凝土碳化、锈蚀胀裂：混凝土保护层因碳化或氯离子侵入导致钢筋锈蚀，锈胀力使混凝土顺筋开裂、剥落。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "拆改承重墙：擅自部分或全部拆除混凝土、砖砌体承重墙；\r\n在承重墙上开设超大尺寸门洞、窗口或横向槽沟，严重削弱墙体承载力。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "屋顶违规搭建：在平屋顶上加建轻质房、\r\n种植屋面（未做防水承重设计）、大型太阳能支架等。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "户内承重墙体拆墙打洞，底商或者地库结构柱拆除，\r\n砖混结构拆除阳台承重墙垛等。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "非承重围护墙失稳风险：高大填充墙、女儿墙与主体结构拉结不足，顶部无压顶，存在倒塌风险。",
      "total": 1
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "多层住宅公共楼道及出入口照明不足或灯具损坏，未按要求设置具备声控、\r\n光控或延时功能的应急照明设施，影响老年人夜间安全出行。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "高层住宅消火栓缺失或无水、无日常维护、\r\n老化损坏、消防水源不达标。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "窗下墙改门/窗：擅自将砖混结构的外窗窗下墙拆除，\r\n改为门或落地窗，破坏了墙体的连续性。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "砖混结构主体出现砖体缺棱掉角、表面存在裂缝状，砖与砂浆之间存在较大缝隙。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "关键部位渗漏侵蚀：长期渗漏水导致阳台根部、卫生间楼板等处的混凝土与钢筋发生腐蚀，截面削弱。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "墙体局部空鼓、歪闪：墙体局部与主体脱离、鼓出（膨隆）或发生明显平面外倾斜，\r\n存在失稳风险。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规加建悬挑结构：擅自在外墙外挑出阳台、飘窗、设备平台等，\r\n其承载完全依赖后置锚固，抗倾覆能力不足。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "地基不均匀沉降迹象：墙体出现45度方向发展的斜裂缝（特别是从门窗角部延伸）、\r\n不同部位裂缝形态差异显著，或地坪、散水出现明显开裂、下沉。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除结构承重构件，如户内承重墙体拆墙打洞\r\n底商或者地库结构柱拆除，砖混结构拆除阳台承重墙垛等。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "违规拆除承重结构构件，砖混结构拆除承重墙垛",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "砌筑砂浆失效：灰缝砂浆强度极低，\r\n手捏即碎成粉末状，导致砖块之间粘结力基本丧失",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "纵横墙连接破坏：纵横墙交接处出现竖向通缝，\r\n或连接钢筋缺失、锈断，有分离趋势。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "外墙饰面/构件脱落风险：外墙瓷砖、保温层、\r\n装饰线条等存在大面积空鼓、开裂，有高空坠落隐患。",
      "total": 1
    },
    {
      "secondDimension": "功能完备",
      "indicatorName": "需要进行适老化改造的住宅数量（栋）",
      "indicatorCode": "N-AFRB",
      "issueDesc": "多层无电梯住宅楼梯间未沿墙加装扶手",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在结构安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU",
      "issueDesc": "楼板/屋面板开裂：混凝土楼板出现贯穿性裂缝（从板底到板顶），\r\n特别是出现在板跨中、支座处或呈放射性分布，伴有渗水痕迹。",
      "total": 1
    },
    {
      "secondDimension": "安全耐久",
      "indicatorName": "存在楼道安全隐患的住宅数量（栋）",
      "indicatorCode": "N-SSHU2",
      "issueDesc": "住户占用消防楼梯、楼道、\r\n管道井等公共空间，用于堆放杂物。",
      "total": 1
    }
  ]
}
```

### 返回结果

|状态码|状态码含义|说明|数据模型|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|none|Inline|

### 返回数据结构

状态码 **200**

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|» code|integer|true|none||none|
|» errorMessage|string|true|none||none|
|» data|[object]|true|none||none|
|»» secondDimension|string|true|none||二级指标名称|
|»» indicatorName|string|true|none||指标名称|
|»» indicatorCode|string|true|none||指标编码|
|»» issueDesc|string|true|none||问题描述|
|»» total|integer|true|none||总数|
|»» latitude|number|true|none||纬度|
|»» longitude|number|true|none||经度|
|»» geometry|string|true|none||空间范围|

### 请求的Shell示例：

```shell
curl --location --request GET 'http://uam.ffcs.cn:30148/api/lbm/lbmcityissues/indicator-statistics?type=1&name=达明新村&_t=1776234098464' \
--header 'Authorization: Bearer /f3jMpT/fb0xxZiV.Noysaumr/HF7YlyZ7aG1/IKah+0lKQazSnPk1VQ4yC0PN8cIXDM2PqCn3rjAayV2XwU79q0XcnUSDusgjg2cr/LCMLu51hahZCw7hYGYJu21Z8xkGety5rId39ZzNFlBO8HnWm47lxjYfCtK0GwucKNAeBscukYgze8YhmkNrZ8zpjARhB0gmz/1iwACzrvByTpeezBsuW5DfSdCI2XkrLg6RXzpJxOv59xpszJoMOSmxmZD/vjvQmaTauz6UPW/Vhz3Bqjnd89syVqfmCBOEdey12luRU0/j8G4h4/+xS9bJ/z2fzttKJVaH3SS2ePjspHYadaNyrQ3aO1WlGMMA2u/sAs5buX89k5/eZhRASC+WVslQ/unhGfVML7XNpdkuEgHYBCnuctV9URoOWudiI6zT/PtT1UvQBMcfV3h7omELiMGG51wGwpCAhFHBmhNNnSfJmytSBAO6JA069IKQfBCbnm3OZT9jAOsXDc/94pzavaaXSl923Zo9S05qIsGPv5TvqUg/dQH2BOFjF5YsJtz5xJs3Q3yw9HIoQGfPWOhUuXtgnKBdVZE2xyMT0QIhJTdutIWvkVkJ2Eis3nC36QiJjtGdS0vST15XqEasjtMuHjaPGmHUQnAu/TXXf1MibnpdqXdTuSMflDBg3fz9VGHe0kFCbyTXjd5NlUWR0htLWI3IbS/xo/GYcwLjJOLv1H9rw1Lmfa75mLMrxqlRFGFIUEwEdmzXO1tg9OUNfdiqIdX5/iinV8uvbEg==.t6Y3famRpYTJAQNfYjXAQ5W6WwO9laFyJ3UkTUrQ87I' \
--header 'cloudna-service-route-id: zdh'
```



# 数据模型

<h2 id="tocS_区域综合统计">区域综合统计</h2>

<a id="schema区域综合统计"></a>
<a id="schema_区域综合统计"></a>
<a id="tocS区域综合统计"></a>
<a id="tocs区域综合统计"></a>

```json
{
  "code": 0,
  "errorMessage": "string",
  "data": [
    {
      "issuesType": "string",
      "ownerDetailList": [
        {
          "issueOwnerName": "string",
          "districtName": "string",
          "streetName": "string",
          "communityName": "string",
          "checkIndicatorCount": 0
        }
      ],
      "statisticsList": [
        {
          "questionCount": "string",
          "checkIndicators": "string"
        }
      ],
      "indicators": [
        {
          "checkIndicatorCode": "string",
          "checkIndicatorCount": 0
        }
      ]
    }
  ]
}

```

### 属性

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|code|integer|true|none||none|
|errorMessage|string|true|none||none|
|data|[object]|true|none||none|
|» issuesType|string|true|none||分析维度|
|» ownerDetailList|[object]|true|none||从属详情列表|
|»» issueOwnerName|string|true|none||小区名称|
|»» districtName|string|true|none||区划名称|
|»» streetName|string|true|none||街道名称|
|»» communityName|string|true|none||社区名称|
|»» checkIndicatorCount|integer|true|none||涉及指标数量|
|» statisticsList|[object]|true|none||统计详情列表|
|»» questionCount|string|true|none||问题数量|
|»» checkIndicators|string|true|none||指标（去重）|
|» indicators|[object]|true|none||指标列表|
|»» checkIndicatorCode|string|true|none||涉及指标数量|
|»» checkIndicatorCount|integer|true|none||涉及指标类型|

<h2 id="tocS_分析维度-指标统计">分析维度-指标统计</h2>

<a id="schema分析维度-指标统计"></a>
<a id="schema_分析维度-指标统计"></a>
<a id="tocS分析维度-指标统计"></a>
<a id="tocs分析维度-指标统计"></a>

```json
{
  "code": 0,
  "errorMessage": "string",
  "data": [
    {
      "issuesType": "string",
      "statisticsList": [
        {
          "questionCount": 0,
          "checkIndicators": 0
        }
      ]
    }
  ]
}

```

### 属性

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|code|integer|true|none||none|
|errorMessage|string|true|none||none|
|data|[object]|true|none||none|
|» issuesType|string|true|none|分析维度类型|none|
|» statisticsList|[object]|true|none||none|
|»» questionCount|integer|true|none|问题数量|none|
|»» checkIndicators|integer|true|none|指标数量|none|

<h2 id="tocS_区域-指标-列表">区域-指标-列表</h2>

<a id="schema区域-指标-列表"></a>
<a id="schema_区域-指标-列表"></a>
<a id="tocS区域-指标-列表"></a>
<a id="tocs区域-指标-列表"></a>

```json
{
  "code": 0,
  "errorMessage": "string",
  "data": [
    {
      "issueOwnerName": "string",
      "issusePosition": [
        "string"
      ],
      "checkIndicatorCodes": [
        "string"
      ],
      "latitude": 0,
      "longitude": 0,
      "xxx": null
    }
  ]
}

```

### 属性

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|code|integer|true|none||none|
|errorMessage|string|true|none||none|
|data|[object]|true|none||none|
|» issueOwnerName|string|true|none||none|
|» issusePosition|[string]|true|none||none|
|» checkIndicatorCodes|[string]|true|none||none|
|» latitude|number|true|none||none|
|» longitude|number|true|none||none|
|» xxx|null|true|none||none|

<h2 id="tocS_名称-指标实体">名称-指标实体</h2>

<a id="schema名称-指标实体"></a>
<a id="schema_名称-指标实体"></a>
<a id="tocS名称-指标实体"></a>
<a id="tocs名称-指标实体"></a>

```json
{
  "code": 0,
  "errorMessage": "string",
  "data": [
    {
      "secondDimension": "string",
      "indicatorName": "string",
      "indicatorCode": "string",
      "issueDesc": "string",
      "total": 0,
      "latitude": 0,
      "longitude": 0,
      "geometry": "string"
    }
  ]
}

```

### 属性

|名称|类型|必选|约束|中文名|说明|
|---|---|---|---|---|---|
|code|integer|true|none||none|
|errorMessage|string|true|none||none|
|data|[object]|true|none||none|
|» secondDimension|string|true|none||二级指标名称|
|» indicatorName|string|true|none||指标名称|
|» indicatorCode|string|true|none||指标编码|
|» issueDesc|string|true|none||问题描述|
|» total|integer|true|none||总数|
|» latitude|number|true|none||纬度|
|» longitude|number|true|none||经度|
|» geometry|string|true|none||空间范围|

