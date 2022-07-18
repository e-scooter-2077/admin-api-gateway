using Azure.DigitalTwins.Core;
using System.Text.Json;

namespace AdminApiGateway;

public static class Extensions
{
    public static JsonElement ReadProperty(this BasicDigitalTwin twin, string propertyName) =>
        (JsonElement)twin.Contents[propertyName];
}
