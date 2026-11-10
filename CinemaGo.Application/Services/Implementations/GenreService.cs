using AutoMapper;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Domain.DTOs.GenreDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;

namespace CinemaGo.Application.Services.Implementations;

public class GenreService : IGenreService
{
    private readonly IGenreRepository _genreRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GenreService(IGenreRepository genreRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _genreRepository = genreRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ICollection<GenreDTO>> GetAllGenresAsync(CancellationToken cancellationToken)
    {
        var genres = await _genreRepository.GetAllGenresAsync(cancellationToken);
        return genres.OrderBy(g => g.Name).Select(_mapper.Map<GenreDTO>).ToList();
    }

    public async Task<GenreDTO?> GetGenreByIdAsync(int id, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetGenreByIdAsync(id, cancellationToken);
        return genre is null ? null : _mapper.Map<GenreDTO>(genre);
    }

    public async Task<GenreDTO> AddGenreAsync(CreateGenreDTO dto, CancellationToken cancellationToken)
    {
        var name = NormalizeName(dto.Name);
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var genre = new Genre { Name = name };
        await _genreRepository.AddGenreAsync(genre, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<GenreDTO>(genre);
    }

    public async Task<GenreDTO?> UpdateGenreAsync(int id, UpdateGenreDTO dto, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetGenreByIdAsync(id, cancellationToken);
        if (genre is null)
        {
            return null;
        }

        var name = NormalizeName(dto.Name);
        await EnsureNameIsUniqueAsync(name, id, cancellationToken);

        genre.Name = name;
        _genreRepository.UpdateGenreAsync(genre);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<GenreDTO>(genre);
    }

    public async Task<bool> DeleteGenreAsync(int id, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetGenreByIdAsync(id, cancellationToken);
        if (genre is null)
        {
            return false;
        }

        if (genre.Movies.Count > 0)
        {
            throw new ConflictException("A genre assigned to movies cannot be deleted.");
        }

        _genreRepository.DeleteGenreAsync(genre);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ValidationException("Genre name cannot be empty.");
        }

        if (trimmed.Length > 100)
        {
            throw new ValidationException("Genre name cannot be longer than 100 characters.");
        }

        return trimmed;
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var genres = await _genreRepository.GetAllGenresAsync(cancellationToken);
        if (genres.Any(g => g.Id != excludedId && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"A genre named '{name}' already exists.");
        }
    }
}
