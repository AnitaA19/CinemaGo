using System.Security.Claims;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.TicketDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    // Admin sees any ticket; a customer only their own (403 otherwise, enforced in the service).
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDTO>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(
            id,
            GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return Ok(ticket);
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid user identity.");
    }
}
