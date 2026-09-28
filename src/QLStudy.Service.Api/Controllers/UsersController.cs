using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseApiController
    {
        public UsersController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<IActionResult> GetUsers()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(GetUsers), Array.Empty<object?>(), ControllerContext));
                }

        [HttpGet("{id}")]
                public async Task<IActionResult> GetUser(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(GetUser), new object?[] { id }, ControllerContext));
                }

        [HttpPost]
                public async Task<IActionResult> CreateUser([FromBody] UsersControllerLogic.UserDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(CreateUser), new object?[] { dto }, ControllerContext));
                }

        [HttpPut("{id}")]
                public async Task<IActionResult> UpdateUser(int id, [FromBody] UsersControllerLogic.UserDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(UpdateUser), new object?[] { id, dto }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteUser(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(DeleteUser), new object?[] { id }, ControllerContext));
                }

        [HttpGet("permissions")]
                public async Task<IActionResult> GetPermissions()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(GetPermissions), Array.Empty<object?>(), ControllerContext));
                }

        [HttpPut("permissions")]
                public async Task<IActionResult> UpdatePermissions([FromBody] List<ScreenPermission> permissions)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(UsersControllerLogic), nameof(UpdatePermissions), new object?[] { permissions }, ControllerContext));
                }
    }
}
