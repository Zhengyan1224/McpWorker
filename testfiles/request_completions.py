import requests

url = "http://localhost:9083/mcphost/api/v1/chat/completions"

def post_sse(url):
    headers = {
        "Authorization": "Bearer zhengyan",
        "Content-Type": "application/json"
    }

    data = {
  "messages": [
    {
      "role": "user",
      "content": "你好/no_think"
    },
    {
      "role": "assistant",
      "content": "\u0060\u0060\u0060\n\n\n\n\u0060\u0060\u0060\n\n你好！我是郑炎工作室开发的占卜小助手，如果你有任何占卜相关的问题或者需要帮助，可以随时告诉我哦！\uD83D\uDE0A"
    },
    {
      "role": "user",
      "content": "帮我占卜一下吧"
    },
    {
      "role": "assistant",
      "content": "\u0060\u0060\u0060\n\n\n\n\u0060\u0060\u0060\n\n当然可以！使用我的数字卦占卜工具，你需要提供三个大于100的整数。这些数字可以是随机的，也可以对你有特殊的意义。请告诉我这三个数字，我将为你卜卦，并解释卦象和爻辞。\uD83D\uDE0A"
    },
    {
      "role": "user",
      "content": "112 938 532"
    }
  ],
  "model": "dev_qwen3_dn",
  "n": 1,
  "temperature": 0.9,
  "top_p": 0.9,
  "stream": True,
  "max_completion_tokens": 4096,
  "presence_penalty": 0,
  "frequency_penalty": 0
}
    
    try:
        response = requests.post(url, headers=headers,json=data, stream=True)

        response.raise_for_status()  # 检查请求是否成功
        for line in response.iter_lines():
            if line:
                # 处理每一行数据
                print(line.decode('utf-8'))
        
    except Exception as e:
        print(f"请求失败：{e}")
        return None

def main():
    post_sse(url)
if __name__ == "__main__":
    main()
