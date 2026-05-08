
using System.Collections.Concurrent;
using JiebaNet.Segmenter.Common;

namespace Zhengyan.KnowledgeBase;

public enum LifeCycle
{
    Load,
    Create
}

public delegate T KBInitBehaviorDelegate<T>(string name, string storageBaseDir, ConcurrentDictionary<string, object> dependencyCollection, LifeCycle lifeCycle) where T : IKnowledgeBase;
public delegate bool KBDisposeBehaviorDelegate<T>(string name, T kb) where T : IKnowledgeBase;
public delegate bool KBSaveBehaviorDelegate<T>(string name, T kb) where T : IKnowledgeBase;
public class KnowledgeBaseManager<T> where T : IKnowledgeBase
{
    public string StorageBaseDir { get; set; }


    public ConcurrentDictionary<string, T> KnowledgeBases { get; private set; }

    public ConcurrentDictionary<string, object> DependencyCollection { get; private set; }

    private KBInitBehaviorDelegate<T> _kbInitBehavior;
    private KBDisposeBehaviorDelegate<T> _kbDisposeBehavior;
    private KBSaveBehaviorDelegate<T> _kbSaveBehavior;

    private KnowledgeBaseManager()
    {
        KnowledgeBases = new ConcurrentDictionary<string, T>();
        DependencyCollection = new ConcurrentDictionary<string, object>();
    }

    public static KnowledgeBaseManager<T> Create() => new KnowledgeBaseManager<T>();

    public KnowledgeBaseManager<T> ConfigureKBInitBehavior(KBInitBehaviorDelegate<T> kbInitBehavior)
    {
        this._kbInitBehavior = kbInitBehavior;
        return this;
    }

    public KnowledgeBaseManager<T> ConfigureKBDisposeBehavior(KBDisposeBehaviorDelegate<T> kbDisposeBehavior)
    {
        this._kbDisposeBehavior = kbDisposeBehavior;
        return this;
    }

    public KnowledgeBaseManager<T> ConfigureKBSaveBehavior(KBSaveBehaviorDelegate<T> kbSaveBehavior)
    {
        this._kbSaveBehavior = kbSaveBehavior;
        return this;
    }

    public bool AddDependency(string name, object dependencyObj)
    {
        return DependencyCollection.AddDependency(name, dependencyObj);
    }

    public D GetDependency<D>(string name)
    {
        return DependencyCollection.GetDependency<D>(name);
    }

    public bool RemoveDependency(string name)
    {
        return DependencyCollection.RemoveDependency(name);
    }

    public KnowledgeBaseManager<T> ClearDependency()
    {
        DependencyCollection.Clear();
        return this;
    }

    public KnowledgeBaseManager<T> Load(string storageBaseDir)
    {
        this.StorageBaseDir = storageBaseDir;
        this.StorageBaseDir.CreateDirectory();
        string[] dirpaths = Directory.GetDirectories(this.StorageBaseDir, "*", SearchOption.TopDirectoryOnly);
        foreach (string dirpath in dirpaths)
        {
            string name = Path.GetFileName(dirpath);

            var kb = CreateKnowledgeBase(name, LifeCycle.Load);
            if (kb != null)
                AddKnowledgeBase(name, kb, false);
        }

        return this;
    }

    /// <summary>
    /// 根据知识库名获取知识库（如果知识库不存在则创建）
    /// </summary>
    /// <param name="name">知识库名</param>
    /// <returns>知识库对象</returns>
    public T GetKnowledgeBase(string name)
    {

        if (KnowledgeBases.TryGetValue(name, out T kb))
            return kb;

        kb = CreateKnowledgeBase(name, LifeCycle.Create);
        if (kb != null)
        {
            AddKnowledgeBase(name, kb, true);
        }
        return kb;
    }

    public T? CreateKnowledgeBase(string name, LifeCycle lifeCycle)
    {
        if (_kbInitBehavior != null)
            return _kbInitBehavior(name, StorageBaseDir, this.DependencyCollection, lifeCycle);
        return default;
    }

    public bool AddKnowledgeBase(string name, T kb, bool createNew = false)
    {
        if (createNew)
            DeleteKnowledgeBase(name);
        return KnowledgeBases.TryAdd(name, kb);
    }

    public bool DeleteKnowledgeBase(string name)
    {
        if (KnowledgeBases.TryRemove(name, out T kb))
        {
            if (_kbDisposeBehavior != null)
                return _kbDisposeBehavior(name, kb);
            return true;
        }
        return false;
    }

    public bool SaveKnowledgeBase(string name)
    {
        if (KnowledgeBases.TryGetValue(name, out T kb))
        {
            if (_kbSaveBehavior != null)
                return _kbSaveBehavior(name, kb);
            return true;
        }
        return false;
    }
}