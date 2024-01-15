namespace CacheSourceGenerator.Sample;

public partial class Some
{
    [GenerateMemoryCache(MethodName = "Test")]
    public string DoTesty()
    {
        return "Jeppe";
    }
}

public partial class Another
{
    
    [GenerateMemoryCache(MethodName = "Angriest", KeyGenerator = nameof(GetCacheKey))]
    public string DoAngry(int factor, string customId)
    {
        return "Bruce Banner";
    }

    public object GetCacheKey(int factor, string customId)
    {
        return new {Name = "Angry", VeryAngry = factor, customId};
    }
}