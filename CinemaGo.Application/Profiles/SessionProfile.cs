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
        CreateMap<Session, SessionDTO>();
    }
}
