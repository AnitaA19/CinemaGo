using AutoMapper;
using CinemaGo.Domain.DTOs.GenreDTOs;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Domain.Entities;

namespace CinemaGo.Application.Profiles;

public class MovieMapper : Profile
{
    public MovieMapper()
    {
        CreateMap<Movie, MovieDTO>()
            .ForMember(d => d.Genres, o => o.MapFrom(m => m.MovieGenres
                .OrderBy(mg => mg.Genre.Name)
                .Select(mg => new GenreDTO { Id = mg.GenreId, Name = mg.Genre.Name })));
        CreateMap<CreateMovieDTO, Movie>();
        CreateMap<UpdateMovieDTO, Movie>();
        CreateMap<Movie, UpdateMovieDTO>();
        CreateMap<MovieDTO, Movie>();
        CreateMap<Movie, CreateMovieDTO>();
        CreateMap<CreateMovieDTO, MovieDTO>();
        CreateMap<MovieDTO, CreateMovieDTO>();
        CreateMap<UpdateMovieDTO, MovieDTO>();
        CreateMap<MovieDTO, UpdateMovieDTO>();
        CreateMap<CreateMovieDTO, UpdateMovieDTO>();
        CreateMap<UpdateMovieDTO, CreateMovieDTO>();
    }
}

