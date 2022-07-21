using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdminApiGateway.Services;

public interface ICustomersService
{
    Task<IEnumerable<CustomerDto>> GetCustomers();
}

public record CustomerDto(
    Guid Id,
    string Username);
