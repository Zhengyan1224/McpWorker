import csv
import json
import requests

add_api_url = "http://127.0.0.1:9084/kbserver/api/knowledge/add"
csv_file_path = "./zhujian.csv"

def read_csv(file_path):
    with open(file_path, mode='r', newline='', encoding='utf-8') as infile:
        reader = csv.reader(infile)
        header = next(reader)
        for row in reader:
            data = {}
            for h,r in zip(header, row):
                data[h] = r
            yield data
    


def AddKnowledge(dbName,knowledgeList,chunkSize=0):
    post_url = f"{add_api_url}?dbName={dbName}&chunkSize={chunkSize}"
    headers = {
        'Content-Type': 'application/json',
        'Accept': '*/*'
    }

    print(json.dumps(knowledgeList, ensure_ascii=False))

    with requests.post(post_url, json=knowledgeList, headers=headers) as response:
        if response.status_code == 200:
            print("Data inserted successfully.")
        else:
            print(f"Failed to insert data. Status code: {response.status_code}")
        print(f"Response: {response.text}")

def main():
    dbName = "zhujian"

    content_set = set()

    for data in read_csv(csv_file_path):
        content = data.get("content").strip()
        if content in content_set:
            print(f"Duplicate content found: {content}")
            continue
        else:
            content_set.add(content)
        _data = {'content': content, 'metaData': {'source' : data['title'] + data['module']}}
        knowledgeList = []
        knowledgeList.append(_data)
        AddKnowledge(dbName, knowledgeList, chunkSize=0)

if __name__ == "__main__":
    main()