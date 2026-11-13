using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Interfaces;

internal sealed class SolverUnavailableFilter : IExceptionFilter {
    public void OnException(ExceptionContext context) {
        if (SolverUnavailableException.Find(context.Exception) is not { } ex)
            return;

        context.Result = new ObjectResult(ex.Body()) {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };

        context.ExceptionHandled = true;
    }
}