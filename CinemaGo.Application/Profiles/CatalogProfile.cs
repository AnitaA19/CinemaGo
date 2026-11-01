using AutoMapper;
using CinemaGo.Domain.DTOs.GenreDTOs;
using CinemaGo.Domain.DTOs.HallDTOs;
using CinemaGo.Domain.DTOs.SeatDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Application.Profiles;

public class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        CreateMap<Genre, GenreDTO>();
        CreateMap<Hall, HallDTO>();
        CreateMap<Seat, SeatDTO>();
    }
}
