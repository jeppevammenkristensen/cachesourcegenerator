using System;
using System.Threading.Tasks;

namespace CacheSourceGenerator.Sample;

// This code will not compile until you build the project with the Source Generators


public partial class SampleEntity
{
    public int Id { get; } = 42;
    public string? Name { get; } = "Sample";

    [Cacho(MethodName = "GetId")]
    private string DoGetId(int i)
    {
        return "Jeppe";
    }

    [Cacho(MethodName = "GetIdAsync")]
    public async Task<string> DoGetIdAsync()
    {
        await Task.Delay(3000);
        return "Jeppe";
    }
}