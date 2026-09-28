using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace QLStudy.Service.Api.Features.Legacy
{
    public sealed class LegacyControllerActionHandler : IRequestHandler<LegacyControllerActionRequest, IActionResult>
    {
        private readonly IServiceProvider _serviceProvider;

        public LegacyControllerActionHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<IActionResult> Handle(LegacyControllerActionRequest request, CancellationToken cancellationToken)
        {
            var logicType = typeof(LegacyControllerActionHandler).Assembly
                .GetTypes()
                .FirstOrDefault(type =>
                    type.Namespace == typeof(LegacyControllerActionHandler).Namespace &&
                    type.Name == request.LogicTypeName);

            if (logicType == null)
            {
                return new NotFoundObjectResult($"CQRS handler target '{request.LogicTypeName}' was not found.");
            }

            var logic = ActivatorUtilities.CreateInstance(_serviceProvider, logicType);
            if (logic is ControllerBase controllerBase)
            {
                controllerBase.ControllerContext = request.ControllerContext;
            }

            var method = logicType
                .GetMethods()
                .Where(method => method.Name == request.ActionName)
                .FirstOrDefault(method => method.GetParameters().Length == request.Arguments.Length);

            if (method == null)
            {
                return new NotFoundObjectResult($"CQRS action '{request.ActionName}' was not found.");
            }

            var invocationResult = method.Invoke(logic, request.Arguments);
            if (invocationResult is Task task)
            {
                await task.ConfigureAwait(false);
                invocationResult = task.GetType().GetProperty("Result")?.GetValue(task);
            }

            return ToActionResult(invocationResult);
        }

        private static IActionResult ToActionResult(object? result)
        {
            if (result is IActionResult actionResult)
            {
                return actionResult;
            }

            if (result is IConvertToActionResult convertible)
            {
                return convertible.Convert();
            }

            return new ObjectResult(result);
        }
    }
}
