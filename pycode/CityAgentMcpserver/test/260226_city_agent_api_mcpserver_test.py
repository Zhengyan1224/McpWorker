import asyncio
import json
import os

import yaml
from fastmcp import Client

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(PROJECT_ROOT, "config.yaml")
RESULT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_res.json")


def load_config():
    with open(CONFIG_PATH, "r", encoding="utf-8") as file:
        return yaml.safe_load(file) or {}


def normalize_root_path(root_path):
    if root_path is None:
        return "cityagent"
    return str(root_path).strip().strip("/")


def build_sse_url():
    config = load_config()
    mcp_config = config.get("mcp", {})

    host = str(mcp_config.get("host", "127.0.0.1"))
    if host in {"0.0.0.0", "::"}:
        host = "127.0.0.1"

    port = int(mcp_config.get("port", 28080))
    root_path = normalize_root_path(mcp_config.get("root_path", "cityagent"))
    sse_path = f"/{root_path}/sse" if root_path else "/sse"
    return f"http://{host}:{port}{sse_path}"


client = Client(build_sse_url())


def extract(obj):
    if hasattr(obj, "data"):
        return obj.data
    if hasattr(obj, "structured_content"):
        return obj.structured_content
    if isinstance(obj, Exception):
        return {"error": str(obj)}
    return obj


async def call_tool_safely(tool_name, arguments):
    try:
        result = await client.call_tool(tool_name, arguments)
        return extract(result)
    except Exception as exc:  # noqa: BLE001
        return extract(exc)


async def test_all_tools():
    results = {}

    async with client:
        tools = await client.list_tools()
        results["available_tools"] = extract(tools)

        results["get_region_overview"] = await call_tool_safely(
            "get_region_overview", {}
        )
        results["get_dimension_statistics"] = await call_tool_safely(
            "get_dimension_statistics", {}
        )
        results["list_indicator_targets"] = await call_tool_safely(
            "list_indicator_targets",
            {"indicator_code": "N-ESHU"},
        )
        results["get_target_indicator_statistics"] = await call_tool_safely(
            "get_target_indicator_statistics",
            {"target_type": "1", "target_name": "达明新村"},
        )

        results["legacy_get_area_summary"] = await call_tool_safely(
            "get_area_summary", {}
        )

    with open(RESULT_PATH, "w", encoding="utf-8") as file:
        json.dump(results, file, ensure_ascii=False, indent=2)

    print(f"All tests completed. Results saved to {RESULT_PATH}")
    return results


if __name__ == "__main__":
    asyncio.run(test_all_tools())
