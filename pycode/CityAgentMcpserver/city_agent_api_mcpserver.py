from __future__ import annotations

import asyncio
import json
import logging
import os
import time
from dataclasses import dataclass
from typing import Any, Dict, Optional

import httpx
import yaml
from fastmcp import FastMCP

DEFAULT_MCP_HOST = "0.0.0.0"
DEFAULT_MCP_PORT = 28080
DEFAULT_MCP_NAME = "城市更新智能体McpServer"
DEFAULT_ROOT_PATH = "cityagent"
DEFAULT_SERVICE_ROUTE_ID = "zdh"
DEFAULT_TIMEOUT_SECONDS = 30.0
DEFAULT_VERIFY_SSL = False

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger("CityAgentMCP")

CONFIG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.yaml")


@dataclass(frozen=True)
class McpSettings:
    host: str = DEFAULT_MCP_HOST
    port: int = DEFAULT_MCP_PORT
    name: str = DEFAULT_MCP_NAME
    root_path: Optional[str] = DEFAULT_ROOT_PATH


@dataclass(frozen=True)
class ApiSettings:
    base_url: str
    token_url: str
    service_route_id: str = DEFAULT_SERVICE_ROUTE_ID
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS
    verify_ssl: bool = DEFAULT_VERIFY_SSL


@dataclass(frozen=True)
class AuthSettings:
    client_id: str
    client_secret: str


@dataclass(frozen=True)
class AppConfig:
    mcp: McpSettings
    api: ApiSettings
    auth: AuthSettings


def load_config_dict() -> Dict[str, Any]:
    with open(CONFIG_PATH, "r", encoding="utf-8") as file:
        return yaml.safe_load(file) or {}


def parse_bool(value: Any, default: bool) -> bool:
    if value is None:
        return default

    if isinstance(value, bool):
        return value

    if isinstance(value, str):
        normalized = value.strip().lower()
        if normalized in {"1", "true", "yes", "on"}:
            return True
        if normalized in {"0", "false", "no", "off"}:
            return False

    return bool(value)


def build_config(raw_config: Dict[str, Any]) -> AppConfig:
    mcp_config = raw_config.get("mcp", {})
    api_config = raw_config.get("api", {})
    auth_config = raw_config.get("auth", {})

    return AppConfig(
        mcp=McpSettings(
            host=str(mcp_config.get("host", DEFAULT_MCP_HOST)),
            port=int(mcp_config.get("port", DEFAULT_MCP_PORT)),
            name=str(mcp_config.get("name", DEFAULT_MCP_NAME)),
            root_path=mcp_config.get("root_path", DEFAULT_ROOT_PATH),
        ),
        api=ApiSettings(
            base_url=str(api_config["base_url"]).rstrip("/"),
            token_url=str(api_config["token_url"]),
            service_route_id=str(
                api_config.get("service_route_id", DEFAULT_SERVICE_ROUTE_ID)
            ),
            timeout_seconds=float(
                api_config.get(
                    "timeout_seconds",
                    api_config.get("timeout", DEFAULT_TIMEOUT_SECONDS),
                )
            ),
            verify_ssl=parse_bool(
                api_config.get("verify_ssl"),
                DEFAULT_VERIFY_SSL,
            ),
        ),
        auth=AuthSettings(
            client_id=str(auth_config["client_id"]),
            client_secret=str(auth_config["client_secret"]),
        ),
    )


def normalize_root_path(root_path: Optional[str]) -> str:
    if root_path is None:
        return DEFAULT_ROOT_PATH

    return str(root_path).strip().strip("/")


def build_prefixed_path(
    root_path: Optional[str], endpoint: str, trailing_slash: bool = False
) -> str:
    normalized_root_path = normalize_root_path(root_path)
    cleaned_endpoint = endpoint.strip("/")
    base_path = f"/{cleaned_endpoint}" if cleaned_endpoint else "/"

    if normalized_root_path:
        full_path = f"/{normalized_root_path}{base_path}"
    else:
        full_path = base_path

    if trailing_slash and not full_path.endswith("/"):
        return f"{full_path}/"

    return full_path


