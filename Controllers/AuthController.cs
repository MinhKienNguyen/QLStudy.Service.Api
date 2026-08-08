using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        public AuthController(IMediator mediator) : base(mediator)
        {
        }

        [HttpPost("login")]
                public async Task<IActionResult> Login([FromBody] AuthControllerLogic.LoginRequest request)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AuthControllerLogic), nameof(Login), new object?[] { request }, ControllerContext));
                }

        [HttpPost("logout")]
                public async Task<IActionResult> Logout()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AuthControllerLogic), nameof(Logout), Array.Empty<object?>(), ControllerContext));
                }

        [HttpPost("forgot-password")]
                public async Task<IActionResult> ForgotPassword([FromBody] AuthControllerLogic.ForgotPasswordRequest request)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AuthControllerLogic), nameof(ForgotPassword), new object?[] { request }, ControllerContext));
                }

        [HttpPost("change-password")]
                public async Task<IActionResult> ChangePassword([FromBody] AuthControllerLogic.ChangePasswordRequest request)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AuthControllerLogic), nameof(ChangePassword), new object?[] { request }, ControllerContext));
                }

        [HttpGet("me")]
                public async Task<IActionResult> Me()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AuthControllerLogic), nameof(Me), Array.Empty<object?>(), ControllerContext));
                }
    }
}
