using System.Security.Claims;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.BookingDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaGo.Web.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    public async Task<ActionResult<BookingDTO>> Book(
        CreateBookingDTO dto,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingService.BookTicketAsync(
            dto,
            GetUserId(),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    [HttpGet("my")]
    public async Task<ActionResult<ICollection<BookingDTO>>> GetMyBookings(
        CancellationToken cancellationToken)
    {
        return Ok(await _bookingService.GetUserBookingsAsync(
            GetUserId(),
            cancellationToken));
    }

    // Admin sees any booking; a user only sees their own.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDTO>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = User.IsInRole("Admin")
            ? await _bookingService.GetBookingByIdAsync(id, cancellationToken)
            : await _bookingService.GetUserBookingByIdAsync(id, GetUserId(), cancellationToken);

        return booking is null ? NotFound() : Ok(booking);
    }

    // Admin cancels any booking; a user only their own (enforced in the service).
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        await _bookingService.CancelBookingAsync(
            id,
            GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ICollection<BookingDTO>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _bookingService.GetAllBookingsAsync(cancellationToken));
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid user identity.");
    }
}
