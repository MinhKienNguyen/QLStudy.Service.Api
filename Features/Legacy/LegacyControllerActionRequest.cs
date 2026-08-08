using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace QLStudy.Service.Api.Features.Legacy
{
    public sealed record LegacyControllerActionRequest(
        string LogicTypeName,
        string ActionName,
        object?[] Arguments,
        ControllerContext ControllerContext) : IRequest<IActionResult>;
}
