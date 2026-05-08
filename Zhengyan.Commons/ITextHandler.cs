namespace Zhengyan.Commons
{
    public interface ITextHandler
    {
        void Begin();
        void ProcessChar(char ch, int index);
        void End();
        object GetResult();
    }
}