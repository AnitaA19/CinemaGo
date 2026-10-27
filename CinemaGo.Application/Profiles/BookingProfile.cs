using AutoMapper;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Application.Profiles;

public class BookingProfile : Profile
{
    public BookingProfile()
    {
        CreateMap<Booking, BookingDTO>()
            .ForMember(destination => destination.Status,
                options => options.MapFrom(source => source.Status.ToString()))
            .ForMember(destination => destination.MovieTitle,
                options => options.MapFrom(source => source.Session.Movie.Title))
            .ForMember(destination => destination.SessionStartsAt,
                options => options.MapFrom(source => source.Session.StartsAt))
            .ForMember(destination => destination.RowNumber,
                options => options.MapFrom(source => source.Seat.RowNumber))
            .ForMember(destination => destination.SeatNumber,
                options => options.MapFrom(source => source.Seat.SeatNumber))
            .ForMember(destination => destination.IsVip,
                options => options.MapFrom(source => source.Seat.IsVip))
            .ForMember(destination => destination.TicketCode,
                options => options.MapFrom(source => source.Ticket == null
                    ? (string?)null
                    : source.Ticket.TicketCode))
            .ForMember(destination => destination.FinalPrice,
                options => options.MapFrom(source => source.Ticket == null
                    ? (decimal?)null
                    : source.Ticket.FinalPrice));
    }
}
