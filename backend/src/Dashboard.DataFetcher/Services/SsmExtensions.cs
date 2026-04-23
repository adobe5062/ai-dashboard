using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Dashboard.DataFetcher.Services;

public static class SsmExtensions
{
    public static async Task<string> GetDecryptedAsync(
        this IAmazonSimpleSystemsManagement ssm, string parameterName)
    {
        var response = await ssm.GetParameterAsync(new GetParameterRequest
        {
            Name = parameterName,
            WithDecryption = true,
        });
        return response.Parameter.Value;
    }
}
