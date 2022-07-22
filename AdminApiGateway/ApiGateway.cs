using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AdminApiGateway.Services;
using Azure;
using EasyDesk.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AdminApiGateway;

public class ApiGateway
{
    private readonly ILogger<ApiGateway> _logger;
    private readonly ICustomersService _customersService;
    private readonly IScootersService _scootersService;

    public ApiGateway(
        ILogger<ApiGateway> logger,
        ICustomersService customersService,
        IScootersService scootersService)
    {
        _logger = logger;
        _customersService = customersService;
        _scootersService = scootersService;
    }

    [FunctionName("customers")]
    public async Task<IActionResult> GetCustomers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        return await HandleRequest(_customersService.GetCustomers);
    }

    [FunctionName("scooters")]
    public async Task<IActionResult> GetScooters(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        return await HandleRequest(_scootersService.GetScooters);
    }

    private async Task<IActionResult> HandleRequest<T>(AsyncFunc<IEnumerable<T>> result)
    {
        try
        {
            return new OkObjectResult(await result());
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError("Error [{status} - {errorCode}]: {message}", ex.Status, ex.ErrorCode, ex.Message);
            return new ObjectResult(new { Error = ex.Message })
            {
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }
}
