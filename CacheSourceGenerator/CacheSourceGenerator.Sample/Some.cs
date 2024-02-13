using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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
    
    [GenerateMemoryCache(MethodName = "Angriest")]
    public string DoAngry([IgnoreKey]int factor, string customId)
    {
        return "Bruce Banner";
    }

    partial void OnCallingDoAngry(int factor, string customId)
    {
        Debug.WriteLine($"{factor}{customId} calling");
    }

    partial void OnCalledDoAngry(int factor, string customId, string _returned_)
    {
        Debug.WriteLine($"{factor}{customId} {_returned_} called");
    }
}

public partial class ListTypeReturned
{
    [GenerateMemoryCache(MethodName = "CachedGetResult")]
    private List<string> GetResult(string parameter)
    {
        return [parameter];
    }

    [GenerateMemoryCache(MethodName = "CacheAsyncResult")]
    private async Task<List<string>> GetResultAsync(string parameter)
    {
        return [parameter];
    }
}