using AutoMapper;
using CinemaGo.Domain.Constants;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Application.Services.Interfaces;

namespace CinemaGo.Application.Services.Implementations
{
    public class MovieService : IMovieService
    {
        private readonly IMovieRepository _movieRepository;
        private readonly IGenreRepository _genreRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public MovieService(IMovieRepository movieRepository, IGenreRepository genreRepository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _movieRepository = movieRepository;
            _genreRepository = genreRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        private static void ValidateMovie(Movie movie)
        {
            if (string.IsNullOrWhiteSpace(movie.Title))
            {
                throw new ValidationException("Movie title cannot be empty.");
            }
            if (movie.DurationMinutes <= 0)
            {
                throw new ValidationException("Movie duration must be greater than zero.");
            }
            if (movie.AgeRating < 0 || movie.AgeRating > 21)
            {
                throw new ValidationException("Movie age rating must be between 0 and 21.");
            }
        }

        private async Task<List<Genre>> ResolveGenresAsync(IEnumerable<int>? genreIds, CancellationToken cancellationToken)
        {
            var genres = new List<Genre>();
            foreach (var genreId in (genreIds ?? Enumerable.Empty<int>()).Distinct())
            {
                var genre = await _genreRepository.GetGenreByIdAsync(genreId, cancellationToken);
                if (genre == null)
                {
                    throw new ValidationException($"Genre with ID {genreId} does not exist.");
                }
                genres.Add(genre);
            }
            return genres;
        }

        public async Task<ICollection<MovieDTO>> GetAllMoviesAsync(CancellationToken cancellationToken)
        {
            var movies = await _movieRepository.GetAllMoviesAsync(cancellationToken);
            return movies.Select(_mapper.Map<Movie, MovieDTO>).ToList();
        }

        public async Task<PagedMoviesDTO> GetMoviesAsync(string? title, int? genreId, int page, int pageSize, CancellationToken cancellationToken)
        {
            if (page < 1)
            {
                throw new ValidationException("Page must be greater than or equal to 1.");
            }
            if (pageSize < 1 || pageSize > PaginationLimits.MaxPageSize)
            {
                throw new ValidationException($"Page size must be between 1 and {PaginationLimits.MaxPageSize}.");
            }
            if (genreId.HasValue && genreId.Value < 1)
            {
                throw new ValidationException("Genre id must be greater than zero.");
            }

            var (movies, totalCount) = await _movieRepository.GetMoviesPagedAsync(
                title?.Trim(), genreId, page, pageSize, cancellationToken);

            return new PagedMoviesDTO
            {
                Movies = movies.Select(_mapper.Map<Movie, MovieDTO>).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
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
            foreach (var genre in await ResolveGenresAsync(createMovieDto.GenreIds, cancellationToken))
            {
                movie.Genres.Add(genre);
            }
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
            existingMovie.PosterUrl = string.IsNullOrWhiteSpace(updateMovieDto.PosterUrl) ? null : updateMovieDto.PosterUrl.Trim();
            ValidateMovie(existingMovie);

            var genres = await ResolveGenresAsync(updateMovieDto.GenreIds, cancellationToken);
            var wantedIds = genres.Select(g => g.Id).ToHashSet();
            foreach (var stale in existingMovie.Genres.Where(g => !wantedIds.Contains(g.Id)).ToList())
            {
                existingMovie.Genres.Remove(stale);
            }
            foreach (var genre in genres.Where(g => existingMovie.Genres.All(existing => existing.Id != g.Id)))
            {
                existingMovie.Genres.Add(genre);
            }

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

        public Task<ICollection<MovieSalesDTO>> GetSalesByMovieAsync(CancellationToken cancellationToken) =>
            _movieRepository.GetSalesByMovieAsync(cancellationToken);
    }
}


