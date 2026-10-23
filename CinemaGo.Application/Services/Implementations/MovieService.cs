using AutoMapper;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Application.Services.Interfaces;

namespace CinemaGo.Application.Services.Implementations
{
    public class MovieService : IMovieService
    {
        private readonly IMovieRepository _movieRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public MovieService(IMovieRepository movieRepository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _movieRepository = movieRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        private static void ValidateMovie(Movie movie)
        {
            if (string.IsNullOrWhiteSpace(movie.Title))
            {
                throw new ArgumentException("Movie title cannot be empty.");
            }
            if (movie.DurationMinutes <= 0)
            {
                throw new ArgumentException("Movie duration must be greater than zero.");
            }
            if (movie.AgeRating < 0 && movie.AgeRating > 21)
            {
                throw new ArgumentException("Movie age rating cannot be negative.");
            }
        }

        public async Task<ICollection<MovieDTO>> GetAllMoviesAsync(CancellationToken cancellationToken)
        {
            var movies = await _movieRepository.GetAllMoviesAsync(cancellationToken);
            return movies.Select(_mapper.Map<Movie, MovieDTO>).ToList();
        }

        public async Task<MovieDTO?> GetMovieByIdAsync(int id, CancellationToken cancellationToken)
        {
            var movie = await _movieRepository.GetMovieByIdAsync(id, cancellationToken);
            return movie == null ? null : _mapper.Map<Movie, MovieDTO>(movie);
        }

        public async Task<MovieDTO> AddMovieAsync(CreateMovieDTO createMovieDto, CancellationToken cancellationToken)
        {
            var movie = _mapper.Map<CreateMovieDTO, Movie>(createMovieDto);
            ValidateMovie(movie);
            await _movieRepository.AddMovieAsync(movie, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Movie, MovieDTO>(movie);
        }

        public async Task<MovieDTO?> UpdateMovieAsync(int id, UpdateMovieDTO updateMovieDto, CancellationToken cancellationToken)
        {
            var existingMovie = await _movieRepository.GetMovieByIdAsync(id, cancellationToken);
            if (existingMovie == null)
            {
                return null;
            }
            existingMovie.Title = updateMovieDto.Title;
            existingMovie.DurationMinutes = updateMovieDto.DurationMinutes;
            existingMovie.AgeRating = updateMovieDto.AgeRating;
            ValidateMovie(existingMovie);
            _movieRepository.UpdateMovieAsync(existingMovie);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Movie, MovieDTO>(existingMovie);
        }

        public async Task<bool> DeleteMovieAsync(int id, CancellationToken cancellationToken)
        {
            var movie = await _movieRepository.GetMovieByIdAsync(id, cancellationToken);
            if (movie == null)
            {
                return false;
            }
            _movieRepository.DeleteMovieAsync(movie);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}