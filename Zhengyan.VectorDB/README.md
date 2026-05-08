# Zhengyan.VectorDB

`Zhengyan.VectorDB` 是本地向量存储和近邻检索封装库，服务于 DataQuery、Memory、KnowledgeBase 等需要语义召回的项目。

## 目标框架

```text
net9.0
```

## 主要能力

- `IVectorDB`：向量库接口。
- `CVector`：向量数据结构。
- `LiteVectorDB` / `LiteVectorDBV2`：本地向量库实现。
- `IDataStorage`：数据持久化接口。
- `LocalDiskDataStorage`：本地磁盘存储实现。
- `BitmapFileManager`：辅助管理 bitmap 文件。

## 依赖

```text
Zhengyan.HNSW
HNSWIndex
MessagePack
```

这个项目不单独运行，由需要本地向量检索的上层项目引用。
