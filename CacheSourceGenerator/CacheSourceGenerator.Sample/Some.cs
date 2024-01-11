namespace CacheSourceGenerator.Sample;

public partial class Some
{
    [GenerateMemoryCache(MethodName = "Test")]
    public string DoTesty()
    {
        return "Jeppe";
    }
}