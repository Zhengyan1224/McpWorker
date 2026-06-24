public class ObjectConfig
{
    public string Type { get; set; }
    public ParameterConfig[] Parameters { get; set; }
}

// 定义一个类来表示 Parameters 的结构
public class ParameterConfig
{
    public string Type { get; set; }
    public object Value { get; set; }

    public Type ToParameterType()
    {
        return System.Type.GetType(Type) ?? throw new ArgumentException($"Type '{Type}' not found.");
    }
}