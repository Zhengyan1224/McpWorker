# Zhengyan.KnowledgeBase

`Zhengyan.KnowledgeBase` 是本地知识库抽象和实现库，主要被 `Zhengyan.KBServer` 使用。

## 目标框架

```text
net9.0
```

## 主要能力

- `IKnowledgeBase`：知识库增删查接口。
- `SimpleKnowledgeBase`：基于本地向量库的基础知识库实现。
- `TextFeaturesEnhancedKnowledgeBase`：支持摘要、标签等文本特征增强。
- `KnowledgeBaseManager<T>`：管理多个知识库实例和生命周期。
- `ITextEmbedder`：文本 embedding 抽象。
- `ITextProcessor`：文本切分、摘要、关键词等处理抽象。
- `DefaultTextProcessor`：默认文本处理实现，使用 `jieba.NET`。

## 依赖

```text
Zhengyan.VectorDB
Zhengyan.Commons
jieba.NET
```

这个项目不单独运行，由知识库服务或需要本地知识库能力的项目引用。
