using Azure.DigitalTwins.Core;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdminApiGateway;

public static class Extensions
{
    public static async Task<IEnumerable<T>> ListQuery<T>(this DigitalTwinsClient client, string query)
    {
        var result = client.QueryAsync<T>(query);
        var items = new List<T>();
        await foreach (var twin in result)
        {
            items.Add(twin);
        }
        return items;
    }

    public static JsonElement ReadProperty(this BasicDigitalTwin twin, string propertyName) =>
        (JsonElement)twin.Contents[propertyName];
}
