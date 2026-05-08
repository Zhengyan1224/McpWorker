# Zhengyan.HNSW

`Zhengyan.HNSW` 是 HNSW/SmallWorld 近似最近邻搜索实现库。它基于 HNSW 思路提供高效向量近邻搜索，并支持 MessagePack 序列化相关能力。

## 目标框架

```text
net9.0
```

## 主要能力

- `SmallWorld<TItem, TDistance>`：核心近邻图结构。
- `CosineDistance` / `DistanceUtils`：距离计算。
- `Graph` / `Node`：底层图结构。
- `Algorithms`：构建和搜索算法。
- `IProgressReporter`：进度报告接口。
- `GraphBuildEventSource` / `GraphSearchEventSource`：构建和搜索事件源。

## 用途

该项目通常不直接运行，而是被 `Zhengyan.VectorDB` 使用，用于构建本地向量索引和执行近似近邻搜索。
