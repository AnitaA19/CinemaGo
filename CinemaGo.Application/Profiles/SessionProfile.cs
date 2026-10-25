using AutoMapper;
using CinemaGo.Domain.DTOs.SessionDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Application.Profiles;

public class SessionProfile : Profile
{
    public SessionProfile()
    {
        CreateMap<CreateSessionDTO, Session>();
        CreateMap<UpdateSessionDTO, Session>();
        CreateMap<Session, SessionDTO>()
            .ForMember(d => d.MovieTitle, o => o.MapFrom(s => s.Movie.Title))
            .ForMember(d => d.DurationMinutes, o => o.MapFrom(s => s.Movie.DurationMinutes))
            .ForMember(d => d.HallName, o => o.MapFrom(s => s.Hall.Name))
            .ForMember(d => d.EndsAt, o => o.MapFrom(s => s.StartsAt.AddMinutes(s.Movie.DurationMinutes)));
    }
}
