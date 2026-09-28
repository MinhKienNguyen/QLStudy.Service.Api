using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectsController : BaseApiController
    {
        public SubjectsController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<IActionResult> GetSubjects()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SubjectsControllerLogic), nameof(GetSubjects), Array.Empty<object?>(), ControllerContext));
                }

        [HttpGet("{id}")]
                public async Task<IActionResult> GetSubject(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SubjectsControllerLogic), nameof(GetSubject), new object?[] { id }, ControllerContext));
                }

        [HttpPost]
                public async Task<IActionResult> CreateSubject([FromBody] Subject subject)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SubjectsControllerLogic), nameof(CreateSubject), new object?[] { subject }, ControllerContext));
                }

        [HttpPut("{id}")]
                public async Task<IActionResult> UpdateSubject(int id, [FromBody] Subject subject)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SubjectsControllerLogic), nameof(UpdateSubject), new object?[] { id, subject }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteSubject(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SubjectsControllerLogic), nameof(DeleteSubject), new object?[] { id }, ControllerContext));
                }
    }
}