def build_sse_paths(mcp_settings: McpSettings) -> tuple[str, str]:
    return (
        build_prefixed_path(mcp_settings.root_path, "sse"),
        build_prefixed_path(mcp_settings.root_path, "messages", trailing_slash=True),
    )


def current_timestamp_ms() -> str:
    return str(int(time.time() * 1000))


def with_timestamp(
    params: Optional[Dict[str, Any]] = None, timestamp: Optional[str] = None
) -> Dict[str, Any]:
    query_params = dict(params or {})
    query_params["_t"] = timestamp or current_timestamp_ms()
    return query_params


def filter_query_params(params: Optional[Dict[str, Any]]) -> Dict[str, Any]:
    return {
        key: value
        for key, value in (params or {}).items()
        if value is not None and value != ""
    }


def preview_payload(payload: Any, limit: int = 400) -> str:
    serialized = json.dumps(payload, ensure_ascii=False, default=str)
    if len(serialized) <= limit:
        return serialized
    return f"{serialized[:limit]}..."


class CityAgentApiClient:
    def __init__(self, api_settings: ApiSettings, auth_settings: AuthSettings) -> None:
        self._api_settings = api_settings
        self._auth_settings = auth_settings
        self._token_lock = asyncio.Lock()
        self._cached_bearer_token: Optional[str] = None
        self._token_expire_at = 0.0

    async def get(self, path: str, params: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        query_params = filter_query_params(params)

        try:
            bearer_token = await self._get_bearer_token()
            headers = self._build_headers(bearer_token)

            async with httpx.AsyncClient(
                base_url=self._api_settings.base_url,
                timeout=self._api_settings.timeout_seconds,
                verify=self._api_settings.verify_ssl,
            ) as client:
                response = await client.get(
                    path.lstrip("/"),
                    params=query_params,
                    headers=headers,
                )
                response.raise_for_status()
                return response.json()
        except httpx.HTTPStatusError as exc:
            return self._build_http_error(exc.response)
        except httpx.RequestError as exc:
            logger.error("[api] 请求失败 path=%s error=%s", path, exc)
            return {
                "code": -1,
                "errorMessage": f"Request failed: {exc}",
                "detail": {"path": path, "params": query_params},
            }
        except ValueError as exc:
            logger.error("[api] 响应 JSON 解析失败 path=%s error=%s", path, exc)
            return {
                "code": -1,
                "errorMessage": f"Invalid JSON response: {exc}",
                "detail": {"path": path, "params": query_params},
            }
        except Exception as exc:  # noqa: BLE001
            logger.error("[api] 未知异常 path=%s error=%s", path, exc)
            return {
                "code": -1,
                "errorMessage": f"Unexpected error: {exc}",
                "detail": {"path": path, "params": query_params},
            }

    async def _get_bearer_token(self) -> str:
        now = time.time()
        if self._cached_bearer_token and now < self._token_expire_at - 60:
            return self._cached_bearer_token

        async with self._token_lock:
            now = time.time()
            if self._cached_bearer_token and now < self._token_expire_at - 60:
                return self._cached_bearer_token

            params = {
                "client_id": self._auth_settings.client_id,
                "client_secret": self._auth_settings.client_secret,
                "grant_type": "client_credentials",
            }
            headers = {
                "Accept": "application/json",
                "Content-Type": "application/json",
            }

            async with httpx.AsyncClient(
                timeout=self._api_settings.timeout_seconds,
                verify=self._api_settings.verify_ssl,
            ) as client:
                response = await client.post(
                    self._api_settings.token_url,
                    params=params,
                    headers=headers,
                )
                response.raise_for_status()
                payload = response.json()

            access_token = payload.get("access_token")
            if not access_token:
                raise RuntimeError(f"Token response missing access_token: {payload}")

            expires_in_raw = payload.get("expires_in", 300)
            try:
                expires_in = int(expires_in_raw)
            except (TypeError, ValueError):
                expires_in = 300

            self._cached_bearer_token = f"Bearer {access_token}"
            self._token_expire_at = time.time() + max(expires_in, 120)
            logger.info("[token] 获取成功 expires_in=%s", expires_in)
            return self._cached_bearer_token

    def _build_headers(self, bearer_token: str) -> Dict[str, str]:
        return {
            "Authorization": bearer_token,
            "Accept": "application/json",
            "Content-Type": "application/json",
            "cloudna-service-route-id": self._api_settings.service_route_id,
        }

    @staticmethod
    def _build_http_error(response: httpx.Response) -> Dict[str, Any]:
        try:
            detail = response.json()
        except ValueError:
            detail = response.text

        return {
            "code": response.status_code,
            "errorMessage": f"HTTP Error: {response.status_code}",
            "detail": detail,
        }


CONFIG = build_config(load_config_dict())
API_CLIENT = CityAgentApiClient(CONFIG.api, CONFIG.auth)
mcp = FastMCP(CONFIG.mcp.name)


async def call_api(
    tool_name: str, path: str, params: Optional[Dict[str, Any]] = None
) -> Dict[str, Any]:
    query_params = filter_query_params(params)
    logger.info("[%s] 请求参数: %s", tool_name, query_params)
    result = await API_CLIENT.get(path, query_params)
    logger.info("[%s] 响应摘要: %s", tool_name, preview_payload(result))
    return result


async def fetch_area_summary(areaCode: Optional[str] = None) -> Dict[str, Any]:
    return await call_api(
        "get_area_summary",
        "/api/lbm/lbmcityissues/summary",
        {"areaCode": areaCode},
    )


async def fetch_summary_indicator() -> Dict[str, Any]:
    return await call_api(
        "get_summary_indicator",
        "/api/lbm/lbmcityissues/summaryIndicator",
    )


async def fetch_safe_total(
    indicatorCode: str,
    areaCode: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_safe_total",
        "/api/lbm/lbmcityissues/safeTotal",
        {"areaCode": areaCode, "indicatorCode": indicatorCode},
    )


async def fetch_indicator_statistics(
    statistics_type: str,
    name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_indicator_statistics",
        "/api/lbm/lbmcityissues/indicator-statistics",
        with_timestamp({"type": statistics_type, "name": name}, _t),
    )


async def fetch_target_detail(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_target_detail",
        "/api/lbm/lbmestate/detail",
        with_timestamp({"issueOwnerName": target_name}, _t),
    )


async def fetch_target_issue_totals(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_target_issue_totals",
        "/api/lbm/lbmcityissues/detailToTal",
        with_timestamp({"issueOwnerName": target_name}, _t),
    )


async def fetch_target_issues(
    target_name: str,
    page_size: int,
    page_number: int,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "list_target_issues",
        "/api/lbm/lbmcityissues/page",
        with_timestamp(
            {
                "issueOwnerName": target_name,
                "size": page_size,
                "current": page_number,
            },
            _t,
        ),
    )


async def fetch_issue_detail(
    issue_id: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_issue_detail",
        "/api/lbm/lbmcityissues/questionDetails",
        with_timestamp({"id": issue_id}, _t),
    )


async def fetch_target_indicator_totals(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    return await call_api(
        "get_target_indicator_totals",
        "/api/lbm/lbmcityissues/checkIndicatorTotal",
        with_timestamp({"issueOwnerName": target_name}, _t),
    )


@mcp.tool()
async def get_region_overview(area_code: Optional[str] = None) -> Dict[str, Any]:
    """
    获取某个行政区划范围内的综合总览。

    适合在需要先看整体情况时使用，例如：
    - 想知道某个区域有哪些主要问题类型
    - 想了解重点小区、问题总量、指标分布
    - 想先拿区域总览，再决定是否继续查某个指标或某个小区

    参数：
    - area_code: 行政区划编码，可选。不传时默认查询全市范围。

    返回结果通常包含分析维度、重点对象、问题总量和指标统计。
    """
    return await fetch_area_summary(areaCode=area_code)


@mcp.tool()
async def get_dimension_statistics() -> Dict[str, Any]:
    """
    获取各分析维度的汇总统计，适合做图表、卡片汇总和维度对比。

    常见用途：
    - 统计不同分析维度分别有多少问题
    - 统计不同分析维度分别涉及多少指标
    - 为看板、柱状图、饼图提供简洁汇总数据
    """
    return await fetch_summary_indicator()


@mcp.tool()
async def list_indicator_targets(
    indicator_code: str,
    area_code: Optional[str] = None,
) -> Dict[str, Any]:
    """
    根据指标编码列出涉及该指标的问题对象列表。

    适合在已经知道某个指标后，继续查看哪些小区、楼栋或点位存在这类问题。

    参数：
    - indicator_code: 指标编码，必填。
    - area_code: 行政区划编码，可选。用于限制查询范围。

    返回结果通常包含对象名称、位置列表、关联指标编码和经纬度信息。
    """
    return await fetch_safe_total(
        indicatorCode=indicator_code,
        areaCode=area_code,
    )


@mcp.tool()
async def get_target_indicator_statistics(
    target_type: str,
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    获取某个目标对象下的指标统计明细。

    适合回答这类问题：
    - 某个小区具体有哪些问题
    - 某个区划范围内各类问题分别有多少

    参数：
    - target_type: 查询对象类型，必填。用于区分按小区查询还是按区划查询，类型((1：住房,2：小区，3：街区，4：城区))。
    - target_name: 查询对象名称，必填。可以是小区名称，也可以是区划名称。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。

    返回结果通常包含二级维度、指标名称、指标编码、问题描述和数量统计。
    """
    return await fetch_indicator_statistics(
        statistics_type=target_type,
        name=target_name,
        _t=_t,
    )


@mcp.tool()
async def get_target_detail(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    获取某个目标对象的详细信息。

    一般用于已经知道对象名称后，继续查询该对象的基础详情。

    参数：
    - target_name: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。
    """
    return await fetch_target_detail(target_name=target_name, _t=_t)


@mcp.tool()
async def get_target_issue_totals(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    获取某个目标对象的问题总量信息。

    适合快速查看：
    - 该对象总共有多少问题位置
    - 该对象总共涉及多少个指标

    参数：
    - target_name: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。
    """
    return await fetch_target_issue_totals(target_name=target_name, _t=_t)


@mcp.tool()
async def list_target_issues(
    target_name: str,
    page_size: int,
    page_number: int,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    分页列出某个目标对象下的问题列表。

    适合在列表页、表格页、长清单遍历场景中使用。

    参数：
    - target_name: 目标对象名称，通常是小区名称，必填。
    - page_size: 每页条数，必填。
    - page_number: 当前页码，必填，从 1 开始。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。
    """
    return await fetch_target_issues(
        target_name=target_name,
        page_size=page_size,
        page_number=page_number,
        _t=_t,
    )


@mcp.tool()
async def get_issue_detail(
    issue_id: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    根据问题 ID 获取单条问题的详细内容。

    适合在已经拿到问题列表后，继续查看某条问题的描述、位置和图片等详细信息。

    参数：
    - issue_id: 问题记录 ID，必填。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。
    """
    return await fetch_issue_detail(issue_id=issue_id, _t=_t)


@mcp.tool()
async def get_target_indicator_totals(
    target_name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    获取某个目标对象的指标数量统计。

    适合查看某个小区或对象分别涉及哪些指标，以及每类指标的数量。

    参数：
    - target_name: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。不传时自动补当前毫秒时间戳。
    """
    return await fetch_target_indicator_totals(target_name=target_name, _t=_t)


@mcp.tool()
async def get_area_summary(areaCode: Optional[str] = None) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_region_overview`。

    参数：
    - areaCode: 行政区划编码，可选。不传时默认查询全市范围。
    """
    return await get_region_overview(area_code=areaCode)


@mcp.tool()
async def get_city_total(
    areaCode: Optional[str] = None,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_region_overview`。

    参数：
    - areaCode: 行政区划编码，可选。不传时默认查询全市范围。
    - _t: 兼容旧调用保留字段，当前业务上可忽略。
    """
    return await get_region_overview(area_code=areaCode)


@mcp.tool()
async def get_summary_indicator() -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_dimension_statistics`。
    """
    return await get_dimension_statistics()


@mcp.tool()
async def get_safe_total(
    indicatorCode: str,
    areaCode: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `list_indicator_targets`。

    参数：
    - indicatorCode: 指标编码，必填。
    - areaCode: 行政区划编码，可选。
    """
    return await list_indicator_targets(
        indicator_code=indicatorCode,
        area_code=areaCode,
    )


@mcp.tool()
async def get_indicator_statistics(
    statistics_type: str,
    name: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_target_indicator_statistics`。

    参数：
    - statistics_type: 查询对象类型((1：住房,2：小区，3：街区，4：城区))，必填。
    - name: 查询对象名称，必填。
    - _t: 请求时间戳，可选。
    """
    return await get_target_indicator_statistics(
        target_type=statistics_type,
        target_name=name,
        _t=_t,
    )


@mcp.tool()
async def get_estate_detail(
    issueOwnerName: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_target_detail`。

    参数：
    - issueOwnerName: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。
    """
    return await get_target_detail(target_name=issueOwnerName, _t=_t)


@mcp.tool()
async def get_detail_total(
    issueOwnerName: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_target_issue_totals`。

    参数：
    - issueOwnerName: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。
    """
    return await get_target_issue_totals(target_name=issueOwnerName, _t=_t)


@mcp.tool()
async def get_issues_page(
    issueOwnerName: str,
    size: int,
    current: int,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `list_target_issues`。

    参数：
    - issueOwnerName: 目标对象名称，通常是小区名称，必填。
    - size: 每页条数，必填。
    - current: 当前页码，必填，从 1 开始。
    - _t: 请求时间戳，可选。
    """
    return await list_target_issues(
        target_name=issueOwnerName,
        page_size=size,
        page_number=current,
        _t=_t,
    )


@mcp.tool()
async def get_question_details(id: str, _t: Optional[str] = None) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_issue_detail`。

    参数：
    - id: 问题记录 ID，必填。
    - _t: 请求时间戳，可选。
    """
    return await get_issue_detail(issue_id=id, _t=_t)


@mcp.tool()
async def get_issue_statistics(
    issueOwnerName: str,
    _t: Optional[str] = None,
) -> Dict[str, Any]:
    """
    兼容别名，作用等同于 `get_target_indicator_totals`。

    参数：
    - issueOwnerName: 目标对象名称，通常是小区名称，必填。
    - _t: 请求时间戳，可选。
    """
    return await get_target_indicator_totals(target_name=issueOwnerName, _t=_t)


def run_server() -> None:
    sse_path, message_path = build_sse_paths(CONFIG.mcp)
    logger.info(
        "[server] 启动参数 host=%s port=%s sse_path=%s message_path=%s",
        CONFIG.mcp.host,
        CONFIG.mcp.port,
        sse_path,
        message_path,
    )

    if hasattr(mcp, "sse_app"):
        try:
            import uvicorn

            application = mcp.sse_app(path=sse_path, message_path=message_path)
            uvicorn.run(
                application,
                host=CONFIG.mcp.host,
                port=CONFIG.mcp.port,
                log_level="info",
            )
            return
        except TypeError as exc:
            logger.warning(
                "[server] 当前 FastMCP 版本不支持自定义 message_path，回退到 mcp.run: %s",
                exc,
            )
        except ImportError as exc:
            logger.warning(
                "[server] 未安装 uvicorn，回退到 mcp.run，message_path 可能无法带 root_path: %s",
                exc,
            )

    try:
        mcp.run(
            transport="sse",
            host=CONFIG.mcp.host,
            port=CONFIG.mcp.port,
            path=sse_path,
        )
    except TypeError as exc:
        logger.warning(
            "[server] 当前 FastMCP 版本不支持 path 参数，将使用默认 SSE 路径启动: %s",
            exc,
        )
        mcp.run(transport="sse", host=CONFIG.mcp.host, port=CONFIG.mcp.port)


if __name__ == "__main__":
    run_server()
