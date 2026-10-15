using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Models;

namespace Yurt.WebApi.Common;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToResult<T>(Result<T> result)
    {
        if (result.Succeeded)
        {
            return result.StatusCode switch
            {
                201 => StatusCode(201, result.Data),
                204 => NoContent(),
                _ => Ok(result.Data)
            };
        }

        if (result.StatusCode == 423)
        {
            var problem = new ProblemDetails { Status = 423, Title = result.Error };
            if (result.MinutesRemaining.HasValue)
                problem.Extensions["minutesRemaining"] = result.MinutesRemaining.Value;
            return StatusCode(423, problem);
        }

        return result.StatusCode switch
        {
            401 => Unauthorized(new ProblemDetails { Title = result.Error }),
            403 => StatusCode(403, new ProblemDetails { Title = result.Error }),
            404 => NotFound(new ProblemDetails { Title = result.Error }),
            409 => Conflict(new ProblemDetails { Title = result.Error }),
            422 => UnprocessableEntity(new ProblemDetails { Title = result.Error }),
            >= 400 and not 400 => StatusCode(
                result.StatusCode, new ProblemDetails { Status = result.StatusCode, Title = result.Error }),
            _ => BadRequest(new ProblemDetails { Title = result.Error })
        };
    }

    protected IActionResult ValidationError(string message)
        => BadRequest(new ProblemDetails { Title = "Validation Error", Detail = message });
}
